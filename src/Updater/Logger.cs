using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Web;

namespace GainCapital.AutoUpdate.Updater
{
    public static class Logger
    {
        public static void Log(string message, string level)
        {
            Trace.WriteLine(message);
            Console.WriteLine(message);

            try
            {
                var updataDir = Path.Combine(Directory.GetCurrentDirectory(), @"..\UpdateData\");
                if (!Directory.Exists(updataDir))
                    Directory.CreateDirectory(updataDir);

                var path = Path.Combine(updataDir, "GainCapital.AutoUpdate.log");

                var line = string.Format("{{\"timestamp\":\"{0}\", \"level\":\"{1}\", \"Message\":\"{2}\"}}",
                    DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ"), EscapeJsonVal(level), EscapeJsonVal(message));

                for (var i = 1; i <= 10; i++)
                {
                    try
                    {
                        File.AppendAllLines(path, new string[] { line });
                        break;
                    }
                    catch (IOException)
                    {
                        Thread.Sleep(i);
                    }
                }
            }
            catch (Exception exc)
            {
                Trace.WriteLine(exc.ToString());
                Console.WriteLine(exc.ToString());
            }
        }

        public static void LogError(string message)
        {
            Log(message, "ERROR");
        }

        public static void LogInfo(string message)
        {
            Log(message, "INFO");
        }

        private static string EscapeJsonVal(string val)
        {
            return HttpUtility.JavaScriptStringEncode(val);
        }
    }
}