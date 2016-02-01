using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GainCapital.AutoUpdate
{
	static class Settings
	{
		public static string NugetServerUrl
		{
			get { return Get("NugetServerUrl", "InternalNugetServerUrl"); }
		}

		public static TimeSpan UpdateCheckingPeriod
		{
			get
			{
				var tmp = Get("UpdateCheckingPeriod");
				if (string.IsNullOrEmpty(tmp))
					return TimeSpan.FromMinutes(5);
				var res = TimeSpan.Parse(tmp);
				return res;
			}
		}

		public static PackageLevel UpdatePackageLevel
		{
			get
			{
				var resText = Get("UpdatePackageLevel");
				if (string.IsNullOrEmpty(resText))
				{
					resText = Get("IsPreProductionEnvironment");
					if (string.IsNullOrEmpty(resText))
						return PackageLevel.Release;
					var res = bool.Parse(resText.ToLowerInvariant());
					return res ? PackageLevel.RC : PackageLevel.Release;
				}
				else
				{
					var res = (PackageLevel)Enum.Parse(typeof (PackageLevel), resText, true);
					return res;
				}
			}
		}

		static string Get(string name, string alternativeName = null)
		{
			var res = ConfigurationManager.AppSettings[name];
			if (string.IsNullOrEmpty(res))
				res = Environment.GetEnvironmentVariable(name);

			if (string.IsNullOrEmpty(res) && !string.IsNullOrEmpty(alternativeName))
				res = Get(alternativeName);

			return res;
		}
	}

	public enum PackageLevel { Beta, RC, Release }
}
