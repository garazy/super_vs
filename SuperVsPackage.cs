using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using Task = System.Threading.Tasks.Task;

namespace SuperVs
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(PackageGuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string, PackageAutoLoadFlags.BackgroundLoad)]
    public sealed class SuperVsPackage : AsyncPackage
    {
        public const string PackageGuidString = "4f0066d4-ef76-4fa1-9491-fd87f39d6b87";

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await base.InitializeAsync(cancellationToken, progress);

            _ = Task.Run(
                () =>
                {
                    WindowsTerminalLocator.GetWindowsTerminalPath();
                    try
                    {
                        WindowsTerminalProfileManager.EnsureDefaultProfiles();
                    }
                    catch
                    {
                    }
                },
                cancellationToken);

            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            await OpenTerminalCommand.InitializeAsync(this);
            await CopyRelativePathCommand.InitializeAsync(this);
            await ExecuteCmdCommand.InitializeAsync(this);
            await FtpUploadCommand.InitializeAsync(this);
        }
    }
}
