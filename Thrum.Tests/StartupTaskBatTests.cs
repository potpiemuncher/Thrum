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

using DS4Windows;
using DS4WinWPF;
using System;
using System.IO;
using System.Text;

namespace DS4WindowsTests;

/// <summary>
/// Covers the helper batch file the logon scheduled task runs.
///
/// <para>Two properties, from the two halves of issue #9. First, turning the
/// startup option off has to remove the file as well as the task — it is an
/// executable launcher we generated, and leaving it behind is residue no
/// setting accounts for. Second, and less obviously, the path has to be spelled
/// exactly once.</para>
///
/// <para>The spelling matters because <c>Global.exedirpath</c> comes from
/// <c>DirectoryInfo.FullName</c>, which keeps its trailing separator at a drive
/// root. <c>$@"{dir}\task.bat"</c> and <c>Path.Combine(dir, "task.bat")</c>
/// agree everywhere except there, and where they disagree
/// <c>DeleteOldTaskEntry</c> compares the registered action against a string it
/// can never match and deletes a healthy task on every settings load.</para>
/// </summary>
[TestClass]
public class StartupTaskBatTests
{
    private string tempDir;

    [TestInitialize]
    public void CreateTempDir()
    {
        tempDir = Path.Combine(Path.GetTempPath(),
            "thrum-taskbat-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
    }

    [TestCleanup]
    public void RemoveTempDir()
    {
        try
        {
            foreach (string file in Directory.EnumerateFiles(tempDir))
            {
                new FileInfo(file).IsReadOnly = false;
            }

            Directory.Delete(tempDir, true);
        }
        catch (Exception)
        {
            // A leaked temp directory must not fail a green test run.
        }
    }

    private string WriteBat(string name = "task.bat")
    {
        string path = Path.Combine(tempDir, name);
        File.WriteAllText(path, "@echo off\r\nexit\r\n");
        return path;
    }

    [TestMethod]
    public void DeleteTaskBatRemovesTheFile()
    {
        string path = WriteBat();

        Assert.IsTrue(StartupMethods.DeleteTaskBat(path));
        Assert.IsFalse(File.Exists(path),
            "the orphan issue #9 reported is still reproducible");
    }

    [TestMethod]
    public void AnAbsentFileCountsAsSuccess()
    {
        // The result reports "the file is gone", not "a delete happened".
        // Disabling startup twice must not look like a failure the second time.
        string path = Path.Combine(tempDir, "never-created.bat");

        Assert.IsTrue(StartupMethods.DeleteTaskBat(path));
    }

    [TestMethod]
    public void AReadOnlyAttributeIsClearedRatherThanRespected()
    {
        // We wrote this file, so read-only on it is our own residue. That is
        // the opposite of the shortcut, which DeleteStartProgEntry leaves alone
        // when read-only because a user may have set that deliberately.
        string path = WriteBat();
        FileInfo info = new FileInfo(path);
        info.IsReadOnly = true;

        Assert.IsTrue(StartupMethods.DeleteTaskBat(path));
        Assert.IsFalse(File.Exists(path));
    }

    [TestMethod]
    public void ALockedFileFailsWithoutThrowing()
    {
        // Reachable for real: the logon task may be running the batch file, and
        // cmd.exe holds it open while it executes.
        string path = WriteBat();

        using (new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.None))
        {
            Assert.IsFalse(StartupMethods.DeleteTaskBat(path),
                "a locked file must be reported as still present");
        }

        Assert.IsTrue(File.Exists(path), "the lock should have prevented this");
    }

    [TestMethod]
    public void APathThatNamesNothingIsAFailureNotASuccess()
    {
        // A path we cannot even evaluate must not be reported as "the file is
        // gone" - that would be the same "could not look" / "looked and saw
        // nothing" conflation the driver gate and the diagnostics collector
        // both refuse to make.
        Assert.IsFalse(StartupMethods.DeleteTaskBat(null));
        Assert.IsFalse(StartupMethods.DeleteTaskBat(string.Empty));
        Assert.IsFalse(StartupMethods.DeleteTaskBat("   "));
    }

