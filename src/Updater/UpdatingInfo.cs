﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GainCapital.AutoUpdate
{
    public class UpdatingInfo
	{
		public string NugetAppName;
		public string NugetServerUrl;

		public PackageLevel UpdatePackageLevel
		{
			get { return _updatePackageLevel; }
			set
			{
				_updatePackageLevel = value;
				_isUpdatePackageLevelInitialized = true;
			}
		}

		private PackageLevel _updatePackageLevel;
		private bool _isUpdatePackageLevelInitialized;

		public TimeSpan UpdateCheckingPeriod;
		
		public string ServiceName;
		public string ExeName { get; set; }

		public Action<string, string> Update;

		public bool CopyContent = true;

		public void OnUpdate(string stagingPath, string targetPath)
		{
			if (Update != null)
				Update(stagingPath, targetPath);
		}

		public void Prepare()
		{
			if (string.IsNullOrEmpty(NugetAppName) || string.IsNullOrEmpty(ServiceName) || string.IsNullOrEmpty(ExeName))
				throw new ApplicationException("Invalid updating info");

			if (string.IsNullOrEmpty(NugetServerUrl))
				NugetServerUrl = Settings.NugetServerUrl;

			if (UpdateCheckingPeriod.Ticks == 0)
				UpdateCheckingPeriod = Settings.UpdateCheckingPeriod;

			if (!_isUpdatePackageLevelInitialized)
				_updatePackageLevel = Settings.UpdatePackageLevel;
		}
	}
}