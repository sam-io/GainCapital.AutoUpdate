using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace GainCapital.AutoUpdate.Updater
{
    public class ParentUpdater
    {
        public void CheckForUpdates()
        {
            try
            {             
                var parent = ParentProcess.GetParentProcess();
                
                var packageName = parent.ProcessName;

                var updaterChecker = new UpdateChecker(
                    parent,
                    new UpdatingInfo()
                    {
                        NugetAppName = packageName,
                        ServiceName = packageName,   
                        ExeName = Path.GetFileName(parent.MainModule.FileName),                        
                    });

                updaterChecker.Start();                
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex);
            }            
        }

        public void CheckForUpdates(int prcId)
        {
            var parent = Process.GetProcessById(prcId);
           
        }
    }

	static class Program
	{
		static void Main(string[] args)
		{
            try
			{
			    if (args.Length == 0)
			    {
                    var wait = new ManualResetEvent(false);
                    Console.CancelKeyPress += (sender, eventArgs) =>
                    {
                        LogInfo("Close event captured");
                        eventArgs.Cancel = true;
                        wait.Set();
                    };

			        new ParentUpdater().CheckForUpdates();

                    LogInfo("Waiting for close event...");
			        wait.WaitOne();
                    LogInfo("Shutdown");
                    return;
			    }
                
				if (args.Length != 5)
					throw new ApplicationException("Invalid args: " + Environment.CommandLine);

				LogInfo("Updating: " + Environment.CommandLine);

				var parentProcessId = int.Parse(args[0]);

				var appMode = (AppMode)Enum.Parse(typeof(AppMode), args[1], true);

				var startingName = args[2];
				var sourcePath = args[3];
				var targetPath = args[4];

				try
				{
					var parentProcess = Process.GetProcessById(parentProcessId);
					if (!parentProcess.WaitForExit(5 * 60 * 1000))
						throw new ApplicationException("Parent process didn't stop");
				}
				catch (ArgumentException)
				{
					// process already has stopped
				}

				if (Directory.Exists(targetPath))
					Directory.Delete(targetPath);
				JunctionPoint.Create(targetPath, sourcePath);
				Start(appMode, startingName, targetPath);

				LogInfo(parentProcessId + " - finished successfully");

			}
			catch (ApplicationException exc)
			{
				LogError(exc.Message);
			}
			catch (Exception exc)
			{
				LogError(exc.ToString());
			}
		}

		public static void Log(string message, string level)
		{
			Trace.WriteLine(message);
			Console.WriteLine(message);

		    try
		    {
		        var updataDir = Path.Combine(Directory.GetCurrentDirectory(), @"..\UpdateData\");
		        if (!Directory.Exists(updataDir))
		            Directory.CreateDirectory(updataDir);

		        var path = Path.Combine(updataDir, "GainCapital.AutoUpdate.log");
		        
		        var line = string.Format("{{\"timestamp\":\"{0}\", \"level\":\"{1}\", \"Message\":\"{2}\"}}",
		            DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ"), EscapeJsonVal(level), EscapeJsonVal(message));

                for(var i=1;i<=10;i++)
		        {
		            try
		            {
		                File.AppendAllLines(path, new string[] {line});
		                break;
		            }
		            catch (IOException)
		            {
		                Thread.Sleep(i);
		            }
		        }
			}
			catch (Exception exc)
			{
				Trace.WriteLine(exc.ToString());
				Console.WriteLine(exc.ToString());
			}
		}

		static void LogError(string message)
		{
			Log(message, "ERROR");
		}

		static void LogInfo(string message)
		{
			Log(message, "INFO");
		}

		static string EscapeJsonVal(string val)
		{
			return HttpUtility.JavaScriptStringEncode(val);
		}

		static void Start(AppMode appMode, string startingName, string targetPath)
		{
			switch (appMode)
			{
				case AppMode.Console:                    
					var processFilePath = Path.Combine(targetPath, startingName);
                    LogInfo(string.Format("Starting process {0}", processFilePath));
					Process.Start(processFilePath, "");
					break;
				case AppMode.Service:
			        LogInfo(string.Format("Starting service {0}", startingName));
					var service = new ServiceController(startingName);
					service.Start();
                    LogInfo(string.Format("Waiting for service {0} to start...", startingName));
                    service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
					break;
				default:
					throw new ArgumentOutOfRangeException("appMode", appMode, null);
			}
		}
		
	}
}
