using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using log4net;
using log4net.Appender;
using log4net.Repository.Hierarchy;
using NuGet;
using Topshelf;
using Topshelf.Hosts;

using GainCapital.AutoUpdate.Updater;
using ThreadState = System.Threading.ThreadState;

namespace GainCapital.AutoUpdate
{
	public class UpdateChecker
	{
		public UpdateChecker(HostControl host, UpdatingInfo info)
		{
			_host = host;
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
			var curAssemblyPath = Assembly.GetEntryAssembly().Location;
			_appPath = Path.GetDirectoryName(curAssemblyPath);

			if (!JunctionPoint.Exists(_appPath))
			{
				LogError(string.Format("Invalid app folder structure: \"{0}\". Turned off auto updates.", _appPath));
				return;
			}

			_curVersion = new Version(FileVersionInfo.GetVersionInfo(curAssemblyPath).FileVersion);

			_appParentPath = Path.GetDirectoryName(_appPath);
			_updateDataPath = Path.Combine(_appParentPath, "UpdateData");

			_repository = PackageRepositoryFactory.Default.CreateRepository(UpdateUrl);

			lock (_thread)
			{
				_thread.Start();
			}
		}

		public void Stop()
		{
			lock (_thread)
			{
				_terminationEvent.Set();
				Thread.Sleep(1);

				_thread.Interrupt();
				if (_thread.ThreadState == ThreadState.Running)
					_thread.Join(TimeSpan.FromSeconds(10));
			}
		}

		void CheckForUpdates()
		{
			while (true)
			{
				try
				{
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
			Log.Info(new
			{
				Category = Const.LogCategory.InternalDiagnostic,
				Message = "Checking for updates",
				UpdateUrl,
				_info.UpdatePackageLevel,
				CurrentVersion = _curVersion.ToString(),
			});

			if (Directory.Exists(_updateDataPath))
				FileUtil.Cleanup(_updateDataPath, "*.*", false, true);

			if (string.IsNullOrEmpty(UpdateUrl))
				return;

			var lastPackage = GetLastPackage(PackageId);
			var updateVersion = lastPackage.Version.Version;

			if (updateVersion <= _curVersion)
				return;

			Log.Info(new
			{
				Category = Const.LogCategory.InternalDiagnostic,
				Message = string.Format("Installing update: {0}", _info.NugetAppName),
				OldVersion = _curVersion.ToString(),
				NewVersion = updateVersion.ToString(),
			});

			InstallUpdate(lastPackage);
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

			var updaterPath = Path.Combine(updateDeploymentPath, "GainCapital.Updater.exe");
			var updatedCurrentPath = Path.Combine(_appParentPath, "current");

			var appMode = GetAppMode(_host);
			var startingName = (appMode == AppMode.Service) ? _info.ServiceName : _info.ExeName;
			var args = string.Format("{0} {1} \"{2}\" \"{3}\" \"{4}\"", Process.GetCurrentProcess().Id, appMode,
				EscapeCommandLineArg(startingName), EscapeCommandLineArg(updateDeploymentPath),
				EscapeCommandLineArg(updatedCurrentPath));

			Process.Start(new ProcessStartInfo
			{
				WorkingDirectory = GetMainLogFolder() ?? Path.GetTempPath(),
				FileName = updaterPath,
				Arguments = args,
			});
			_host.Stop();
		}

		private IPackage GetLastPackage(string packageId)
		{
			var packages = _repository.FindPackagesById(packageId).ToList();
			packages.RemoveAll(val => !val.IsListed());

			if (_info.UpdatePackageLevel > PackageLevel.Beta)
				packages.RemoveAll(val => val.Version.SpecialVersion.ToLowerInvariant() == "beta");

			if (_info.UpdatePackageLevel > PackageLevel.RC)
				packages.RemoveAll(val => val.Version.SpecialVersion.ToLowerInvariant() == "rc");

			if (packages.Count == 0)
				throw new ApplicationException("No update package is found");
			packages.Sort((x, y) => x.Version.CompareTo(y.Version));
			var lastPackage = packages.Last();
			return lastPackage;
		}

		public static AppMode GetAppMode(HostControl hostControl)
		{
			return (hostControl is ConsoleRunHost) ? AppMode.Console : AppMode.Service;
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

		static void LogInfo(string message)
		{
			Log.Info(new
			{
				Category = Const.LogCategory.InternalDiagnostic,
				Message = message,
			});
		}

		static void LogError(string message)
		{
			Log.Error(new
			{
				Category = Const.LogCategory.InternalDiagnostic,
				Message = message,
			});
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

		static string GetMainLogFolder()
		{
			var hierarchy = (Hierarchy)LogManager.GetRepository();
			var appender = (FileAppender)hierarchy.GetAppenders().FirstOrDefault(cur => cur.Name == "MainLogAppender");
			if (appender == null)
				return null;
			var res = Path.GetDirectoryName(appender.File);
			return res;
		}

		private IPackageRepository _repository;

		private readonly UpdatingInfo _info;
		private readonly ManualResetEvent _terminationEvent = new ManualResetEvent(false);
		private static readonly ILog Log = LogManager.GetLogger(typeof(UpdateChecker));

		private static readonly string[] UpdateFileTypes = { "*.exe", "*.dll", "*.pdb", "*.xml" };

		private readonly HostControl _host;
		private readonly Thread _thread;

		private string _appPath;
		private string _appParentPath;
		private string _updateDataPath;
		private Version _curVersion;

		private string PackageId { get { return _info.NugetAppName; } }

		private string UpdateUrl { get { return _info.NugetServerUrl; } }
	}
}
