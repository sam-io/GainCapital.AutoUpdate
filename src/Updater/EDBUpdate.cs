using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace GainCapital.AutoUpdate.Updater
{
    public static class EdbUpdate
    {
        private static readonly Dictionary<string, string> EdbEndpoints = new Dictionary<string, string>()
        {
            {"QAT", "http://edbwebapi-stg.cityindex.co.uk:8081/api/UpdateDeployedInfo"},
            {"PPE", "http://edbwebapi-stg.cityindex.co.uk:8081/api/UpdateDeployedInfo"},
            {"LIVE", "http://edbwebapi.cityindex.co.uk:8081/api/UpdateDeployedInfo"}
        };

        public static async void UpdateEdb(string componentName, string path)
        {
            try
            {
                var environmentType = DeploymentEnvironment.EnvironmentType;
                var changeRequestFile = Path.Combine(path, "CRNumber.txt");
                if (!string.IsNullOrEmpty(environmentType) && EdbEndpoints.ContainsKey(environmentType.ToUpper()) && File.Exists(changeRequestFile))
                {
                    var client = new HttpClient();
                    var changeRequestNumber = File.ReadAllText(changeRequestFile).Trim();
                    var request = string.Format(
                            "{{ 'ComponentType': 'WindowsServices', " +
                            "  'ComponentName': '{0}', " +
                            "  'CrNumber': '{1}', " +
                            "  'MachineName': '{2}' }}",
                            componentName,
                            changeRequestNumber,
                            Environment.MachineName);
                    
                    var content = new StringContent(request, Encoding.UTF8, "application/json");
                    var result = await client.PostAsync(EdbEndpoints[environmentType.ToUpper()], content);                   
                    Logger.LogInfo(await result.Content.ReadAsStringAsync());
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(string.Format("Unable to update edb. {0}.", ex));
                throw;
            }

        }

    }
}
