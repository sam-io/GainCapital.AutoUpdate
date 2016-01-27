using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using GainCapital.AutoUpdate;
using Topshelf;

namespace TestApp
{
	class ServiceWorker : ServiceControl
	{
		public bool Start(HostControl hostControl)
		{
			_host = hostControl;
			_updater = new UpdateChecker(_host,
				new UpdatingInfo
				{
					NugetAppName = Program.AppName,
					ServiceName = Program.AppName,
				});
			_updater.Start();

			return true;
		}

		public bool Stop(HostControl hostControl)
		{
			_updater.Stop();

			_host = null;
			_updater = null;

			return true;
		}

		private HostControl _host;
		private UpdateChecker _updater;
	}
}
