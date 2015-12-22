using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using log4net.Config;
using Topshelf;

namespace DebugProject
{
	class Program
	{
		static void Main(string[] args)
		{
			XmlConfigurator.Configure();

			HostFactory.Run(x =>
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
		}

		const string AppName = "DebugProject";
	}
}
