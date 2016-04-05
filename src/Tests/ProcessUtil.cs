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
		public static ProcessInfo Start(string appPath, string args = null, Dictionary<string, string> envVars = null, string curPath = null)
		{
			Console.WriteLine("> {0} {1}", appPath, args);

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

			var processInfo = new ProcessInfo
			{
				Process = process,
			};
			var result = processInfo.Result;

			process.OutputDataReceived +=
				(sender, eventArgs) =>
				{
					lock (result)
					{
						Console.WriteLine("[{0}] {1}", process.ProcessName, eventArgs.Data);
						result.AppendLine(eventArgs.Data);
					}
				};
			process.BeginOutputReadLine();

			process.ErrorDataReceived +=
				(sender, eventArgs) =>
				{
					lock (result)
					{
						Console.WriteLine("[{0}] {1}", process.ProcessName, eventArgs.Data);
						result.AppendLine(eventArgs.Data);
					}
				};
			process.BeginErrorReadLine();

			return processInfo;
		}

		public static Process Execute(string appPath, string args = null, Dictionary<string, string> envVars = null, string curPath = null)
		{
			var processInfo = Start(appPath, args, envVars, curPath);
			var process = processInfo.Process;

			if (!process.WaitForExit(60 * 1000))
			{
				process.CloseMainWindow();
				process.Kill();
				process.WaitForExit();
			}

			if (process.ExitCode != 0)
				throw new ApplicationException(string.Format("Exit code: {0}\r\n", process.ExitCode) + processInfo.Result);

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
