using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Design;
using System.Windows.Forms;
using Task = System.Threading.Tasks.Task;

namespace SuperVs
{
    internal sealed class CopyRelativePathCommand
    {
        public const int CommandId = 0x0110;

        public static readonly Guid CommandSet = new Guid("8e1a11a1-eaf8-491f-9155-e899028fe36a");

        private readonly AsyncPackage package;

        private CopyRelativePathCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var command = new OleMenuCommand(Execute, new CommandID(CommandSet, CommandId));
            command.BeforeQueryStatus += BeforeQueryStatus;
            commandService.AddCommand(command);
        }

        public static CopyRelativePathCommand Instance { get; private set; }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new CopyRelativePathCommand(package, commandService);
        }

        private void BeforeQueryStatus(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var command = (OleMenuCommand)sender;
            SelectionContext context = PathSelectionResolver.GetSelectionContext(package);
            string relativePath = context?.GetRelativePath();
            command.Visible = !string.IsNullOrWhiteSpace(relativePath);
            command.Enabled = !string.IsNullOrWhiteSpace(relativePath);
        }

        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string relativePath = PathSelectionResolver.GetSelectionContext(package)?.GetRelativePath();
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return;
            }

            try
            {
                Clipboard.SetText(relativePath);
                ThreadHelper.JoinableTaskFactory.Run(() => StatusBar.SetTextAsync(package, "Copied relative path: " + relativePath));
            }
            catch (Exception ex)
            {
                VsShellUtilities.ShowMessageBox(
                    package,
                    ex.Message,
                    "Copy Relative Path",
                    OLEMSGICON.OLEMSGICON_WARNING,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
        }
    }
}
