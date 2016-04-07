using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GainCapital.AutoUpdate.Updater
{
    public class FileSystem
    {
        private readonly IEnumerable<FileSystemInfo> _allFiles;
        private readonly string _rootDirectory;

        private FileSystem(IEnumerable<FileSystemInfo> allFiles, string rootDirectory)
        {
            _allFiles = allFiles;
            _rootDirectory = rootDirectory;
        }

        public static FileSystem FromDirectory(string directory)
        {
            return new FileSystem(Enumerable.Empty<FileSystemInfo>(), directory);
        }

        public FileSystem IncludeFiles(string searchPattern)
        {
            return new FileSystem(_allFiles.Union(Directory.GetFiles(_rootDirectory, searchPattern).Select(d => new FileInfo(d))), _rootDirectory);
        }

        public FileSystem IncludeFilesRecursive(string searchPattern)
        {
            return new FileSystem(_allFiles.Union(Directory.GetFiles(_rootDirectory, searchPattern, SearchOption.AllDirectories).Select(d => new FileInfo(d))), _rootDirectory);
        }

        public FileSystem IncludeDirectoriesRecursive(string searchPattern)
        {
            return new FileSystem(_allFiles.Union(Directory.GetDirectories(_rootDirectory, searchPattern, SearchOption.AllDirectories).Select(d => new DirectoryInfo(d))), _rootDirectory);
        }

        public FileSystem Exclude(Func<string, bool> exclude)
        {
            return new FileSystem(_allFiles.Where(f => !exclude(f.FullName)), _rootDirectory);
        }

        public void DeleteAll()
        {
            foreach (var file in _allFiles.OfType<FileInfo>())
            {
                file.Delete();
            }

            foreach (var directory in _allFiles.OfType<DirectoryInfo>().OrderByDescending(d => d.FullName))
            {                
                directory.Delete();
            }
        }

        public void Flatten(string destination)
        {
            Copy(destination, fullName => Path.Combine(destination, Path.GetFileName(fullName)));
        }

        public void CopyTo(string destination)
        {
            Copy(destination, fullName => Path.Combine(destination, fullName.Substring(_rootDirectory.Length + 1)));
        }

        private void Copy(string destination, Func<string, string> getDestinationPath)
        {            
            if (!Directory.Exists(destination))
                Directory.CreateDirectory(destination);

            foreach (var file in _allFiles.OfType<FileInfo>())
            {
                var path = getDestinationPath(file.FullName);
                var parentDir = Path.GetDirectoryName(path);
                if (!Directory.Exists(parentDir))
                {
                    Directory.CreateDirectory(parentDir);
                    
                }                
                File.Copy(file.FullName, path);
            }

        }
    }	
}