    [TestMethod]
    public void AFileUnderAMissingDirectoryIsAlreadyGone()
    {
        // Distinct from the case above: this path is well formed, and the file
        // it names genuinely is not there.
        Assert.IsTrue(StartupMethods.DeleteTaskBat(
            Path.Combine(tempDir, "no", "such", "directory", "task.bat")));
    }

    [TestMethod]
    public void NoInputMakesTheDisablePathThrow()
    {
        // Nothing should be able to turn "switch startup off" into an exception
        // dialog. By the time this runs the scheduled task - the part that
        // actually launches the app - is already gone, so the user's intent has
        // been honoured whatever happens to the file.
        foreach (string candidate in new[]
        {
            null, string.Empty, "   ", "\0", "??invalid|chars?.bat",
            @"\\?\nonexistent-unc\share\task.bat",
            new string('x', 400) + ".bat",
        })
        {
            StartupMethods.DeleteTaskBat(candidate);
        }
    }

    [TestMethod]
    public void TheTaskBatPathIsBuiltFromTheExecutableDirectory()
    {
        Assert.AreEqual(
            Path.Combine(Global.exedirpath, "task.bat"),
            StartupMethods.TaskBatPath);
    }

    [TestMethod]
    public void TheTaskBatPathHasNoDoubledSeparator()
    {
        // The drive-root case, asserted directly. Path.Combine cannot produce
        // this; the interpolated spelling can.
        Assert.IsFalse(
            StartupMethods.TaskBatPath.Contains(@"\\", StringComparison.Ordinal),
            "the task.bat path has a doubled separator, which means it was " +
            "composed by concatenation rather than Path.Combine: " +
            StartupMethods.TaskBatPath);
    }

    /// <summary>
    /// The load-bearing guard, in the same style as
    /// <c>StartupEntryIdentityTests.NoCodePathInTheApplicationCanNameTheInheritedStartupEntries</c>:
    /// scan the compiled application rather than trusting a reviewer to notice.
    ///
    /// <para>The two spellings leave different literals behind.
    /// <c>Path.Combine(dir, "task.bat")</c> puts <c>task.bat</c> in the
    /// metadata string heap. <c>$@"{dir}\task.bat"</c> puts
    /// <c>\task.bat</c> there — separator included — whichever way the compiler
    /// lowers the interpolation. So the separator-prefixed form appearing
    /// anywhere in the assembly means a second spelling came back.</para>
    /// </summary>
    [TestMethod]
    public void NoCodePathComposesTheTaskBatPathByConcatenation()
    {
        string[] haystacks = ApplicationTextImages();

        Assert.IsFalse(ContainsText(haystacks, @"\task.bat"),
            "something in the application composes the task.bat path with a " +
            "literal separator instead of Path.Combine. That reintroduces the " +
            "drive-root mismatch in DeleteOldTaskEntry described on " +
            "StartupMethods.TaskBatPath. Use StartupMethods.TaskBatPath.");

        // Negative control: the scan has to be able to find the bare filename,
        // or its failure to find the prefixed form proves nothing.
        Assert.IsTrue(ContainsText(haystacks, "task.bat"),
            "the scan could not find the task.bat literal at all, so it " +
            "cannot be trusted to detect the concatenated spelling either.");
    }

    private static string[] ApplicationTextImages()
    {
        string location = typeof(Global).Assembly.Location;
        Assert.IsFalse(string.IsNullOrEmpty(location),
            "the application assembly has no file on disk to scan");
        Assert.IsTrue(File.Exists(location), location);

        byte[] bytes = File.ReadAllBytes(location);
        Assert.IsTrue(bytes.Length > 0);

        // String literals live in the #US heap as UTF-16 behind a
        // variable-length prefix, so a literal can begin at an odd offset.
        return new[]
        {
            Encoding.Unicode.GetString(bytes, 0, bytes.Length & ~1),
            Encoding.Unicode.GetString(bytes, 1, (bytes.Length - 1) & ~1),
        };
    }

    private static bool ContainsText(string[] haystacks, string needle)
    {
        foreach (string haystack in haystacks)
        {
            if (haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }
}
