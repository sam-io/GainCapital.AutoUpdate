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
		        Directory.CreateDirectory(_packagesPath);

			_currentAppPath = Path.Combine(_stagingPath, "current");

			Cleanup();

			Directory.CreateDirectory(_stagingPath);

			InitTestApp();
            SetupEnvironment();
			StartNugetServer();
		}

		[TearDown]
		public static void Uninit()
		{
            if (_nugetServer!=null)
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
				Directory.Delete(_currentAppPath, true);

			foreach (var file in Directory.GetFiles(_packagesPath, "*.nupkg"))
			{
				File.Delete(file);
			}
            
			foreach (var file in Directory.GetFiles(_binPath, "*.nupkg"))
			{
				File.Delete(file);
			}

			if (Directory.Exists(_stagingPath))
				Directory.Delete(_stagingPath, true);
		}

		[Test]
		public static void TestUpdatingConsole()
		{
			var testExePath = Path.Combine(_currentAppPath, TestAppExeName);
			var newVersion = BuildAndPublishUpdate(testExePath);
			var testProcess = ProcessUtil.Execute(testExePath);

            var newProcess = WaitUpdateFinished(newVersion);
            newProcess.CloseMainWindow();
            if (!newProcess.WaitForExit(5 * 1000))
                throw new ApplicationException();
            
			var updaterLog = File.ReadAllText(Path.Combine(_stagingPath, @"UpdateData\GainCapital.AutoUpdate.log"));
			var successMessage = string.Format("{0} - finished successfully", testProcess.Id);
			Assert.That(updaterLog.Contains(successMessage));

			var updatedVersion = new Version(FileVersionInfo.GetVersionInfo(testExePath).FileVersion);
			Assert.That(updatedVersion,Is.EqualTo(newVersion));
		}


        [Test]
        public static void TestUpdatingService()
        {
            var testExePath = Path.Combine(_currentAppPath, TestAppExeName);
            var newVersion = BuildAndPublishUpdate(testExePath);
            ProcessUtil.Execute(testExePath, "install --manual");
            try
            {
                ProcessUtil.Execute(testExePath, "start");

                WaitUpdateFinished(newVersion);
                ProcessUtil.Execute(testExePath, "stop");

                var updaterLog = File.ReadAllText(Path.Combine(_stagingPath, @"UpdateData\GainCapital.AutoUpdate.log"));            
                Assert.That(updaterLog.Contains("finished successfully"));

                var updatedVersion = new Version(FileVersionInfo.GetVersionInfo(testExePath).FileVersion);
                Assert.That(updatedVersion, Is.EqualTo(newVersion));
            }
            finally
            {
                ProcessUtil.Execute(testExePath, "uninstall");
            }
        }

	    private static void SetupEnvironment()
	    {
            Environment.SetEnvironmentVariable("EnvironmentName", "DEV", EnvironmentVariableTarget.Machine);
            Environment.SetEnvironmentVariable("EnvironmentType", "DEV", EnvironmentVariableTarget.Machine);
            Environment.SetEnvironmentVariable("NugetServerUrl", Settings.NugetUrl, EnvironmentVariableTarget.Machine);
            Environment.SetEnvironmentVariable("UpdatePackageLevel", "Beta", EnvironmentVariableTarget.Machine);
            Environment.SetEnvironmentVariable("UpdateCheckingPeriod", "0:0:1", EnvironmentVariableTarget.Machine);
	    }

		static Version BuildAndPublishUpdate(string testExePath)
		{
			var versionText = FileVersionInfo.GetVersionInfo(testExePath).FileVersion;
			var version = new Version(versionText);
			var newVersion = new Version(version.Major, version.Minor, version.MajorRevision, version.MinorRevision + 1);
            var buildFilePath = Path.GetFullPath(Path.Combine(_binPath, @"..\build.xml"));
            var buildArgs = string.Format("{0} /t:Build /t:Package /p:BUILD_VERSION={1} /p:VERSION_SUFFIX=\"-beta\"", buildFilePath,
				newVersion);

            var versionFile = Path.GetFullPath(Path.Combine(_binPath, @"..\src\DebugProject\Properties\AssemblyVersion.cs"));
		    var oldVersionText = File.ReadAllText(versionFile);
		    try
		    {
                File.WriteAllText(versionFile,
                        string.Format("using System.Reflection;\n" +
                                      "[assembly: AssemblyVersion(\"{0}\")]\n" +
                                      "[assembly: AssemblyInformationalVersion(\"{0}\")]", newVersion));

			    ProcessUtil.Execute(Path.Combine(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "msbuild.exe"), buildArgs);
            }
            finally
		    {
		        File.WriteAllText(versionFile, oldVersionText);
		    }

			var nugetPath = Path.GetFullPath(Path.Combine(_binPath, @"..\src\.nuget\nuget.exe"));

			foreach (var file in Directory.GetFiles(_binPath, "*.nupkg"))
			{
				ProcessUtil.Execute(nugetPath, string.Format("push {0} -Source {1}", file, Settings.NugetUrl));
			}

			return newVersion;
		}

		private static Process WaitUpdateFinished(Version newVersion)
		{
			for (var i = 0; i < 10; i++)
			{
				var testProcesses = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(TestAppExeName));
                var newTestApps = testProcesses.Where(p => new Version(p.MainModule.FileVersionInfo.FileVersion) == newVersion).ToArray();

				if (newTestApps.Length > 1)
					throw new ApplicationException();

                if (newTestApps.Length == 1)
				{
					return newTestApps.First();				    
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
