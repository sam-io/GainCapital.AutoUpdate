using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace GainCapital.AutoUpdate
{
	public static class FileUtil
	{
		public static void ResetAttributes(string fileName, FileAttributes attr)
		{
			if (HasAttributes(fileName, attr))
				File.SetAttributes(fileName, File.GetAttributes(fileName) & ~attr);
		}

		public static bool HasAttributes(string fileName, FileAttributes attr)
		{
			return ((File.GetAttributes(fileName) & attr) == attr);
		}

		public static void Cleanup(string path, string wildcard, string exclude, bool removeThisFolder, bool recursively)
		{
			exclude = exclude.ToLowerInvariant();

			foreach (var file in Directory.GetFiles(path, wildcard))
			{
				if (Path.GetExtension(file).ToLowerInvariant() == exclude)
					continue;
				ResetAttributes(file, FileAttributes.ReadOnly);
				File.Delete(file);
			}

			if (recursively)
			{
				foreach (var directory in Directory.GetDirectories(path))
				{
					Cleanup(directory, wildcard, exclude, true, true);
				}
			}

			if (removeThisFolder && Directory.GetFileSystemEntries(path).Length == 0)
				Directory.Delete(path);
		}
	}
}
