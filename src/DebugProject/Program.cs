using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using log4net.Config;
using Topshelf;

namespace GainCapital.AutoUpdate.DebugProject
{
	class Program
	{
		static int Main(string[] args)
		{
            XmlConfigurator.Configure();

			var exitCode = HostFactory.Run(x =>
			{
				x.Service(settings => new ServiceWorker());
				x.RunAsLocalSystem();
				x.StartAutomatically();

				x.SetDescription(AppName);
				x.SetDisplayName(AppName);
				x.SetServiceName(AppName);

				x.EnableServiceRecovery(rc =>
				{
					rc.RestartService(1); // restart the service after 1 minute
				});

				x.UseLog4Net();
			});

			if (Debugger.IsAttached)
				Debugger.Break();

			return (int)exitCode;
		}

		public const string AppName = "GainCapital.AutoUpdate.DebugProject";
	}
}
