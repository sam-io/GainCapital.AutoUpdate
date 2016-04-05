using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Topshelf;

namespace GainCapital.AutoUpdate.DebugProject
{
	class ServiceWorker : ServiceControl
	{
	    private Process _updateProcess;
		public bool Start(HostControl hostControl)
		{
		    try
		    {

		        _host = hostControl;
		        var processInfo = new ProcessStartInfo("GainCapital.Updater.exe") { UseShellExecute = false };
		        /*var variables = Environment.GetEnvironmentVariables();
		        foreach (var key in variables.Keys.Cast<string>().Where(k => !processInfo.EnvironmentVariables.ContainsKey(k)))
		        {
		            processInfo.EnvironmentVariables.Add(key, (string) variables[key]);
                    Console.WriteLine("Adding env {0}", key);
		        }*/
                Console.WriteLine("Starting {0}", processInfo.FileName);
                _updateProcess = Process.Start(processInfo);

		    }
		    catch (Exception ex)
		    {
		        Console.WriteLine(ex);
		    }
		    /*_updater = new UpdateChecker(_host,
				new UpdatingInfo
				{
					NugetAppName = Program.AppName,
					ServiceName = Program.AppName,
				});
			_updater.Start();*/

			return true;
		}

		public bool Stop(HostControl hostControl)
		{
            _updateProcess.Kill();
			_host = null;		
			return true;
		}

		private HostControl _host;
		
	}
}
