using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;



using GainCapital.AutoUpdate.Updater;
using NuGet;
using ThreadState = System.Threading.ThreadState;

namespace GainCapital.AutoUpdate
{
	public class UpdateChecker
	{
		public UpdateChecker(Process parent, UpdatingInfo info)
		{
		    _parent = parent;
		    _info = info;

			info.Prepare();

			_thread = new Thread(CheckForUpdates)
			{
				Name = "AutoUpdateThread",
				IsBackground = true,
			};
		}

		public void Start()
		{
			_appPath = Path.GetDirectoryName(_parent.MainModule.FileName);

			if (!JunctionPoint.Exists(_appPath))
			{
				LogError(string.Format("Invalid app folder structure: \"{0}\". Turned off auto updates.", _appPath));
				return;
			}

		    _curVersion = new Version(_parent.MainModule.FileVersionInfo.FileVersion);

			_appParentPath = Path.GetDirectoryName(_appPath);
			_updateDataPath = Path.Combine(_appParentPath, "UpdateData");

		    LogInfo(String.Format("Going to check {0} for update to {1}, current version is {2}", UpdateUrl, _appPath, _curVersion));

			_repository = PackageRepositoryFactory.Default.CreateRepository(UpdateUrl);
            
            LogInfo("Starting check thread");
			_thread.Start();

			
		}

		public void Stop()
		{
            LogInfo("Stopping check thead");
			
			_terminationEvent.Set();
			Thread.Sleep(1);

			_thread.Interrupt();
			if (_thread.ThreadState == ThreadState.Running)
				_thread.Join(TimeSpan.FromSeconds(10));
			
		}


        static void LogInfo(string message)
        {
            Program.Log(message, "INFO");
        }

        static void LogError(string message)
        {
            Program.Log(message, "ERROR");
        }

		void CheckForUpdates()
		{
			while (true)
			{
				try
				{
                    LogInfo("Checking updates");
					CheckUpdatesOnce();
				}
				catch (ThreadInterruptedException)
				{
					break;
				}
				catch (ThreadAbortException)
				{
					break;
				}
				catch (ApplicationException exc)
				{
					LogError(exc.Message);
				}
				catch (InvalidOperationException exc)
				{
					if (!exc.Message.StartsWith("Unable to find version"))
						LogError(exc.ToString());
				}
				catch (Exception exc)
				{
					LogError(exc.ToString());
				}

				if (_terminationEvent.WaitOne(_info.UpdateCheckingPeriod))
					break;
			}
		}

		private void CheckUpdatesOnce()
		{
		   if (Directory.Exists(_updateDataPath))
				FileUtil.Cleanup(_updateDataPath, "*.*", ".log", false, true);

		    LogInfo(string.Format("Update url is {0}", UpdateUrl));

			if (string.IsNullOrEmpty(UpdateUrl))
				return;

			var lastPackage = GetLastPackage(PackageId);
			var updateVersion = lastPackage.Version.Version;

			if (updateVersion <= _curVersion)
				return;

            LogInfo(string.Format("New version {0} found, current version is {1}, installing...", updateVersion, _curVersion));

			InstallUpdate(lastPackage);
		}

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

		private void InstallUpdate(IPackage package)
		{
			var packageManager = new PackageManager(_repository, _updateDataPath);
			packageManager.InstallPackage(PackageId, package.Version, true, false);

			var packagePath = Path.Combine(_updateDataPath, PackageId + "." + package.Version);
			var updateDeploymentPath = Path.Combine(_appParentPath, "v" + package.Version);
			var packageBinPath = Path.Combine(packagePath, "lib");

            Copy(packageBinPath, updateDeploymentPath, UpdateFileTypes);

			if (_info.CopyContent)
				Copy(Path.Combine(packagePath, "content"), updateDeploymentPath, new[] { "*.*" });

			Copy(_appPath, updateDeploymentPath, new[] { "*.log" });

			_info.OnUpdate(packagePath, updateDeploymentPath);

			UpdateVersionMarkerFile(package.Version.ToString());

			
            var updatedCurrentPath = Path.Combine(_appParentPath, "current");

            LogInfo(String.Format("Updating from {0}.", packagePath));
		    LogInfo("Shutting down parent process");

		    var serviceName = GetService(_parent.Id);
            var appMode = serviceName == null ? AppMode.Console : AppMode.Service;

            var args = string.Format("{0} {1} \"{2}\" \"{3}\" \"{4}\"", _parent.Id, appMode,
                EscapeCommandLineArg(appMode == AppMode.Console ? _info.ExeName : serviceName), EscapeCommandLineArg(updateDeploymentPath),
                EscapeCommandLineArg(updatedCurrentPath));

            Process.Start(new ProcessStartInfo
            {
                FileName = Assembly.GetExecutingAssembly().Location,
                Arguments = args,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });

		    if (appMode == AppMode.Console)
		    {
                LogInfo("Process is console app, sending CTRL+C");
		        GenerateConsoleCtrlEvent(ConsoleCtrlEvent.CTRL_C, 0);
		    }
		    else
		    {
                LogInfo(string.Format("Process is windows service, shutting down {0}", serviceName));
                StopService(serviceName);
		    }

            Environment.Exit(0);
		}

