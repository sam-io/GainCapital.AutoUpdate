using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GainCapital.AutoUpdate.Tests
{
	static class ProcessUtil
	{
		public static void Execute(string appPath, Dictionary<string, string> envVars)
		{
			var result = new StringBuilder();

			var process = new Process
			{
				StartInfo = new ProcessStartInfo
				{
					FileName = appPath,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					UseShellExecute = false,
				}
			};
			foreach (var envVar in envVars)
			{
				process.StartInfo.EnvironmentVariables.Add(envVar.Key, envVar.Value);
			}

			process.Start();

			process.OutputDataReceived +=
				(sender, args) =>
				{
					lock (result)
					{
						Console.WriteLine(args.Data);
						result.AppendLine(args.Data);
					}
				};
			process.BeginOutputReadLine();

			process.ErrorDataReceived +=
				(sender, args) =>
				{
					lock (result)
					{
						Console.WriteLine(args.Data);
						result.AppendLine(args.Data);
					}
				};
			process.BeginErrorReadLine();

			if (!process.WaitForExit(10 * 1000))
				process.Kill();

			if (process.ExitCode != 0)
				throw new ApplicationException(result.ToString());
		}
	}
}
