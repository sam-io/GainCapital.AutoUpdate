using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Topshelf;

namespace DebugProject
{
	class ServiceWorker : ServiceControl
	{
		public bool Start(HostControl hostControl)
		{
			_host = hostControl;
			return true;
		}

		public bool Stop(HostControl hostControl)
		{
			_host = null;
			return true;
		}

		HostControl _host;
	}
}
