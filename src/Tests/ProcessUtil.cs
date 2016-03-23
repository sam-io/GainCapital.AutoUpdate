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
		public static void Execute(string appPath, string args = null, Dictionary<string, string> envVars = null, string curPath = null)
		{
			var result = new StringBuilder();

			var startInfo = new ProcessStartInfo
			{
				FileName = appPath,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
			};

			if (args != null)
				startInfo.Arguments = args;
			if (curPath != null)
				startInfo.WorkingDirectory = curPath;

			if (envVars != null)
			{
				foreach (var envVar in envVars)
				{
					startInfo.EnvironmentVariables[envVar.Key] = envVar.Value;
				}
			}

			var process = new Process
			{
				StartInfo = startInfo,
			};
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
