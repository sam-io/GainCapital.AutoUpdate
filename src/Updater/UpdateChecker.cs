﻿using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using NuGet;

namespace GainCapital.AutoUpdate.Updater
{
	public class UpdateChecker
	{
	    private UpdatePackages _updatePackages;
        private readonly ParentProcess _parentProcess;
	    private CancellationToken _canelToken;

        private readonly Thread _updateThread;

        private string _appPath;
        private string _appParentPath;
        private string _updateDataPath;

        public event EventHandler Shutdown;
	    
		public UpdateChecker(ParentProcess parentProcess)
		{
		    _parentProcess = parentProcess;
		    
			_updateThread = new Thread(CheckForUpdates)
			{
				Name = "AutoUpdateThread",
				IsBackground = true,
			};
		}

		public void Start(CancellationToken canelToken)
		{
		    _canelToken = canelToken;
		    _appPath = _parentProcess.Location;

            if (!JunctionPoint.Exists(_appPath))
			{
				Logger.LogError(string.Format("Invalid app folder structure: \"{0}\". Turned off auto updates.", _appPath));
                OnShutdown(EventArgs.Empty);
			    return;
			}

		    _appParentPath = Path.GetDirectoryName(_appPath);
			_updateDataPath = Path.Combine(_appParentPath, "UpdateData");

            Logger.LogInfo(String.Format("Going to check {0} for update to {1}, current version is {2}", Settings.NugetServerUrl, _appPath, _parentProcess.Version));

            _updatePackages = new UpdatePackages(Settings.NugetServerUrl);

            Logger.LogInfo("Starting update thread");
			_updateThread.Start();            
		}

		private void CheckForUpdates()
		{
            while (!_canelToken.WaitHandle.WaitOne(Settings.UpdateCheckingPeriod))
			{
				try
				{
				    if (!_parentProcess.IsRunning)
				    {
				        Logger.LogInfo("Parent process is no longer running, will shut down.");
				        OnShutdown(EventArgs.Empty);
				    }
				    else
				    {
                        if(string.IsNullOrEmpty(DeploymentEnvironment.EnvironmentName))
                            Logger.LogError("EnvironmentName is undefined, auto update will be skipped.");

                        if (string.IsNullOrEmpty(DeploymentEnvironment.EnvironmentType))
                            Logger.LogError("EnvironmentType is undefined, auto update will be skipped.");

                        if(!string.IsNullOrEmpty(DeploymentEnvironment.EnvironmentName) &&
                           !string.IsNullOrEmpty(DeploymentEnvironment.EnvironmentType))
				        {
				            CheckUpdatesOnce();    
				        }
				    }
				}
				catch (ThreadInterruptedException)
				{
					break;
				}
				catch (ThreadAbortException)
				{
					break;
				}		
				catch (Exception ex)
				{
                    Logger.LogError(ex.ToString());
				}
			}
		}

		private void CheckUpdatesOnce()
		{
            Logger.LogInfo("Checking for updates");

		    if (Directory.Exists(_updateDataPath))
                FileSystem.FromDirectory(_updateDataPath)
                    .IncludeFilesRecursive("*.*")
                    .IncludeDirectoriesRecursive("*")
                    .Exclude(f => Path.GetExtension(f).Equals(".log", StringComparison.OrdinalIgnoreCase))                                        
                    .DeleteAll();
            
            Logger.LogInfo(string.Format("Update url is {0}", Settings.NugetServerUrl));

            if (string.IsNullOrEmpty(Settings.NugetServerUrl))
				return;
             
			var lastPackage = _updatePackages.GetLastPackage(_parentProcess.Name, Settings.UpdatePackageLevel);
		    if (lastPackage.Version.Version > _parentProcess.Version)
		    {
                Logger.LogInfo(string.Format("New version {0} found, current version is {1}, installing...", lastPackage.Version.Version, _parentProcess.Version));
                InstallUpdate(lastPackage);
		    }		           
		}

       
		private void InstallUpdate(IPackage package)
		{
	        var updatePackage = _updatePackages.Download(package, _appParentPath);

            Logger.LogInfo(string.Format("Updating from {0}.", updatePackage.UpdateLocation));

            var args = string.Format("\"{0}\" \"{1}\" {2}",
                            updatePackage.UpdateLocation,
                            updatePackage.ChangeRequestNumber,
                            _parentProcess.ToCommandLine());

            Process.Start(new ProcessStartInfo
                {
                    FileName = Assembly.GetExecutingAssembly().Location,
                    Arguments = args,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });

            File.WriteAllText(Path.Combine(updatePackage.UpdateLocation, "RollbackInfo.txt"), _parentProcess.Version.ToString());
            
            Logger.LogInfo("Shutting down parent process");
		    _parentProcess.ShutDown();
            
            OnShutdown(EventArgs.Empty);
		}

	    private void OnShutdown(EventArgs args)
	    {
	        var handler = Shutdown;
            if(handler!=null)
                handler(this, args);
	    }
       
    }
}
