using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Xml;

using NUnit.Framework;

using GainCapital.AutoUpdate.Updater;

namespace GainCapital.AutoUpdate.Tests
{
	[TestFixture]
	static class IntegrationTests
	{
		[SetUp]
		public static void Init()
		{
			_binPath = TestContext.CurrentContext.TestDirectory;
			_testBinPath = Path.Combine(_binPath, "TestBin");
			Directory.CreateDirectory(_testBinPath);

			_stagingPath = Path.Combine(_binPath, "TestStaging");
			_currentAppPath = Path.Combine(_stagingPath, "current");

			Cleanup();

			Directory.CreateDirectory(_stagingPath);

			InitTestApp();

			StartNugetServer();
		}

		[TearDown]
		public static void Uninit()
		{
			try
			{
				if (_serviceInstalled)
					ProcessUtil.Execute(_testExePath, "uninstall");
			}
			catch (Exception exc)
			{
				Console.WriteLine(exc);
			}

			if (_nugetServer != null)
				_nugetServer.Stop(10);
		}

		static void InitTestApp()
		{
			var testAppAssemblyPath = Path.Combine(_binPath, TestAppExeName);

			var versionText = FileVersionInfo.GetVersionInfo(testAppAssemblyPath).FileVersion;
			var appDeploymentPath = Path.Combine(_stagingPath, "v" + versionText);
			Directory.CreateDirectory(appDeploymentPath);

			foreach (var file in Directory.GetFiles(_binPath))
			{
				var targetFile = Path.Combine(appDeploymentPath, Path.GetFileName(file));
				File.Copy(file, targetFile);
			}

			if (!JunctionPoint.Exists(_currentAppPath))
				JunctionPoint.Create(_currentAppPath, appDeploymentPath);
			Assert.That(Directory.Exists(_currentAppPath));

			_testExePath = Path.Combine(_currentAppPath, TestAppExeName);
		}

		static void StartNugetServer()
		{
			var uri = new Uri(Settings.KlondikeUrl);
			var fileName = Path.GetFileName(uri.LocalPath);
			var serverPath = Path.Combine(_binPath, "NuGet", Path.GetFileNameWithoutExtension(uri.LocalPath));
			Directory.CreateDirectory(serverPath);

			var packageFile = Path.Combine(serverPath, fileName);

			var exeFile = Path.Combine(serverPath, @"bin\Klondike.SelfHost.exe");
			if (!File.Exists(exeFile))
			{
				if (File.Exists(packageFile))
					File.Delete(packageFile);
				using (var client = new WebClient())
				{
					client.DownloadFile(uri, packageFile);
				}

				ZipFile.ExtractToDirectory(packageFile, serverPath);
			}

			var serverDataPath = Path.GetFullPath(Path.Combine(serverPath, "App_Data"));
			if (Directory.Exists(serverDataPath))
			{
				foreach (var file in Directory.GetFiles(serverDataPath, "*.*", SearchOption.AllDirectories))
				{
					File.Delete(file);
				}
			}

			_nugetServer = ProcessUtil.Start(exeFile, Settings.KlondikeStarArgs).Process;
		}

		public static void Cleanup()
		{
			if (Directory.Exists(_currentAppPath))
				Directory.Delete(_currentAppPath);

			foreach (var file in Directory.GetFiles(_testBinPath, "*.nupkg", SearchOption.AllDirectories))
			{
				File.Delete(file);
			}

			if (Directory.Exists(_stagingPath))
				Directory.Delete(_stagingPath, true);
		}

		[Test]
		public static void TestUpdatingOnceConsoleMode()
		{
			TestUpdatingOnce(AppMode.Console);
		}

		[Test]
		public static void TestUpdatingOnceServiceMode()
		{
			TestUpdatingOnce(AppMode.Service);
		}

