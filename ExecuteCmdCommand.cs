using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using Task = System.Threading.Tasks.Task;

namespace SuperVs
{
    internal sealed class ExecuteCmdCommand
    {
        public const int CommandId = 0x0120;

        public static readonly Guid CommandSet = new Guid("8e1a11a1-eaf8-491f-9155-e899028fe36a");

        private readonly AsyncPackage package;

        private ExecuteCmdCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var command = new OleMenuCommand(Execute, new CommandID(CommandSet, CommandId));
            command.BeforeQueryStatus += BeforeQueryStatus;
            commandService.AddCommand(command);
        }

        public static ExecuteCmdCommand Instance { get; private set; }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new ExecuteCmdCommand(package, commandService);
        }

        private void BeforeQueryStatus(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var command = (OleMenuCommand)sender;
            string file = PathSelectionResolver.GetSelectionContext(package)?.SelectedPath;
            bool isCmdFile = IsCmdFile(file);
            command.Visible = isCmdFile;
            command.Enabled = isCmdFile;
        }

        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string file = PathSelectionResolver.GetSelectionContext(package)?.SelectedPath;
            if (!IsCmdFile(file))
            {
                return;
            }

            try
            {
                var fileInfo = new FileInfo(file);
                string cmdExe = Environment.GetEnvironmentVariable("COMSPEC");
                if (string.IsNullOrWhiteSpace(cmdExe))
                {
                    cmdExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = cmdExe,
                    Arguments = "/k call \"" + fileInfo.FullName + "\"",
                    WorkingDirectory = fileInfo.DirectoryName,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Normal
                };

                startInfo.EnvironmentVariables.Remove("VisualStudioVersion");
                startInfo.EnvironmentVariables.Remove("VSToolsPath");

                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                VsShellUtilities.ShowMessageBox(
                    package,
                    ex.Message,
                    "Execute CMD",
                    OLEMSGICON.OLEMSGICON_WARNING,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
        }

        private static bool IsCmdFile(string file)
        {
            return !string.IsNullOrWhiteSpace(file)
                && File.Exists(file)
                && string.Equals(Path.GetExtension(file), ".cmd", StringComparison.OrdinalIgnoreCase);
        }
    }
}
