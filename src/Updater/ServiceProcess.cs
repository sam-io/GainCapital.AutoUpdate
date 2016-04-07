using System;
using System.ServiceProcess;

namespace GainCapital.AutoUpdate.Updater
{
    public class ServiceProcess : ParentProcess
    {
        private readonly string _serviceName;

        public static ServiceProcess FromCommandLine(string[] args, int startIndex)
        {
            var processId = int.Parse(args[startIndex]);
            var fullName = args[startIndex + 1];
            var serviceName = args[startIndex + 2];

            return new ServiceProcess(processId, fullName, serviceName);
        }

        public ServiceProcess(int processId, string fullName, string serviceName) : base(processId, fullName)
        {
            _serviceName = serviceName;
        }

        public override void ShutDown()
        {
            Logger.LogInfo(string.Format("Process is windows service, shutting down {0}", _serviceName));
            var controller = new ServiceController(_serviceName);
            controller.Stop();  
        }

        public override string ToCommandLine()
        {
            return string.Format("{0} {1} \"{2}\", \"{3}\"",
                this.GetType().Name,
                base.ProcessId,  
                base.FullName,
                _serviceName);
        }

        public override void Start()
        {
            Logger.LogInfo(string.Format("Starting service {0}", _serviceName));
            var service = new ServiceController(_serviceName);
            service.Start();
            Logger.LogInfo(string.Format("Waiting for service {0} to start...", _serviceName));
            service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));            
        }
    }
}