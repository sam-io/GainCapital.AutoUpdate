using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GainCapital.AutoUpdate.Updater;
using NUnit.Framework;

namespace GainCapital.AutoUpdate.Tests
{
    [TestFixture]
    public class JunctionPointTests
    {

        [Test]
        public static void TestCreateJunctionPoint()
        {
            var rootDir = Environment.CurrentDirectory;
            var folderToJunction = Path.Combine(rootDir, "TestFolder");
            var junctionFolder = Path.Combine(rootDir, "TestJunctionFolder");
            
            if (Directory.Exists(folderToJunction))
            {
                Directory.Delete(folderToJunction, true);                
            }

            if (Directory.Exists(junctionFolder))            
                Directory.Delete(junctionFolder);

            Directory.CreateDirectory(folderToJunction);
            File.WriteAllText(Path.Combine(folderToJunction, "TestFile.txt"), "Test file");

            JunctionPoint.Create(junctionFolder, folderToJunction);

            Assert.That(Directory.Exists(junctionFolder), "Junction not created");
            Assert.That(File.Exists(Path.Combine(junctionFolder, "TestFile.txt")), "File not found in junction");
        }
    }
}
