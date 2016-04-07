using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace GainCapital.AutoUpdate.Updater
{
    public class ConsoleProcess : ParentProcess
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GenerateConsoleCtrlEvent(ConsoleCtrlEvent sigevent, int dwProcessGroupId);
        public enum ConsoleCtrlEvent
        {
            CTRL_C = 0,
            CTRL_BREAK = 1,
            CTRL_CLOSE = 2,
            CTRL_LOGOFF = 5,
            CTRL_SHUTDOWN = 6
        }
        public static ConsoleProcess FromCommandLine(string[] args, int startIndex)
        {
            var processId = int.Parse(args[startIndex]);
            var fullName = args[startIndex + 1];

            return new ConsoleProcess(processId, fullName);
        }

        public ConsoleProcess(int processId, string fullName)
            : base(processId, fullName)
        {
            
        }
        
        public override void ShutDown()
        {
            Logger.LogInfo("Process is console app, sending CTRL+C");
            GenerateConsoleCtrlEvent(ConsoleCtrlEvent.CTRL_C, 0);
        }

        public override string ToCommandLine()
        {
            return string.Format("{0} {1} \"{2}\"",
                this.GetType().Name,
                base.ProcessId,
                base.FullName);
        }

        public override void Start()
        {
            Logger.LogInfo(string.Format("Starting process {0}", base.FullName));
			Process.Start(base.FullName, "");            
        }
    }
}