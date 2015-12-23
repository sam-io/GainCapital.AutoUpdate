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
					return TimeSpan.FromMinutes(1);
				var res = TimeSpan.Parse(tmp);
				return res;
			}
		}

		public static bool IsPreProductionEnvironment
		{
			get
			{
				var res = Get("IsPreProductionEnvironment");
				if (string.IsNullOrEmpty(res))
					return false;
				return bool.Parse(res);
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
}
