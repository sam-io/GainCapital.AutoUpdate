using System;
using System.IO;
using System.Linq;
using System.Net;
using NuGet;

namespace GainCapital.AutoUpdate.Updater
{
    public class UpdatePackages
    {
        private readonly IPackageRepository _packageRepository;

        public UpdatePackages(string updateUrl)
        {
            _packageRepository = PackageRepositoryFactory.Default.CreateRepository(updateUrl);
        }

        public IPackage GetLastPackage(string packageId, PackageLevel packageLevel)
        {
            Logger.LogInfo(string.Format("Getting packages for {0}", packageId));
            var packages = _packageRepository.FindPackagesById(packageId).ToList();

            Logger.LogInfo(string.Format("Found {0} packages for {1}", packages.Count, packageId));

            packages.RemoveAll(val => !val.IsListed());

            if (packageLevel > PackageLevel.Beta)
                packages.RemoveAll(val => val.Version.SpecialVersion.ToLowerInvariant() == "beta");

            if (packageLevel > PackageLevel.RC)
                packages.RemoveAll(val => val.Version.SpecialVersion.ToLowerInvariant() == "rc");

            if (packages.Count == 0)
                throw new ApplicationException("No update package is found");

            packages.Sort((x, y) => x.Version.CompareTo(y.Version));
            var res = packages.Last();
            Logger.LogInfo(string.Format("Latest package is {0}", res.Version));

            return res;
        }

        public string Download(IPackage package, string installPath)
        {
            var updateDataPath = Path.Combine(installPath, "UpdateData");

            var packageManager = new PackageManager(_packageRepository, updateDataPath);
            packageManager.InstallPackage(package.Id, package.Version, true, false);

            var packagePath = Path.Combine(updateDataPath, package.Id + "." + package.Version);
            var updateDeploymentPath = Path.Combine(installPath, "v" + package.Version.Version);
            var packageBinPath = Path.Combine(packagePath, "lib");

            if (Directory.Exists(updateDeploymentPath))
                Directory.Delete(updateDeploymentPath, true);

            FileSystem.FromDirectory(packageBinPath)
                .IncludeFilesRecursive("*.exe")
                .IncludeFilesRecursive("*.dll")
                .IncludeFilesRecursive("*.pdb")
                .IncludeFilesRecursive("*.xml")
                .Flatten(updateDeploymentPath);

            var environmentType = Environment.GetEnvironmentVariable("EnvironmentName", EnvironmentVariableTarget.Machine);
            if (string.IsNullOrEmpty(environmentType))
                environmentType = "local";

            var configPath = Path.Combine(packagePath, @"content\net45\Config", environmentType, "Apps", package.Title, "PKH-QAT-APP01");
            Logger.LogInfo(string.Format("Copying config from {0}", configPath));
            FileSystem.FromDirectory(configPath)
                .IncludeFiles("*.config")
                .Flatten(updateDeploymentPath);

            return updateDeploymentPath;
        }

    }
}
