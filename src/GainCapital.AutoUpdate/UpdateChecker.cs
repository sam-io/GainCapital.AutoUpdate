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

namespace GainCapital.AutoUpdate
{
	public class UpdateChecker
	{
		public UpdateChecker(HostControl host, UpdatingInfo info)
		{
			_host = host;
			_info = info;

			info.Prepare();

			_thread = new Thread(CheckForUpdates);
		}

		public void Start()
		{
			var curAssemblyPath = Assembly.GetExecutingAssembly().Location;
			_appPath = Path.GetDirectoryName(curAssemblyPath);

			if (!JunctionPoint.Exists(_appPath))
			{
				LogError(string.Format("Invalid app folder structure: \"{0}\". Turned off auto updates.", _appPath));
				return;
			}

			_curVersion = new Version(FileVersionInfo.GetVersionInfo(curAssemblyPath).FileVersion);

			_thread.Start();
		}

		public void Stop()
		{
			_terminationEvent.Set();
			_thread.Interrupt();
			_thread.Join(TimeSpan.FromSeconds(10));
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
			var packageId = _info.NugetAppName;
			var updateUrl = _info.NugetServerUrl;

			var appParentPath = Path.GetDirectoryName(_appPath);
			var updateDataPath = Path.Combine(appParentPath, "UpdateData");

			if (Directory.Exists(updateDataPath))
				FileUtil.Cleanup(updateDataPath, "*.*", false, true);

			Log.Info(new
			{
				Category = Const.LogCategory.InternalDiagnostic,
				Message = string.Format("Auto update URL: {0}", updateUrl),
				IsPreProductionEnvironment = _info.IsPreProductionEnvironment,
			});

			if (string.IsNullOrEmpty(updateUrl))
				return;

			var repo = PackageRepositoryFactory.Default.CreateRepository(updateUrl);
			var lastPackage = GetLastPackage(repo, packageId);
			var updateVersion = lastPackage.Version.Version;

			if (updateVersion <= _curVersion)
				return;

			var packageManager = new PackageManager(repo, updateDataPath);
			packageManager.InstallPackage(packageId, lastPackage.Version, true, false);

			Log.Info(new
			{
				Category = Const.LogCategory.InternalDiagnostic,
				Message = string.Format("Updating {0}", _info.NugetAppName),
				OldVersion = _curVersion.ToString(),
				NewVersion = updateVersion.ToString(),
			});

			var packagePath = Path.Combine(updateDataPath, packageId + "." + lastPackage.Version);
			var updateDeploymentPath = Path.Combine(appParentPath, "v" + lastPackage.Version);
			var updatedCurrentPath = Path.Combine(appParentPath, "current");
			var packageBinPath = Path.Combine(packagePath, "lib");

			Copy(packageBinPath, updateDeploymentPath, UpdateFileTypes);
			Copy(_appPath, updateDeploymentPath, new[] { "*.log" });
			_info.OnUpdate(packageBinPath, updateDeploymentPath);

			var updaterPath = Path.Combine(updateDeploymentPath, "GainCapital.Updater.exe");

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

		private IPackage GetLastPackage(IPackageRepository repo, string packageId)
		{
			var packages = repo.FindPackagesById(packageId).ToList();
			packages.RemoveAll(val => !val.IsListed());
			if (_info.IsPreProductionEnvironment != true)
				packages.RemoveAll(val => !val.IsReleaseVersion());
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

		static int Copy(string sourcePath, string targetPath, string[] fileTypes)
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
			var appender = (FileAppender)hierarchy.GetAppenders().First(cur => cur.Name == "MainLogAppender");
			if (appender == null)
				return null;
			var res = Path.GetDirectoryName(appender.File);
			return res;
		}

		private readonly UpdatingInfo _info;
		private readonly ManualResetEvent _terminationEvent = new ManualResetEvent(false);
		private static readonly ILog Log = LogManager.GetLogger(typeof(UpdateChecker));

		private static readonly string[] UpdateFileTypes = { "*.exe", "*.dll", "*.pdb", "*.xml" };

		private readonly HostControl _host;
		private readonly Thread _thread;

		private string _appPath;
		private Version _curVersion;
	}
}
