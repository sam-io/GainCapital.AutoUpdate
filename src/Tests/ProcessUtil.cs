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
		public static void Execute(string appPath)
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

			process.Start();

			process.OutputDataReceived +=
				(sender, args) =>
				{
					lock (result)
					{
						result.AppendLine(args.Data);
					}
				};
			process.BeginOutputReadLine();

			process.ErrorDataReceived +=
				(sender, args) =>
				{
					lock (result)
					{
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
