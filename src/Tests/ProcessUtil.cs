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
		public static void Execute(string appPath, string args = null, Dictionary<string, string> envVars = null)
		{
			var result = new StringBuilder();

			var process = new Process
			{
				StartInfo = new ProcessStartInfo
				{
					FileName = appPath,
					Arguments = args,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					UseShellExecute = false,
				}
			};
			if (envVars != null)
			{
				foreach (var envVar in envVars)
				{
					process.StartInfo.EnvironmentVariables[envVar.Key] = envVar.Value;
				}
			}

			process.Start();

			process.OutputDataReceived +=
				(sender, eventArgs) =>
				{
					lock (result)
					{
						Console.WriteLine(eventArgs.Data);
						result.AppendLine(eventArgs.Data);
					}
				};
			process.BeginOutputReadLine();

			process.ErrorDataReceived +=
				(sender, eventArgs) =>
				{
					lock (result)
					{
						Console.WriteLine(eventArgs.Data);
						result.AppendLine(eventArgs.Data);
					}
				};
			process.BeginErrorReadLine();

			if (!process.WaitForExit(10 * 1000))
			{
				process.Kill();
				process.WaitForExit();
			}

			if (process.ExitCode != 0)
				throw new ApplicationException(result.ToString());
		}
	}
}
