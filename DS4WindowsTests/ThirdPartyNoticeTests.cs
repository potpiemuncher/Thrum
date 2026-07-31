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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DS4WindowsTests;

/// <summary>
/// Keeps <c>NOTICE.txt</c> honest about what the product actually ships.
///
/// <para>The 2026-07-31 audit found the inherited notice was wrong in both
/// directions: it credited Font Awesome, which appears nowhere in the tree, and
/// it omitted every NuGet dependency, both bundled binaries and a 431 KB
/// vendored JavaScript bundle. Neither error is visible from reading the file —
/// only from comparing it against the build inputs. That comparison is what
/// these tests automate.</para>
///
/// <para>They check <b>presence of an entry</b>, not correctness of a licence.
/// A licence identifier can only be verified against the real artifact, which is
/// what <c>docs/dev/third-party-audit.md</c> records. What these tests prevent is
/// the silent case: someone adds a dependency, ships it, and nobody notices the
/// notice was never updated.</para>
/// </summary>
[TestClass]
public class ThirdPartyNoticeTests
{
    private static string notice;
    private static string appCsproj;
    private static string repoRoot;

    [ClassInitialize]
    public static void LoadFiles(TestContext context)
    {
        repoRoot = FindRepoRoot();
        Assert.IsNotNull(repoRoot,
            "could not locate the repository root above " + AppContext.BaseDirectory);
        notice = File.ReadAllText(Path.Combine(repoRoot, "NOTICE.txt"));
        appCsproj = File.ReadAllText(
            Path.Combine(repoRoot, "DS4Windows", "DS4WinWPF.csproj"));
    }

    [TestMethod]
    public void EveryPackageReferenceIsNamedInTheNotice()
    {
        List<string> missing = new List<string>();
        foreach (Match m in Regex.Matches(appCsproj,
            @"<PackageReference\s+Include=""(?<id>[^""]+)"""))
        {
            string id = m.Groups["id"].Value;
            if (!notice.Contains(id, StringComparison.OrdinalIgnoreCase))
            {
                missing.Add(id);
            }
        }

        Assert.AreEqual(0, missing.Count,
            "these NuGet packages are referenced by the application but appear " +
            "nowhere in NOTICE.txt: " + string.Join(", ", missing) +
            ". Add an entry with the licence taken from the package's own " +
            "metadata or bundled licence file, and record the evidence in " +
            "docs/dev/third-party-audit.md.");
    }

    [TestMethod]
    public void EveryBundledBinaryIsNamedInTheNotice()
    {
        string libs = Path.Combine(repoRoot, "DS4Windows", "libs");
        Assert.IsTrue(Directory.Exists(libs), "DS4Windows/libs is missing.");

        // Bundled binaries have no package metadata to fall back on, so an
        // unlisted one is the least discoverable kind of omission.
        List<string> missing = Directory
            .GetFiles(libs, "*.dll", SearchOption.AllDirectories)
            .Select(Path.GetFileNameWithoutExtension)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(name => !notice.Contains(name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.AreEqual(0, missing.Count,
            "these binaries are bundled under DS4Windows/libs but appear nowhere " +
            "in NOTICE.txt: " + string.Join(", ", missing));
    }

    [TestMethod]
    public void TheNoticeStillPointsAtTheFilesThatAreAuthoritative()
    {
        // Three files carry notices this one deliberately does not restate. If
        // one is moved or renamed, the cross-reference has to move with it.
        foreach (string authoritative in new[]
        {
            "DS4Windows/ThirdParty/SbcSharp/LICENSE.txt",
            "DS4Windows/Resources/ControllerArtwork.NOTICE.txt",
            "DS4Windows/Resources/ICONS.NOTICE.txt",
        })
        {
            Assert.IsTrue(File.Exists(Path.Combine(repoRoot,
                    authoritative.Replace('/', Path.DirectorySeparatorChar))),
                authoritative + " is referenced by NOTICE.txt but does not exist.");

            string leaf = authoritative.Substring(authoritative.LastIndexOf('/') + 1);
            Assert.IsTrue(notice.Contains(leaf, StringComparison.Ordinal),
                "NOTICE.txt no longer cross-references " + leaf +
                ", so the notices in that file are now undiscoverable.");
        }
    }

    [TestMethod]
    public void UnresolvedLicensingIsStatedAsReleaseBlocking()
    {
        // The audit left three items without a clean grant. Whoever resolves them
        // deletes the section; until then the file must keep saying so out loud,
        // because a notice that reads as complete when it is not is worse than no
        // notice at all.
        if (!notice.Contains("UNRESOLVED", StringComparison.Ordinal))
        {
            // Section gone: fine, but then nothing may still be marked blocking.
            Assert.IsFalse(notice.Contains("** NO LICENCE", StringComparison.Ordinal),
                "an entry is still flagged as having no licence, but the UNRESOLVED " +
                "section that explains it has been removed.");
            return;
        }

        StringAssert.Contains(notice, "release-blocking",
            "the UNRESOLVED section must say plainly that it blocks a release.");
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NOTICE.txt")) &&
                Directory.Exists(Path.Combine(directory.FullName, "DS4Windows")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
