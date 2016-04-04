using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;

namespace GainCapital.AutoUpdate.Tests
{
	static class ProcessUtil
	{
		public static Process Start(string appPath, string args = null, Dictionary<string, string> envVars = null, string curPath = null)
		{
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

			return process;
		}

		public static Process Execute(string appPath, string args = null, Dictionary<string, string> envVars = null, string curPath = null)
		{
			var process = Start(appPath, args, envVars, curPath);

			var result = new StringBuilder();

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

			return process;
		}

		public static string GetCommandLine(this Process process)
		{
			var commandLine = new StringBuilder(process.MainModule.FileName);

			commandLine.Append(" ");
			using (var searcher = new ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE ProcessId = " + process.Id))
			{
				foreach (var @object in searcher.Get())
				{
					commandLine.Append(@object["CommandLine"]);
					commandLine.Append(" ");
				}
			}

			return commandLine.ToString();
		}
	}
}
