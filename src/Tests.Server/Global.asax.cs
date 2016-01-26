using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Reflection;
using System.Web;
using System.Web.Configuration;

namespace GainCapital.AutoUpdate.Tests.Server
{
	public class Global : System.Web.HttpApplication
	{
		protected void Application_Start(object sender, EventArgs e)
		{
			var location = Assembly.GetExecutingAssembly().CodeBase;
			location = Path.GetDirectoryName((new Uri(location)).LocalPath);

			var appSettings = WebConfigurationManager.AppSettings;
			appSettings["packagesPath"] = Path.GetFullPath(Path.Combine(location, appSettings["packagesPath"]));
		}

		protected void Session_Start(object sender, EventArgs e)
		{

		}

		protected void Application_BeginRequest(object sender, EventArgs e)
		{

		}

		protected void Application_AuthenticateRequest(object sender, EventArgs e)
		{

		}

		protected void Application_Error(object sender, EventArgs e)
		{

		}

		protected void Session_End(object sender, EventArgs e)
		{

		}

		protected void Application_End(object sender, EventArgs e)
		{

		}
	}
}