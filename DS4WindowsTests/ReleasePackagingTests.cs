/*
Thrum
Copyright (C) 2026  Thrum contributors

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace DS4WindowsTests;

/// <summary>
/// Ensures every <c>dotnet publish</c> of <c>DS4WinWPF.csproj</c> in the
/// GitHub Actions workflows ships a self-contained binary.
///
/// <para>Thrum's CI and release workflows currently publish framework-dependent
/// artifacts, so a machine without the .NET 8 Desktop Runtime cannot start
/// <c>Thrum.exe</c> at all. Every publish step must carry <c>--self-contained
/// true</c> and a runtime identifier such as <c>-r win-x64</c>.</para>
/// </summary>
[TestClass]
public class ReleasePackagingTests
{
    /// <summary>Minimum number of publish invocations the guard expects.</summary>
    private const int MinimumPublishCount = 2;

    [TestMethod]
    public void EveryPublishIsSelfContained()
    {
        string repoRoot = FindRepositoryRoot();
        string workflowsDir = Path.Combine(repoRoot, ".github", "workflows");

        if (!Directory.Exists(workflowsDir))
        {
            Assert.IsTrue(false,
                workflowsDir + " does not exist; no workflows to guard.");
            return;
        }

        // Collect (file, line, content) for every dotnet publish on DS4WinWPF.csproj.
        List<(string file, int line, string content)> publishes = new();

        foreach (string ymlFile in Directory.GetFiles(workflowsDir, "*.yml"))
        {
            string[] lines = File.ReadAllLines(ymlFile);
            for (int i = 0; i < lines.Length; i++)
            {
                // Only publish invocations. A plain `dotnet build` or
                // `dotnet test` line that mentions the project must not be
                // held to publish flags - demanding --self-contained on a
                // build command would be a false positive the first time
                // someone adds one.
                if (Regex.IsMatch(lines[i], @"dotnet\s+publish") &&
                    Regex.IsMatch(lines[i], @"DS4WinWPF\.csproj"))
                {
                    publishes.Add((Path.GetFileName(ymlFile), i + 1, lines[i]));
                }
            }
        }

        // Self-check: a guard that passes on zero publish steps is meaningless.
        Assert.IsTrue(publishes.Count >= MinimumPublishCount,
            "This guard inspected " + publishes.Count +
            " publish invocation(s) but expected at least " +
            MinimumPublishCount + ". A passing guard on an empty set is " +
            "worthless - verify the workflow file list is correct.");

        List<string> failures = new();

        foreach ((string file, int line, string content) in publishes)
        {
            // Check for --self-contained true.
            if (!Regex.IsMatch(content, @"--self-contained\s+true"))
            {
                failures.Add(file + " line " + line + ": " +
                    "dotnet publish of DS4WinWPF.csproj is missing " +
                    "--self-contained true. The artifact will not start on a " +
                    "machine without the .NET Desktop Runtime.");
            }

            // Check for -r win-x64 or -r win-${{ matrix.platform }}.
            if (!Regex.IsMatch(content, @"-r\s+win-x64") &&
                !Regex.IsMatch(content, @"-r\s+win-\$\{\{\s*matrix\.platform\s*\}\}"))
            {
                failures.Add(file + " line " + line + ": " +
                    "dotnet publish of DS4WinWPF.csproj is missing a runtime " +
                    "identifier (-r win-x64 or -r win-${{ matrix.platform }}). " +
                    "The artifact will not start on a machine without the .NET " +
                    "Desktop Runtime, which is exactly the state both validation " +
                    "passes had to work around.");
            }
        }

        if (failures.Count > 0)
        {
            Assert.IsTrue(false,
                "Release packaging guard found " + failures.Count +
                " issue(s) across " + publishes.Count +
                " publish invocation(s):\n" +
                string.Join("\n", failures));
        }
    }

    [TestMethod]
    public void PostBuildPackagesLegalFilesAndVerifiesTheArchive()
    {
        string repoRoot = FindRepositoryRoot();
        string scriptPath = Path.Combine(repoRoot, "utils", "post-build.py");
        Assert.IsTrue(File.Exists(scriptPath),
            scriptPath + " does not exist; release packaging cannot be guarded.");

        string script = File.ReadAllText(scriptPath);
        int copyCall = script.LastIndexOf("copy_required_package_root_files()",
            StringComparison.Ordinal);
        int manifestBuild = script.IndexOf("managed_files = sorted(",
            StringComparison.Ordinal);
        int archiveMove = script.IndexOf("shutil.move(zip_dir, target_zip_path)",
            StringComparison.Ordinal);
        int verifyCall = script.LastIndexOf(
            "verify_release_archive(target_zip_path)",
            StringComparison.Ordinal);

        StringAssert.Contains(script,
            @"required_package_root_files = (""NOTICE.txt"", ""COPYING"")");
        Assert.IsTrue(copyCall >= 0 && manifestBuild >= 0 &&
            copyCall < manifestBuild,
            "post-build.py must copy NOTICE.txt and COPYING before generating " +
            "the updater managed-files manifest.");
        Assert.IsTrue(archiveMove >= 0 && verifyCall > archiveMove,
            "post-build.py must verify the completed ZIP after moving it to " +
            "the release output directory.");
        StringAssert.Contains(script,
            @"archive.read(manifest_entry).decode(""utf-8"")");
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName,
                "DS4WindowsWPF.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
