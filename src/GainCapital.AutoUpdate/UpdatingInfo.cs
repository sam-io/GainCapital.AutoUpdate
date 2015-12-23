using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GainCapital.AutoUpdate
{
	public class UpdatingInfo
	{
		public string NugetAppName;
		public string NugetServerUrl;

		public bool IsPreProductionEnvironment
		{
			get { return _isPreProductionEnvironment; }
			set
			{
				_isPreProductionEnvironment = value;
				_isPreProductionEnvironmentInitialized = true;
			}
		}

		private bool _isPreProductionEnvironment;
		private bool _isPreProductionEnvironmentInitialized;

		public TimeSpan UpdateCheckingPeriod;
		
		public string ServiceName;
		public string ExeName { get { return Process.GetCurrentProcess().MainModule.FileName; } }

		public Action<string, string> Update;

		public void OnUpdate(string stagingPath, string targetPath)
		{
			if (Update != null)
				Update(stagingPath, targetPath);
		}

		public void Prepare()
		{
			if (string.IsNullOrEmpty(NugetAppName) || string.IsNullOrEmpty(ServiceName) || string.IsNullOrEmpty(ExeName))
				throw new ApplicationException("Invalid updating info");

			if (string.IsNullOrEmpty(NugetServerUrl))
				NugetServerUrl = Settings.NugetServerUrl;

			if (UpdateCheckingPeriod.Ticks == 0)
				UpdateCheckingPeriod = Settings.UpdateCheckingPeriod;

			if (!_isPreProductionEnvironmentInitialized)
				_isPreProductionEnvironment = Settings.IsPreProductionEnvironment;
		}
	}
}
