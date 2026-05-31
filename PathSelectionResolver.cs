using Microsoft.VisualStudio.Shell;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SuperVs
{
    internal sealed class SelectionContext
    {
        public SelectionContext(string selectedPath, string selectedDirectory, string projectDirectory, string solutionDirectory, bool isProjectNode)
        {
            SelectedPath = selectedPath;
            SelectedDirectory = selectedDirectory;
            ProjectDirectory = projectDirectory;
            SolutionDirectory = solutionDirectory;
            IsProjectNode = isProjectNode;
        }

        public string SelectedPath { get; }

        public string SelectedDirectory { get; }

        public string ProjectDirectory { get; }

        public string SolutionDirectory { get; }

        public bool IsProjectNode { get; }

        public bool IsFile => !string.IsNullOrWhiteSpace(SelectedPath) && File.Exists(SelectedPath);

        public string RelativeRoot
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(SolutionDirectory) && Directory.Exists(SolutionDirectory))
                {
                    return SolutionDirectory;
                }

                return !string.IsNullOrWhiteSpace(ProjectDirectory) && Directory.Exists(ProjectDirectory)
                    ? ProjectDirectory
                    : null;
            }
        }

        public string TerminalDirectory
        {
            get
            {
                if (IsProjectNode && !string.IsNullOrWhiteSpace(SelectedDirectory))
                {
                    return EnsureTrailingSlash(SelectedDirectory);
                }

                return EnsureTrailingSlash(RelativeRoot ?? SelectedDirectory);
            }
        }

        public string InitialDirectory => SelectedDirectory;

        public string GetRelativePath()
        {
            string root = RelativeRoot;
            if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(SelectedPath))
            {
                return null;
            }

            return GetRelativePath(root, SelectedPath);
        }

        private static string EnsureTrailingSlash(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }

        private static string GetRelativePath(string root, string path)
        {
            Uri rootUri = new Uri(EnsureTrailingSlash(Path.GetFullPath(root)));
            Uri pathUri = new Uri(Path.GetFullPath(path));

            if (!string.Equals(rootUri.Scheme, pathUri.Scheme, StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }

            string relative = Uri.UnescapeDataString(rootUri.MakeRelativeUri(pathUri).ToString());
            return relative.Replace('/', Path.DirectorySeparatorChar);
        }
    }

    internal static class PathSelectionResolver
    {
        public static SelectionContext GetSelectionContext(IAsyncServiceProvider serviceProvider)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            EnvDTE80.DTE2 dte = ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                return await serviceProvider.GetServiceAsync(typeof(EnvDTE.DTE)) as EnvDTE80.DTE2;
            });

            if (dte == null || dte.SelectedItems == null || dte.SelectedItems.Count == 0)
            {
                return null;
            }

            string solutionDirectory = GetSolutionDirectory(dte);

            foreach (EnvDTE.SelectedItem selectedItem in dte.SelectedItems)
            {
                string selectedPath = GetSelectedPath(selectedItem);
                if (string.IsNullOrWhiteSpace(selectedPath))
                {
                    continue;
                }

                string selectedDirectory = GetSelectedDirectory(selectedPath);
                if (string.IsNullOrWhiteSpace(selectedDirectory))
                {
                    continue;
                }

                string projectDirectory = selectedItem.ProjectItem != null
                    ? GetProjectDirectory(selectedItem.ProjectItem.ContainingProject)
                    : GetProjectDirectory(selectedItem.Project);

                bool isProjectNode = selectedItem.Project != null && selectedItem.ProjectItem == null;
                return new SelectionContext(selectedPath, selectedDirectory, projectDirectory, solutionDirectory, isProjectNode);
            }

            return null;
        }

        private static string GetSelectedPath(EnvDTE.SelectedItem selectedItem)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (selectedItem.ProjectItem != null)
            {
                return GetProjectItemPath(selectedItem.ProjectItem);
            }

            if (selectedItem.Project != null)
            {
                return GetProjectDirectory(selectedItem.Project);
            }

            return null;
        }

        private static string GetSelectedDirectory(string selectedPath)
        {
            if (Directory.Exists(selectedPath))
            {
                return selectedPath;
            }

            return File.Exists(selectedPath) ? Path.GetDirectoryName(selectedPath) : null;
        }

        private static string GetProjectItemPath(EnvDTE.ProjectItem projectItem)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                string fileName = projectItem.FileNames[1];
                if (!string.IsNullOrWhiteSpace(fileName))
                {
                    return fileName;
                }
            }
            catch (ArgumentException)
            {
            }
            catch (COMException)
            {
            }

            return GetPropertyValue(projectItem.Properties, "FullPath");
        }

        private static string GetProjectDirectory(EnvDTE.Project project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (project == null)
            {
                return null;
            }

            string fullPath = GetPropertyValue(project.Properties, "FullPath");
            if (!string.IsNullOrWhiteSpace(fullPath) && Directory.Exists(fullPath))
            {
                return fullPath;
            }

            if (!string.IsNullOrWhiteSpace(project.FullName))
            {
                string projectFileDirectory = Path.GetDirectoryName(project.FullName);
                if (!string.IsNullOrWhiteSpace(projectFileDirectory) && Directory.Exists(projectFileDirectory))
                {
                    return projectFileDirectory;
                }
            }

            return null;
        }

        private static string GetSolutionDirectory(EnvDTE80.DTE2 dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string solutionPath = dte.Solution?.FullName;
            if (string.IsNullOrWhiteSpace(solutionPath))
            {
                return null;
            }

            string solutionDirectory = Path.GetDirectoryName(solutionPath);
            return Directory.Exists(solutionDirectory) ? solutionDirectory : null;
        }

        private static string GetPropertyValue(EnvDTE.Properties properties, string name)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                return properties?.Item(name)?.Value?.ToString();
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (COMException)
            {
                return null;
            }
        }
    }
}
