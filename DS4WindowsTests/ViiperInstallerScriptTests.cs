using System;
using System.IO;
using System.Text.RegularExpressions;
using DS4Windows;

namespace DS4WindowsTests;

/// <summary>
/// Structural guards on <c>extras/install-viiper-backend.ps1</c>.
///
/// <para><b>These are a backstop, and they are the weakest tests in this change
/// set.</b> Matching text in a script proves the text is there, not that the
/// script behaves. The real coverage of the fail-closed logic is
/// <see cref="ViiperInstallerPolicyTests"/>, which exercises the actual
/// decision functions; the script was deliberately reduced to orchestration so
/// that almost nothing decidable is left in it.</para>
///
/// <para>What is left in it is four properties that cannot be expressed
/// anywhere else, because they are properties of the absence of code: that no
/// autostart entry is created, that no backend is started without the update
/// notifier disabled, that no URL or digest is written down outside the pins,
/// and that the rollback backup is not deleted on success. A regression in any
/// of those is silent — the script keeps working, it just stops being safe —
/// so a crude check that fails loudly is worth more than no check at all.</para>
///
/// <para>Running the script itself is [VM]-gated: it installs a kernel driver.
/// See the plan's Part 3 rule 1.</para>
/// </summary>
[TestClass]
public class ViiperInstallerScriptTests
{
    private static string script;

    [ClassInitialize]
    public static void LoadScript(TestContext context)
    {
        string path = FindScript();
        Assert.IsNotNull(path,
            "extras/install-viiper-backend.ps1 was not found above " +
            AppContext.BaseDirectory + ".");
        script = File.ReadAllText(path);
    }

    [TestMethod]
    public void TheScriptLooksForTheExecutableThisBuildActuallyProduces()
    {
        // The script cannot read a C# constant, so this coupling is the one
        // place ProductInfo and the script have to be kept in step by hand.
        StringAssert.Contains(script,
            "$script:DefaultAppExecutableName = \"" +
            ProductInfo.ExeBaseName + ".exe\"");
    }

    [TestMethod]
    public void TheScriptCreatesNoAutostartEntry()
    {
        // Both mechanisms, gone: the RunVIIPER logon task and the HKCU Run
        // value that "viiper.exe install" writes. Thrum starts the backend when
        // a profile needs it and stops it on exit (plan task 2.4b), so a logon
        // entry would start a backend the application never owns and never
        // stops.
        foreach (string forbidden in new[]
        {
            "Register-ScheduledTask",
            "New-ScheduledTaskAction",
            "New-ScheduledTaskTrigger",
            "New-ScheduledTaskPrincipal",
            "schtasks",
            "ArgumentList \"install\"",
            "New-ItemProperty",
            "Set-ItemProperty",
            "CurrentVersion\\Run\\",
        })
        {
            Assert.IsFalse(script.Contains(forbidden, StringComparison.OrdinalIgnoreCase),
                "the setup script must not contain '" + forbidden + "'.");
        }
    }

    [TestMethod]
    public void TheTaskNameAppearsOnlyWhereRemovalIsDocumented()
    {
        // The name is allowed to be written down — the script has to be able to
        // say what -RemoveViiperAutostart removes. It is not allowed to appear
        // anywhere a command could act on it, which the creation-verb ban above
        // covers; this pins the remaining occurrence so a registration cannot
        // be reintroduced under cover of the documentation.
        MatchCollection matches = Regex.Matches(script, ".*RunVIIPER.*");
        Assert.AreEqual(1, matches.Count,
            "RunVIIPER may appear once, in the -RemoveViiperAutostart help.");
        StringAssert.Contains(matches[0].Value, "task). Without this");
    }

    [TestMethod]
    public void TheScriptOffersToRemoveAnAutostartEntryItDidNotCreate()
    {
        StringAssert.Contains(script, "[switch]$RemoveViiperAutostart");
        StringAssert.Contains(script, "$autostartArgs += \"--remove\"");
    }

