using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GainCapital.AutoUpdate
{
	public class UpdatingInfo
	{
		public string NugetAppName;
		public string NugetServerUrl;
		public bool IsPreProductionEnvironment;
		public TimeSpan UpdateCheckingPeriod;
		
		public string ServiceName;
		public string ExeName;

		public Action<string, string> Update;

		public void OnUpdate(string stagingPath, string targetPath)
		{
			if (Update != null)
				Update(stagingPath, targetPath);
		}
	}
}
