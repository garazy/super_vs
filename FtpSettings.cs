using System;
using System.IO;

namespace SuperVs
{
    internal sealed class FtpSettings
    {
        private const string PreferredSettingsFileName = "ftp.settings";
        private const string LegacySettingsFileName = "ftp.ext.settings";

        private FtpSettings(string settingsPath, string rootDirectory, string server, string username, string password, string remoteBaseFolder)
        {
            SettingsPath = settingsPath;
            RootDirectory = rootDirectory;
            Server = server;
            Username = username;
            Password = password;
            RemoteBaseFolder = remoteBaseFolder;
        }

        public string SettingsPath { get; }

        public string RootDirectory { get; }

        public string Server { get; }

        public string Username { get; }

        public string Password { get; }

        public string RemoteBaseFolder { get; }

        public static FtpSettings FindForFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return null;
            }

            DirectoryInfo directory = new FileInfo(filePath).Directory;
            while (directory != null)
            {
                FtpSettings settings = TryLoad(Path.Combine(directory.FullName, PreferredSettingsFileName), directory.FullName)
                    ?? TryLoad(Path.Combine(directory.FullName, LegacySettingsFileName), directory.FullName);

                if (settings != null)
                {
                    return settings;
                }

                directory = directory.Parent;
            }

            return null;
        }

        private static FtpSettings TryLoad(string settingsPath, string rootDirectory)
        {
            if (!File.Exists(settingsPath))
            {
                return null;
            }

            string[] lines = File.ReadAllLines(settingsPath);
            if (lines.Length < 4)
            {
                throw new InvalidDataException(Path.GetFileName(settingsPath) + " must contain server, username, password, and remote base folder on the first four lines.");
            }

            string server = lines[0].Trim();
            string username = lines[1].Trim();
            string password = lines[2];
            string remoteBaseFolder = lines[3].Trim();

            if (string.IsNullOrWhiteSpace(server))
            {
                throw new InvalidDataException("FTP server is missing in " + settingsPath + ".");
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                throw new InvalidDataException("FTP username is missing in " + settingsPath + ".");
            }

            if (string.IsNullOrWhiteSpace(remoteBaseFolder))
            {
                remoteBaseFolder = "/";
            }

            return new FtpSettings(settingsPath, rootDirectory, server, username, password, remoteBaseFolder);
        }
    }
}
