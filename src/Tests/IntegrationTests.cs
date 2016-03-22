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
			Directory.CreateDirectory(_stagingPath);
		}

		[TearDown]
		public static void Cleanup()
		{
			//Directory.Delete(_stagingPath, true);
		}

		[Test]
		public static void TestUpdating()
		{
			var currentAppPath = Path.Combine(_stagingPath, "current");
			if (!JunctionPoint.Exists(currentAppPath))
				JunctionPoint.Create(currentAppPath, _binPath);
			Assert.That(Directory.Exists(currentAppPath));

			var testAppPath = Path.Combine(currentAppPath, typeof(TestApp.Program).Assembly.ManifestModule.Name);

			ProcessUtil.Execute(testAppPath,
				new Dictionary<string, string>
				{
					{ "NugetServerUrl", Settings.NugetUrl },
				});
		}

		private static string _binPath;
		private static string _stagingPath;
	}
}
