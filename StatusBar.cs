using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using Task = System.Threading.Tasks.Task;

namespace SuperVs
{
    internal static class StatusBar
    {
        public static async Task SetTextAsync(AsyncPackage package, string message)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            var statusBar = await package.GetServiceAsync(typeof(SVsStatusbar)) as IVsStatusbar;
            if (statusBar != null)
            {
                statusBar.SetText(message);
            }
        }
    }
}
