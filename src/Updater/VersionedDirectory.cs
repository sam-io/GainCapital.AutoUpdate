using System.IO;

namespace GainCapital.AutoUpdate.Updater
{
    public class VersionedDirectory
    {
        public static void SetCurrent(string sourcePath, string targetPath)
        {
            if (Directory.Exists(targetPath))
                Directory.Delete(targetPath);

            JunctionPoint.Create(targetPath, sourcePath);

            UpdateVersionMarkerFile(Path.GetDirectoryName(targetPath), Path.GetFileName(sourcePath).TrimStart('v'));            
        }

        private static void UpdateVersionMarkerFile(string path, string version)
        {
            FileSystem.FromDirectory(path)
                .IncludeFiles("current_is_*")
                .DeleteAll();

            var newVersionFile = string.Format("current_is_{0}", version);
            var newVersionFilePath = Path.Combine(path, newVersionFile);
            File.WriteAllText(newVersionFilePath, string.Format("\"{0}\"\r\n", version));
        }		 
    }
}