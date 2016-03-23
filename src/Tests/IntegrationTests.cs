using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
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
		public static void TestUpdating()
		{
			var versionText = FileVersionInfo.GetVersionInfo(typeof(TestApp.Program).Assembly.Location).FileVersion;
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

			var version = new Version(versionText);
			var newVersion = new Version(version.Major, version.Minor, version.MajorRevision, version.MinorRevision + 1);
			var buildFilePath = Path.GetFullPath(Path.Combine(_binPath, @"..\build.xml"));
			var buildArgs = $"{buildFilePath} /t:Build /t:Package /p:BUILD_VERSION={newVersion} /p:VERSION_SUFFIX=\"-rc\"";
			ProcessUtil.Execute("msbuild.exe", buildArgs);

			foreach (var file in Directory.GetFiles(_binPath, "*.nupkg"))
			{
				var targetFile = Path.Combine(_packagesPath, Path.GetFileName(file));
				File.Copy(file, targetFile);
			}

			var testAppPath = Path.Combine(_currentAppPath, typeof(TestApp.Program).Assembly.ManifestModule.Name);

			ProcessUtil.Execute(testAppPath, null,
				new Dictionary<string, string>
				{
					{ "NugetServerUrl", Settings.NugetUrl },
					{ "UpdatePackageLevel", "Beta" },
					{ "UpdateCheckingPeriod", "0:0:1" },
				});
		}

		private static string _binPath;
		private static string _stagingPath;
		private static string _packagesPath;
		private static string _currentAppPath;
	}
}
