using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
			_stagingPath = Path.Combine(_binPath, "TestStaging");

			_packagesPath = Path.GetFullPath(Path.Combine(_binPath, @"..\src\Tests.Server\Packages"));
			if (!Directory.Exists(_packagesPath))
				throw new ApplicationException();

			_currentAppPath = Path.Combine(_stagingPath, "current");

			Cleanup();

			Directory.CreateDirectory(_stagingPath);

			InitTestApp();

			StartNugetServer();
		}

		[TearDown]
		public static void Uninit()
		{
			_nugetServer.Kill();
		}

		static void InitTestApp()
		{
			var testAppPath = Path.Combine(_binPath, "DebugApp");
			var testAppAssemblyPath = Path.Combine(_binPath, "DebugApp", TestAppExeName);

			var versionText = FileVersionInfo.GetVersionInfo(testAppAssemblyPath).FileVersion;
			var appDeploymentPath = Path.Combine(_stagingPath, "v" + versionText);
			Directory.CreateDirectory(appDeploymentPath);

			foreach (var file in Directory.GetFiles(testAppPath))
			{
				var targetFile = Path.Combine(appDeploymentPath, Path.GetFileName(file));
				File.Copy(file, targetFile);
			}

			if (!JunctionPoint.Exists(_currentAppPath))
				JunctionPoint.Create(_currentAppPath, appDeploymentPath);
			Assert.That(Directory.Exists(_currentAppPath));
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

			_nugetServer = ProcessUtil.Start(exeFile, Settings.KlondikeStarArgs);
		}

		public static void Cleanup()
		{
			if (Directory.Exists(_currentAppPath))
				Directory.Delete(_currentAppPath);

			foreach (var file in Directory.GetFiles(_packagesPath, "*.nupkg"))
			{
				File.Delete(file);
			}

			foreach (var file in Directory.GetFiles(_binPath, "*.nupkg", SearchOption.AllDirectories))
			{
				File.Delete(file);
			}

			if (Directory.Exists(_stagingPath))
				Directory.Delete(_stagingPath, true);
		}

		[Test]
		public static void TestUpdatingOnce()
		{
			var testExePath = Path.Combine(_currentAppPath, TestAppExeName);

			BuildAndPublishUpdate(testExePath);

			var testProcess = ProcessUtil.Execute(testExePath, null,
				new Dictionary<string, string>
				{
					{ "NugetServerUrl", Settings.NugetUrl },
					{ "UpdatePackageLevel", "Beta" },
					{ "UpdateCheckingPeriod", "0:0:1" },
				});

			WaitUpdateFinished();

			var updaterLog = File.ReadAllText(Path.Combine(_stagingPath, @"UpdateData\GainCapital.AutoUpdate.log"));
			var successMessage = string.Format("{0} - finished successfully", testProcess.Id);
			Assert.That(updaterLog.Contains(successMessage));
		}

		static void BuildAndPublishUpdate(string testExePath)
		{
			var versionText = FileVersionInfo.GetVersionInfo(testExePath).FileVersion;
			var version = new Version(versionText);
			var newVersion = new Version(version.Major, version.Minor, version.MajorRevision, version.MinorRevision + 1);
			var buildFilePath = Path.GetFullPath(Path.Combine(_binPath, @"..\build.xml"));
			var buildArgs = string.Format("{0} /t:Build /t:Package /p:BUILD_VERSION={1} /p:VERSION_SUFFIX=\"-rc\"", buildFilePath,
				newVersion);
			ProcessUtil.Execute("msbuild.exe", buildArgs);

			var nugetPath = Path.GetFullPath(Path.Combine(_binPath, @"..\src\.nuget\nuget.exe"));

			foreach (var file in Directory.GetFiles(_binPath, "*.nupkg"))
			{
				ProcessUtil.Execute(nugetPath, string.Format("push {0} -Source {1}", file, Settings.NugetUrl));
			}
		}

		static void WaitUpdateFinished()
		{
			for (int i = 0; i < 10; i++)
			{
				var testProcesses = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(TestAppExeName));
				var newTestApps = testProcesses.Where(process => process.GetCommandLine().StartsWith(_currentAppPath)).ToList();

				if (newTestApps.Count > 1)
					throw new ApplicationException();

				if (newTestApps.Count == 1)
				{
					var newTestApp = newTestApps.First();
					newTestApp.Kill();
					if (!newTestApp.WaitForExit(5 * 1000))
						throw new ApplicationException();
					return;
				}

				Thread.Sleep(TimeSpan.FromSeconds(5));
			}

			throw new ApplicationException();
		}

		private const string TestAppExeName = "GainCapital.AutoUpdate.DebugProject.exe";

		private static string _binPath;
		private static string _stagingPath;
		private static string _packagesPath;
		private static string _currentAppPath;

		private static Process _nugetServer;
	}
}
