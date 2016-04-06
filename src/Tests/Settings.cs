using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Win32;

namespace GainCapital.AutoUpdate.Tests
{
	static class Settings
	{
		public const string KlondikeUrl = "https://github.com/themotleyfool/Klondike/releases/download/v2.0.0/Klondike.2.0.0.26c3df25-build144.zip";
		public const string KlondikeStarArgs = "--port=49361";

		public const string NugetUrl = "http://localhost:49361/api/";
		public const string UpdatePackageLevel = "Beta";
		public const string UpdateCheckingPeriod = "0:0:1";

		public static string MsbuildPath
		{
			get
			{
				var res = TryGetRegistryVal(@"SOFTWARE\Microsoft\MSBuild\ToolsVersions\14.0", "MSBuildToolsPath");
				if (res == null)
					res = TryGetRegistryVal(@"SOFTWARE\Microsoft\MSBuild\ToolsVersions\4.0", "MSBuildToolsPath");
				if (res == null)
					throw new ApplicationException("MSBuild path is not found");
				return res;
			}
		}

		static string TryGetRegistryVal(string path, string valName)
		{
			var key = Registry.LocalMachine.OpenSubKey(path);
			if (key == null)
				return null;
			return (string)key.GetValue(valName);
		}
	}
}
