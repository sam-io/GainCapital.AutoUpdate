using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NUnit.Framework;

namespace GainCapital.AutoUpdate.Tests
{
	[TestFixture]
	static class IntegrationTests
	{
		[Test]
		public static void TestUpdating()
		{
			var testAppPath = Path.Combine(TestContext.CurrentContext.TestDirectory, typeof(TestApp.Program).Assembly.ManifestModule.Name);
		}
	}
}
