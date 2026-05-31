using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace SuperVs
{
    internal sealed class FtpUploadCommand
    {
        public const int CommandId = 0x0130;

        public static readonly Guid CommandSet = new Guid("8e1a11a1-eaf8-491f-9155-e899028fe36a");

        private readonly AsyncPackage package;

        private FtpUploadCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var command = new OleMenuCommand(Execute, new CommandID(CommandSet, CommandId));
            command.BeforeQueryStatus += BeforeQueryStatus;
            commandService.AddCommand(command);
        }

        public static FtpUploadCommand Instance { get; private set; }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new FtpUploadCommand(package, commandService);
        }

        private void BeforeQueryStatus(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var command = (OleMenuCommand)sender;
            SelectionContext context = PathSelectionResolver.GetSelectionContext(package);
            command.Visible = context != null && context.IsFile;
            command.Enabled = command.Visible;
        }

        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string file = PathSelectionResolver.GetSelectionContext(package)?.SelectedPath;
            if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
            {
                return;
            }

            _ = Task.Run(async () => await UploadSelectedFileAsync(file));
        }

        private async Task UploadSelectedFileAsync(string file)
        {
            string fileName = Path.GetFileName(file);

            try
            {
                FtpSettings settings = FtpSettings.FindForFile(file);
                if (settings == null)
                {
                    await StatusBar.SetTextAsync(package, "FTP skipped: no ftp.settings or ftp.ext.settings found for " + fileName + ".");
                    return;
                }

                string relativePath = GetRelativePath(settings.RootDirectory, file);
                string remoteFilePath = CombineRemotePath(settings.RemoteBaseFolder, relativePath);
                string remoteDirectory = GetRemoteDirectory(remoteFilePath);

                await StatusBar.SetTextAsync(package, "FTP uploading " + fileName + "...");
                await EnsureRemoteDirectoriesAsync(settings, remoteDirectory);
                await UploadFileAsync(settings, remoteFilePath, file);
                await StatusBar.SetTextAsync(package, "FTP uploaded " + fileName + " to " + settings.Server + " " + remoteDirectory + ".");
            }
            catch (Exception ex)
            {
                await StatusBar.SetTextAsync(package, "FTP failed for " + fileName + ": " + ex.Message);
            }
        }

        private static async Task EnsureRemoteDirectoriesAsync(FtpSettings settings, string remoteDirectory)
        {
            string normalized = NormalizeRemotePath(remoteDirectory);
            string[] segments = normalized.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            string current = normalized.StartsWith("/", StringComparison.Ordinal) ? "/" : string.Empty;

            foreach (string segment in segments)
            {
                current = CombineRemotePath(current, segment);
                try
                {
                    FtpWebRequest request = CreateRequest(settings, current);
                    request.Method = WebRequestMethods.Ftp.MakeDirectory;
                    using (var response = (FtpWebResponse)await request.GetResponseAsync())
                    {
                    }
                }
                catch (WebException ex)
                {
                    if (!(ex.Response is FtpWebResponse response) || !IsDirectoryAlreadyExists(response.StatusCode))
                    {
                        throw;
                    }
                }
            }
        }

        private static async Task UploadFileAsync(FtpSettings settings, string remoteFilePath, string localFilePath)
        {
            FtpWebRequest request = CreateRequest(settings, remoteFilePath);
            request.Method = WebRequestMethods.Ftp.UploadFile;
            request.UseBinary = true;
            request.KeepAlive = false;

            byte[] bytes = File.ReadAllBytes(localFilePath);
            request.ContentLength = bytes.Length;

            using (Stream requestStream = await request.GetRequestStreamAsync())
            {
                await requestStream.WriteAsync(bytes, 0, bytes.Length);
            }

            using (var response = (FtpWebResponse)await request.GetResponseAsync())
            {
            }
        }

        private static FtpWebRequest CreateRequest(FtpSettings settings, string remotePath)
        {
            var request = (FtpWebRequest)WebRequest.Create(BuildUri(settings.Server, remotePath));
            request.Credentials = new NetworkCredential(settings.Username, settings.Password);
            request.UsePassive = true;
            request.KeepAlive = false;
            return request;
        }

        private static Uri BuildUri(string server, string remotePath)
        {
            string normalizedServer = server.Trim();
            if (!normalizedServer.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase)
                && !normalizedServer.StartsWith("ftps://", StringComparison.OrdinalIgnoreCase))
            {
                normalizedServer = "ftp://" + normalizedServer;
            }

            normalizedServer = normalizedServer.TrimEnd('/');
            string normalizedPath = NormalizeRemotePath(remotePath).TrimStart('/');
            string escapedPath = string.Join("/", normalizedPath.Split('/').Select(Uri.EscapeDataString));
            return new Uri(normalizedServer + "/" + escapedPath);
        }

        private static bool IsDirectoryAlreadyExists(FtpStatusCode statusCode)
        {
            return statusCode == FtpStatusCode.ActionNotTakenFileUnavailable
                || statusCode == FtpStatusCode.ActionNotTakenFilenameNotAllowed;
        }

        private static string GetRelativePath(string root, string path)
        {
            Uri rootUri = new Uri(EnsureTrailingSlash(Path.GetFullPath(root)));
            Uri pathUri = new Uri(Path.GetFullPath(path));
            string relative = Uri.UnescapeDataString(rootUri.MakeRelativeUri(pathUri).ToString());
            return relative.Replace('\\', '/');
        }

        private static string CombineRemotePath(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left))
            {
                return NormalizeRemotePath(right);
            }

            if (string.IsNullOrWhiteSpace(right))
            {
                return NormalizeRemotePath(left);
            }

            return NormalizeRemotePath(left).TrimEnd('/') + "/" + NormalizeRemotePath(right).TrimStart('/');
        }

        private static string GetRemoteDirectory(string remoteFilePath)
        {
            string normalized = NormalizeRemotePath(remoteFilePath);
            int lastSlash = normalized.LastIndexOf('/');
            return lastSlash < 0 ? "/" : normalized.Substring(0, lastSlash + 1);
        }

        private static string NormalizeRemotePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return "/";
            }

            return path.Replace('\\', '/').Replace("//", "/");
        }

        private static string EnsureTrailingSlash(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }
    }
}
