using System;

namespace GainCapital.AutoUpdate.Updater
{
    public class DeploymentEnvironment
    {
        public static string EnvironmentName
        {
            get { return Environment.GetEnvironmentVariable("EnvironmentName", EnvironmentVariableTarget.Machine); }
        }

        public static string EnvironmentType
        {
            get { return Environment.GetEnvironmentVariable("EnvironmentType", EnvironmentVariableTarget.Machine); }
        }
    }
}
