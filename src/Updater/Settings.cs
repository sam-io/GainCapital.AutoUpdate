using System;
using System.Configuration;

namespace GainCapital.AutoUpdate.Updater
{
	public static class Settings
	{
		public static string NugetServerUrl
		{
		    get
		    {
                var result = Get("NugetServerUrl");
		        if (string.IsNullOrEmpty(result))
		            result = "http://internal.nuget.cityindex.co.uk/api/v2";
		        return result;
		    }
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
				var result = Get("UpdatePackageLevel");
                if (string.IsNullOrEmpty(result))
                {
                    var environmentType = Get("EnvironmentType");
                    if (!string.IsNullOrEmpty(environmentType))
                    {
                        if (environmentType.Equals("LIVE"))
                            return PackageLevel.Release;
                        if (environmentType.Equals("PPE"))
                            return PackageLevel.RC;
                    }
                    return PackageLevel.Beta;
				}
				else
				{					
					return (PackageLevel)Enum.Parse(typeof(PackageLevel), result, true);;
				}
			}
		}

		static string Get(string name)
		{
			var res = ConfigurationManager.AppSettings[name];
			if (string.IsNullOrEmpty(res))
				res = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Machine);


			return res;
		}
	}

	public enum PackageLevel { Beta, RC, Release }
}