        public static void StopService(string name)
        {
            var controller = new ServiceController(name);
            controller.Stop();
        }

        public static string GetService(int processId)
        {
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_Service WHERE ProcessId =" + "\"" + processId + "\""))
            {
                foreach (ManagementObject service in searcher.Get())
                    return (string)service["Name"];
            }
            return null;
        }

		private IPackage GetLastPackage(string packageId)
		{
            LogInfo(string.Format("Getting packages for {0}", packageId));
			var packages = _repository.FindPackagesById(packageId).ToList();
            
            LogInfo(string.Format("found {0} packages for {1}", packages.Count, packageId));

			packages.RemoveAll(val => !val.IsListed());

			if (_info.UpdatePackageLevel > PackageLevel.Beta)
				packages.RemoveAll(val => val.Version.SpecialVersion.ToLowerInvariant() == "beta");

			if (_info.UpdatePackageLevel > PackageLevel.RC)
				packages.RemoveAll(val => val.Version.SpecialVersion.ToLowerInvariant() == "rc");

			if (packages.Count == 0)
				throw new ApplicationException("No update package is found");
			packages.Sort((x, y) => x.Version.CompareTo(y.Version));
			var res = packages.Last();
            LogInfo(string.Format("Latest packafge is {0}", res.Version));

			return res;
		}


		void UpdateVersionMarkerFile(string version)
		{
			var currentVersionMarkerFiles = Directory.GetFiles(_appParentPath, "current_is_*");
			foreach (var file in currentVersionMarkerFiles)
			{
				var attributes = File.GetAttributes(file);
				if (attributes.HasFlag(FileAttributes.ReadOnly))
					File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);

				File.Delete(file);
			}

			var newVersionFile = string.Format("current_is_{0}", version);
			var newVersionFilePath = Path.Combine(_appParentPath, newVersionFile);
			File.WriteAllText(newVersionFilePath, string.Format("\"{0}\"\r\n", version));
		}

		static string EscapeCommandLineArg(string val)
		{
			if (val.EndsWith("\\"))
				return val + "\\";
			return val;
		}

		
		public static int Copy(string sourcePath, string targetPath, string[] fileTypes)
		{
			var res = 0;

			if (!Directory.Exists(targetPath))
				Directory.CreateDirectory(targetPath);

			foreach (var wildcard in fileTypes)
			{
				foreach (var file in Directory.GetFiles(sourcePath, wildcard, SearchOption.AllDirectories))
				{
					var targetFilePath = Path.Combine(targetPath, Path.GetFileName(file));
					File.Copy(file, targetFilePath, true);
					res++;
				}
			}

			return res;
		}



		private IPackageRepository _repository;

	    private readonly Process _parent;
	    private readonly UpdatingInfo _info;
		private readonly ManualResetEvent _terminationEvent = new ManualResetEvent(false);
		
		private static readonly string[] UpdateFileTypes = { "*.exe", "*.dll", "*.pdb", "*.xml" };

		private readonly Thread _thread;

		private string _appPath;
		private string _appParentPath;
		private string _updateDataPath;
		private Version _curVersion;

		private string PackageId { get { return _info.NugetAppName; } }

		private string UpdateUrl { get { return _info.NugetServerUrl; } }
	}
}