    [TestMethod]
    public void EveryBackendStartDisablesTheUpdateNotifier()
    {
        // Issue #8's remaining half. The argument vector comes from the same
        // constant the application spawns with, so the script cannot drift from
        // it, and a literal "server" would be exactly that drift.
        StringAssert.Contains(script, "-ArgumentList $serverArgs");
        Assert.IsFalse(script.Contains("-ArgumentList \"server\"",
            StringComparison.Ordinal),
            "the backend must never be started with a hand-written argument list.");
        StringAssert.Contains(script, "$pins['viiper.serverargs']");
    }

    [TestMethod]
    public void TheScriptResolvesNoReleaseAndHardCodesNoArtefactIdentity()
    {
        foreach (string forbidden in new[]
        {
            "api.github.com",
            "github.com",
            "per_page",
            "browser_download_url",
            "-ge $requiredUsbipVersion",
        })
        {
            Assert.IsFalse(script.Contains(forbidden, StringComparison.OrdinalIgnoreCase),
                "the setup script must not contain '" + forbidden +
                "': URLs, digests and admissible versions come from the pins.");
        }
    }

    [TestMethod]
    public void NothingIsExecutedBeforeItHasBeenVerified()
    {
        int verified = script.IndexOf("Get-VerifiedPinnedFile \"usbip\"",
            StringComparison.Ordinal);
        int executed = script.IndexOf("-ArgumentList \"/S\"",
            StringComparison.Ordinal);

        Assert.IsTrue(verified >= 0, "the driver installer must be verified.");
        Assert.IsTrue(executed >= 0, "the driver installer is still run silently.");
        Assert.IsTrue(verified < executed,
            "the pinned installer must be verified before it is executed.");

        Assert.AreEqual(1, Regex.Matches(script,
            Regex.Escape("-ArgumentList \"/S\"")).Count,
            "there must be exactly one place the driver installer is run.");
    }

    [TestMethod]
    public void TheRollbackBackupSurvivesASuccessfulInstall()
    {
        // Upstream deletes viiper.exe.previous once the API answers, which
        // leaves rollback available only inside the install window. The failure
        // this protects against is a backend that installs cleanly and then
        // misbehaves.
        Assert.IsFalse(Regex.IsMatch(script,
            @"Remove-Item\s+-LiteralPath\s+\$backupPath"),
            "the .previous backup must be kept after a successful install.");
        StringAssert.Contains(script, "was kept at $backupPath for rollback");
    }

    [TestMethod]
    public void AMissingOrUnusableVerificationHelperStopsSetup()
    {
        // Fail-closed: no helper, no verification, no install. Silence is never
        // read as approval.
        StringAssert.Contains(script, "rather than installing anything unverified");
        StringAssert.Contains(script, "produced no result");
        StringAssert.Contains(script, "does not match its exit code");
    }

    [TestMethod]
    public void AnUnrecognisedPolicyActionStopsSetup()
    {
        StringAssert.Contains(script, "unrecognised usbip-win2 ");
        StringAssert.Contains(script, "rather than guessing");
    }

    [TestMethod]
    public void AStagedFileReplacesTheDownloadAndNothingElse()
    {
        // The VM run sheet's negative cases stage a corrupted or wrongly-signed
        // artefact. That has to travel the real path, so the staged file is
        // handed to the same verifier by the same call — there is no second,
        // laxer branch for it.
        StringAssert.Contains(script, "[string]$UsbipInstallerFile");
        StringAssert.Contains(script, "[string]$ViiperBackendFile");
        Assert.AreEqual(2, Regex.Matches(script,
            Regex.Escape("Get-VerifiedPinnedFile \"")).Count,
            "both components must be fetched through the verifying helper.");
        Assert.AreEqual(1, Regex.Matches(script,
            @"\$verification\s*=\s*Invoke-InstallerPolicy").Count,
            "there must be exactly one verification call site, shared by the " +
            "download path and the staged-file path.");
    }

    private static string FindScript()
    {
        DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            string candidate = Path.Combine(directory.FullName, "extras",
                "install-viiper-backend.ps1");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
