using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;

namespace GainCapital.AutoUpdate.Updater
{
    public abstract class ParentProcess
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct ParentProcessInfo
        {
            public IntPtr Reserved1;
            public IntPtr PebBaseAddress;
            public IntPtr Reserved2_0;
            public IntPtr Reserved2_1;
            public IntPtr UniqueProcessId;
            public IntPtr InheritedFromUniqueProcessId;
        }

        private readonly int _processId;
        private readonly string _fullName;
        
        public string Location
        {
            get{ return Path.GetDirectoryName(_fullName); }
        }

        protected string FullName
        {
            get { return _fullName; }
        }

        public Version Version
        {
            get { return new Version(FileVersionInfo.GetVersionInfo(_fullName).FileVersion); }
        }

        public string FileName
        {
            get { return Path.GetFileName(_fullName); }
        }

        public int ProcessId
        {
            get { return _processId; }
        }

        [DllImport("ntdll.dll")]
        private static extern int NtQueryInformationProcess(IntPtr processHandle, int processInformationClass, ref ParentProcessInfo processInformation, int processInformationLength, out int returnLength);

        public static ParentProcess GetParentProcess()
        {
            return GetParentProcess(Process.GetCurrentProcess().Handle);
        }

        public static ParentProcess FromCommandLine(string[] args, int startIndex)
        {
            var processTypeName = args[startIndex];
            var processType = typeof(ParentProcess).Assembly
                    .GetTypes()
                    .Where(t => t.IsSubclassOf(typeof (ParentProcess)))
                    .FirstOrDefault(t => t.Name == processTypeName);

            return (ParentProcess)processType
                    .GetMethod("FromCommandLine", BindingFlags.Static | BindingFlags.Public)
                    .Invoke(null, new object[]{args, startIndex+1});
            }

        private static string GetService(int processId)
        {
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_Service WHERE ProcessId =" + "\"" + processId + "\""))
            {
                foreach (ManagementObject service in searcher.Get())
                    return (string)service["Name"];
            }
            return null;
        }

        private static ParentProcess GetParentProcess(IntPtr handle)
        {
            var pbi = new ParentProcessInfo();
            int returnLength;
            var status = NtQueryInformationProcess(handle, 0, ref pbi, Marshal.SizeOf(pbi), out returnLength);
            if (status != 0)
                return null;
            try
            {
                var processId = pbi.InheritedFromUniqueProcessId.ToInt32();
                var serviceName = GetService(processId);

                if(!string.IsNullOrEmpty(serviceName))
                    return new ServiceProcess(processId, serviceName, Process.GetProcessById(processId).MainModule.FileName);

                return new ConsoleProcess(processId, Process.GetProcessById(processId).MainModule.FileName);
            }
            catch (ArgumentException)
            {
                // not found
                return null;
            }
        }

        protected ParentProcess(int processId, string fullName)
        {
            _processId = processId;
            _fullName = fullName;            
        }

        public bool IsRunning
        {
            get { return Process.GetProcesses().Any(p => p.Id == _processId); }
        }

        public abstract void ShutDown();

        public abstract string ToCommandLine();

        public bool WaitForExit()
        {
            if (IsRunning)
            {
                try
                {
                    var parentProcess = Process.GetProcessById(_processId);
                    if (!parentProcess.WaitForExit((int) TimeSpan.FromSeconds(50).TotalMilliseconds))
                        return false;
                }
                catch (ArgumentException)
                {
                    // process already has stopped
                }
            }

            return true;
        }

        public abstract void Start();
    }
}