using System;
using System.Threading;

namespace GainCapital.AutoUpdate.Updater
{
    static class Program
	{
		static void Main(string[] args)
		{
            try
			{
			    if (args.Length == 0)
			    {
			        var cancel = new CancellationTokenSource();
			        var cancelToken = cancel.Token;

			        Console.CancelKeyPress += (sender, eventArgs) =>
			        {
			            Logger.LogInfo("Close event captured");
			            eventArgs.Cancel = true;
			            cancel.Cancel();
			        };
			        
			        var updaterChecker = new UpdateChecker(ParentProcess.GetParentProcess());

			        updaterChecker.Shutdown += (sender, eventArgs) => cancel.Cancel();
			        updaterChecker.Start(cancelToken);
			        
			        cancelToken.WaitHandle.WaitOne();
			    }
			    else
			    {
                    Logger.LogInfo("Updating: " + Environment.CommandLine);

			        var sourcePath = args[0];
			        var changeRequestNumber = args[1];
                    var parentProcess = ParentProcess.FromCommandLine(args, 2);
			        if (!parentProcess.WaitForExit())
			            throw new ApplicationException("Parent process didn't stop");
                    
			        VersionedDirectory.SetCurrent(sourcePath, parentProcess.Location);
                    EdbUpdate.UpdateEdb(parentProcess.Name, changeRequestNumber);

                    parentProcess.Start();
                    Logger.LogInfo(parentProcess.ProcessId + " - finished successfully");
			    }                
			}
			catch (Exception ex)
			{
                Logger.LogError(ex.ToString());
			}
		}
	}
}