		static void TestUpdatingOnce(AppMode mode)
		{
			Process testProcess = null;

			if (mode == AppMode.Console)
			{
				testProcess = ProcessUtil.Start(_testExePath, null,
					new Dictionary<string, string>
					{
						{ "NugetServerUrl", Settings.NugetUrl },
						{ "UpdatePackageLevel", Settings.UpdatePackageLevel },
						{ "UpdateCheckingPeriod", Settings.UpdateCheckingPeriod },
					}).Process;
			}
			else if (mode == AppMode.Service)
			{
				if (!_serviceInstalled)
				{
					ProcessUtil.Execute(_testExePath, "install --manual");
					_serviceInstalled = true;
				}

				SetConfigUpdateParams(_testExePath + ".config");
				ProcessUtil.Execute(_testExePath, "start");
			}
			else
				throw new NotSupportedException();

			try
			{
				var newVersion = BuildAndPublishUpdate(_testExePath);

				var updaterLogPath = Path.Combine(_stagingPath, @"UpdateData\GainCapital.AutoUpdate.log");
				WaitUpdateFinished(mode, updaterLogPath);

				var updaterLog = File.ReadAllText(updaterLogPath);
				var successMessage = " - finished successfully";
				Assert.That(updaterLog.Contains(successMessage));

				var updatedVersion = new Version(FileVersionInfo.GetVersionInfo(_testExePath).FileVersion);
				Assert.That(updatedVersion == newVersion);
			}
			finally
			{
				if (testProcess != null)
					testProcess.Stop();
			}
		}

		static Version BuildAndPublishUpdate(string testExePath)
		{
			var versionText = FileVersionInfo.GetVersionInfo(testExePath).FileVersion;
			var version = new Version(versionText);
			var newVersion = new Version(version.Major, version.Minor, version.MajorRevision, version.MinorRevision + 1);
			var buildFilePath = Path.GetFullPath(Path.Combine(_binPath, @"..\build.xml"));
			var buildArgs = string.Format("{0} /t:Package /p:BUILD_VERSION={1} /p:VERSION_SUFFIX=\"-rc\" /p:OutputPath=\"{2}\"",
				buildFilePath, newVersion, _testBinPath);
			ProcessUtil.Execute("msbuild.exe", buildArgs);

			var nugetPath = Path.GetFullPath(Path.Combine(_binPath, @"..\src\.nuget\nuget.exe"));

			foreach (var file in Directory.GetFiles(_testBinPath, "*.nupkg"))
			{
				ProcessUtil.Execute(nugetPath, string.Format("push {0} -Source {1}", file, Settings.NugetUrl));
			}

			return newVersion;
		}

		static void WaitUpdateFinished(AppMode mode, string updateLogPath)
		{
			for (var i = 0; i < 60; i++)
			{
				Thread.Sleep(TimeSpan.FromSeconds(1));

				if (!File.Exists(updateLogPath))
					continue;

				var updaterProcesses = Process.GetProcessesByName(UpdaterExeProcessName).Where(
					process => process.GetCommandLine().StartsWith(_currentAppPath)).ToList();
				if (updaterProcesses.Count != 0)
					continue;

				var testProcesses = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(TestAppExeName)).Where(
					process => process.GetCommandLine().StartsWith(_currentAppPath)).ToList();

				if (testProcesses.Count > 1)
					throw new ApplicationException();

				if (testProcesses.Count == 1)
				{
					var newTestApp = testProcesses.First();

					if (!newTestApp.WaitForExit(5 * 1000))
					{
						if (mode == AppMode.Console)
							newTestApp.Stop();
						else if (mode == AppMode.Service)
							ProcessUtil.Execute(_testExePath, "stop");
						else
							throw new NotSupportedException();
					}

					return;
				}
				else
					return;
			}

			throw new ApplicationException();
		}

		static void SetConfigUpdateParams(string configName)
		{
			var config = new XmlDocument();
			config.Load(configName);

			SetAttribute(config, "NugetServerUrl", Settings.NugetUrl);
			SetAttribute(config, "UpdatePackageLevel", Settings.UpdatePackageLevel);
			SetAttribute(config, "UpdateCheckingPeriod", Settings.UpdateCheckingPeriod);

			config.Save(configName);
		}

		static void SetAttribute(XmlDocument config, string attributeName, string val)
		{
			var query = string.Format("/configuration/appSettings/add[@key='{0}']/@value", attributeName);
			config.SelectSingleNode(query).InnerText = val;
		}

		private const string TestAppExeName = "GainCapital.AutoUpdate.DebugProject.exe";
		private const string UpdaterExeProcessName = "GainCapital.Updater";

		private static string _binPath;
		private static string _testBinPath;
		private static string _stagingPath;
		private static string _currentAppPath;

		private static string _testExePath;

		private static Process _nugetServer;

		private static bool _serviceInstalled;
	}
}
