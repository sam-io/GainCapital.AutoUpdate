using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Topshelf;

namespace GainCapital.AutoUpdate.DebugProject
{
	class ServiceWorker : ServiceControl
	{
	    private Process _updateProcess;

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GenerateConsoleCtrlEvent(ConsoleCtrlEvent sigevent, int dwProcessGroupId);
        public enum ConsoleCtrlEvent
        {
            CTRL_C = 0,
            CTRL_BREAK = 1,
            CTRL_CLOSE = 2,
            CTRL_LOGOFF = 5,
            CTRL_SHUTDOWN = 6
        }

		public bool Start(HostControl hostControl)
		{
		    try
		    {

		        _host = hostControl;
		        var processInfo = new ProcessStartInfo("Updater.exe") { UseShellExecute = false };
		        Console.WriteLine("Starting {0}", processInfo.FileName);
                _updateProcess = Process.Start(processInfo);

		    }
		    catch (Exception ex)
		    {
		        Console.WriteLine(ex);
		    }
		    
			return true;
		}

		public bool Stop(HostControl hostControl)
		{
		    try
		    {
		        if (!_updateProcess.WaitForExit(1))
		        {
                    Console.WriteLine("Sending CTRL+C to updater...");
		            GenerateConsoleCtrlEvent(ConsoleCtrlEvent.CTRL_C, 0);
		            if (!_updateProcess.WaitForExit(5000))
		            {
		                Console.WriteLine("Not shut down, killing...");
		                _updateProcess.Kill();
                        Thread.Sleep(5000);
		            }
		        }
		    }
		    catch
		    {
		    }
            
			_host = null;		
			return true;
		}

		private HostControl _host;
		
	}
}
