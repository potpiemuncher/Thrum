using System;
using System.IO;

namespace DS4Windows
{
    public enum FirstRunDataLocation
    {
        AppData,
        Portable,
    }

    internal interface IFirstRunDataLocationOperations
    {
        string AppDataPath { get; }
        string ExeDirectoryPath { get; }
        bool AdminNeeded();
        void SaveWhere(string path);
        void SaveDefault(string profilesXmlPath);
        bool DirectoryExists(string path);
        void DeleteDirectory(string path, bool recursive);
        void DeleteFile(string path);
        void ShowCannotDeleteOldSettings();
    }

    internal sealed class GlobalFirstRunDataLocationOperations :
        IFirstRunDataLocationOperations
    {
        public string AppDataPath => Global.appDataPpath;
        public string ExeDirectoryPath => Global.exedirpath;

        public bool AdminNeeded() => Global.AdminNeeded();
        public void SaveWhere(string path) => Global.SaveWhere(path);
        public void SaveDefault(string path) => Global.SaveDefault(path);
        public bool DirectoryExists(string path) => Directory.Exists(path);
        public void DeleteDirectory(string path, bool recursive) =>
            Directory.Delete(path, recursive);
        public void DeleteFile(string path) => File.Delete(path);

        public void ShowCannotDeleteOldSettings() =>
            System.Windows.MessageBox.Show(
                "Cannot Delete old settings, please manaully delete",
                ProductInfo.ProductName);
    }

    /// <summary>
    /// Routes the wizard's choice through the same Global calls, ordering and
    /// cleanup rules as SaveWhere. File operations are abstracted only so the
    /// routing can be pinned without touching a real configuration folder.
    /// </summary>
    internal sealed class FirstRunDataLocationRouter
    {
        private readonly IFirstRunDataLocationOperations operations;

        public FirstRunDataLocationRouter(
            IFirstRunDataLocationOperations operations)
        {
            this.operations = operations ??
                throw new ArgumentNullException(nameof(operations));
        }

        public bool PortableAllowed => !operations.AdminNeeded();

        public bool Apply(FirstRunDataLocation location, bool multipleSaveSpots,
            bool keepExistingSettings)
        {
            if (location == FirstRunDataLocation.Portable)
            {
                if (!PortableAllowed)
                {
                    return false;
                }

                ApplyPortable(multipleSaveSpots, keepExistingSettings);
                return true;
            }

            ApplyAppData(multipleSaveSpots, keepExistingSettings);
            return true;
        }

        private void ApplyPortable(bool multipleSaveSpots,
            bool keepExistingSettings)
        {
            operations.SaveWhere(operations.ExeDirectoryPath);
            if (multipleSaveSpots && !keepExistingSettings)
            {
                try
                {
                    if (operations.DirectoryExists(operations.AppDataPath))
                    {
                        operations.DeleteDirectory(operations.AppDataPath,
                            recursive: true);
                    }
                }
                catch
                {
                    // SaveWhere deliberately tolerates cleanup failures here.
                }
            }
            else if (!multipleSaveSpots)
            {
                operations.SaveDefault(Path.Combine(
                    operations.ExeDirectoryPath, "Profiles.xml"));
            }
        }

        private void ApplyAppData(bool multipleSaveSpots,
            bool keepExistingSettings)
        {
            if (multipleSaveSpots && !keepExistingSettings)
            {
                try
                {
                    operations.DeleteDirectory(Path.Combine(
                        operations.ExeDirectoryPath, "Profiles"),
                        recursive: true);
                    operations.DeleteFile(Path.Combine(
                        operations.ExeDirectoryPath, "Profiles.xml"));
                    operations.DeleteFile(Path.Combine(
                        operations.ExeDirectoryPath, "Auto Profiles.xml"));
                }
                catch (UnauthorizedAccessException)
                {
                    operations.ShowCannotDeleteOldSettings();
                }
            }
            else if (!multipleSaveSpots)
            {
                operations.SaveDefault(Path.Combine(
                    operations.AppDataPath, "Profiles.xml"));
            }

            operations.SaveWhere(operations.AppDataPath);
        }
    }
}
