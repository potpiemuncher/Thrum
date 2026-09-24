using System;
using System.IO;
using System.Text.RegularExpressions;
using DS4WinWPF.DS4Control.DTOXml;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DS4WindowsTests;

/// <summary>
/// Every <c>new XmlSerializer(typeof(ProfileDTO), overrides)</c> generates an
/// assembly that .NET never unloads, so the profile load and save paths must
/// share one serializer rather than build one per call (each profile switch
/// used to leak one).
/// </summary>
[TestClass]
public class ProfileSerializerCacheTests
{
    [TestMethod]
    public void TheProfileSerializerIsBuiltOnce()
    {
        Assert.AreSame(ProfileDTO.SharedSerializer, ProfileDTO.SharedSerializer);
    }

    [TestMethod]
    public void TheAppNeverBuildsAProfileSerializerPerCall()
    {
        string appRoot = FindAppRoot();
        Assert.IsNotNull(appRoot, "Thrum/ source folder was not found above " +
            AppContext.BaseDirectory + ".");

        foreach (string file in Directory.EnumerateFiles(appRoot, "*.cs",
            SearchOption.AllDirectories))
        {
            if (file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) ||
                file.EndsWith("ProfileDTO.cs", StringComparison.Ordinal))
            {
                continue;
            }

            string source = File.ReadAllText(file);
            Assert.IsFalse(Regex.IsMatch(source,
                    @"new\s+XmlSerializer\s*\(\s*typeof\s*\(\s*ProfileDTO\s*\)\s*,"),
                Path.GetFileName(file) + " builds a ProfileDTO serializer with " +
                "overrides; use ProfileDTO.SharedSerializer.");
        }
    }

    private static string FindAppRoot()
    {
        DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            string candidate = Path.Combine(directory.FullName, "Thrum",
                "DS4Control", "DTOXml", "ProfileDTO.cs");
            if (File.Exists(candidate))
            {
                return Path.Combine(directory.FullName, "Thrum");
            }

            directory = directory.Parent;
        }

        return null;
    }
}
