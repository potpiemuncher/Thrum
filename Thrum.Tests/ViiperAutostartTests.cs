using System;
using System.Collections.Generic;
using System.Linq;
using DS4Windows;

namespace DS4WindowsTests;

/// <summary>
/// Detection of VIIPER's own logon entries.
///
/// <para>These entries are not ours. The <c>Run</c> value is written by
/// <c>viiper.exe install</c> and the <c>RunVIIPER</c> task by the VIIPER setup
/// script, and either may belong to a separate VIIPER install the user set up
/// deliberately. So the tests below assert two things in equal measure: that a
/// present entry is reported, and that nothing is ever removed except by an
/// explicit call naming exactly what to remove.</para>
///
/// <para>Everything runs against a fake source. Neither mechanism exists on the
/// development machine, and creating one there in order to test deleting it
/// would be writing autostart entries onto somebody's PC to prove we can
/// delete them.</para>
/// </summary>
[TestClass]
public class ViiperAutostartTests
{
    private sealed class FakeAutostartSource : IViiperAutostartSource
    {
        public string RunValue { get; set; }

        public string ScheduledTask { get; set; }

        public Exception RunValueError { get; set; }

        public Exception ScheduledTaskError { get; set; }

        public Exception DeleteError { get; set; }

        public int RunValueDeletions { get; private set; }

        public int ScheduledTaskDeletions { get; private set; }

        public string ReadRunValue() =>
            RunValueError != null ? throw RunValueError : RunValue;

        public string ReadScheduledTask() =>
            ScheduledTaskError != null ? throw ScheduledTaskError : ScheduledTask;

        public void DeleteRunValue()
        {
            RunValueDeletions++;
            if (DeleteError != null)
            {
                throw DeleteError;
            }

            RunValue = null;
        }

        public void DeleteScheduledTask()
        {
            ScheduledTaskDeletions++;
            if (DeleteError != null)
            {
                throw DeleteError;
            }

            ScheduledTask = null;
        }
    }

    /// <summary>
    /// The state on the maintainer's machine, and the state the Phase 5
    /// installer has to preserve: VIIPER is on-demand only.
    /// </summary>
    [TestMethod]
    public void NoEntriesMeansTheBackendIsOnDemandOnly()
    {
        ViiperAutostartStatus status =
            ViiperAutostart.Inspect(new FakeAutostartSource());

        Assert.IsFalse(status.Any);
        Assert.AreEqual(0, status.Entries.Count);
        StringAssert.Contains(status.DisplayText, "does not start at logon");
    }

    [TestMethod]
    public void TheRunValueIsDetectedAndItsTargetReported()
    {
        FakeAutostartSource source = new FakeAutostartSource
        {
            RunValue = "\"C:\\VIIPER\\viiper.exe\" server --log.file \"C:\\VIIPER\\viiper.log\"",
        };

        ViiperAutostartStatus status = ViiperAutostart.Inspect(source);

        Assert.IsTrue(status.Any);
        ViiperAutostartEntry entry = status.Entries.Single();
        Assert.AreEqual(ViiperAutostartKind.RegistryRunValue, entry.Kind);
        Assert.AreEqual("VIIPER", entry.Name);
        StringAssert.Contains(entry.Target, "viiper.exe");
    }

    [TestMethod]
    public void TheLogonTaskIsDetected()
    {
        FakeAutostartSource source = new FakeAutostartSource
        {
            ScheduledTask = "C:\\VIIPER\\viiper.exe server",
        };

        ViiperAutostartStatus status = ViiperAutostart.Inspect(source);

        ViiperAutostartEntry entry = status.Entries.Single();
        Assert.AreEqual(ViiperAutostartKind.ScheduledTask, entry.Kind);
        Assert.AreEqual("RunVIIPER", entry.Name);
    }

    [TestMethod]
    public void BothMechanismsAreReportedTogether()
    {
        FakeAutostartSource source = new FakeAutostartSource
        {
            RunValue = "\"C:\\VIIPER\\viiper.exe\" server",
            ScheduledTask = "C:\\VIIPER\\viiper.exe server",
        };

        ViiperAutostartStatus status = ViiperAutostart.Inspect(source);

        Assert.AreEqual(2, status.Entries.Count);
        StringAssert.Contains(status.DisplayText, "starts at logon");
    }

    /// <summary>
    /// An empty string is what a cleared registry value reads back as. It is
    /// not an autostart entry, and reporting it as one would put a removal
    /// button in front of the user with nothing behind it.
    /// </summary>
    [TestMethod]
    public void AnEmptyValueIsNotAnEntry()
    {
        ViiperAutostartStatus status = ViiperAutostart.Inspect(
            new FakeAutostartSource { RunValue = "   ", ScheduledTask = string.Empty });

        Assert.IsFalse(status.Any);
    }

    /// <summary>
    /// A lookup that throws is not evidence of absence. Reporting "no autostart"
    /// after failing to look is the one wrong answer here.
    /// </summary>
    [TestMethod]
    public void ALookupThatFailsIsReportedRatherThanReadAsAbsence()
    {
        ViiperAutostartStatus status = ViiperAutostart.Inspect(
            new FakeAutostartSource
            {
                RunValueError = new UnauthorizedAccessException("denied"),
            });

        Assert.IsFalse(status.Any);
        StringAssert.Contains(status.InspectionError, "registry");
        StringAssert.Contains(status.DisplayText, "could not be checked");
        Assert.IsFalse(status.DisplayText.Contains("does not start at logon"),
            "An unreadable entry must never be presented as an absent one.");
    }

    [TestMethod]
    public void OneFailedLookupDoesNotHideTheOtherEntry()
    {
        ViiperAutostartStatus status = ViiperAutostart.Inspect(
            new FakeAutostartSource
            {
                RunValueError = new InvalidOperationException("hive unavailable"),
                ScheduledTask = "C:\\VIIPER\\viiper.exe server",
            });

        Assert.AreEqual(1, status.Entries.Count);
        Assert.AreEqual(ViiperAutostartKind.ScheduledTask, status.Entries[0].Kind);
        StringAssert.Contains(status.InspectionError, "registry");
    }

    // ---- Removal is explicit, scoped, and never implicit ----------------

    /// <summary>
    /// The property that matters most: looking does not delete.
    /// </summary>
    [TestMethod]
    public void InspectionNeverRemovesAnything()
    {
        FakeAutostartSource source = new FakeAutostartSource
        {
            RunValue = "\"C:\\VIIPER\\viiper.exe\" server",
            ScheduledTask = "C:\\VIIPER\\viiper.exe server",
        };

        ViiperAutostart.Inspect(source);
        ViiperAutostart.Inspect(source);

        Assert.AreEqual(0, source.RunValueDeletions);
        Assert.AreEqual(0, source.ScheduledTaskDeletions);
    }

    [TestMethod]
    public void RemovalTouchesOnlyTheEntriesItIsGiven()
    {
        FakeAutostartSource source = new FakeAutostartSource
        {
            RunValue = "\"C:\\VIIPER\\viiper.exe\" server",
            ScheduledTask = "C:\\VIIPER\\viiper.exe server",
        };
        ViiperAutostartStatus status = ViiperAutostart.Inspect(source);
        ViiperAutostartEntry onlyTheRunValue = status.Entries.Single(
            entry => entry.Kind == ViiperAutostartKind.RegistryRunValue);

        ViiperAutostart.Remove(new[] { onlyTheRunValue }, source);

        Assert.AreEqual(1, source.RunValueDeletions);
        Assert.AreEqual(0, source.ScheduledTaskDeletions,
            "Removing one mechanism must not take the other with it.");
        Assert.IsTrue(ViiperAutostart.Inspect(source).Entries
            .All(entry => entry.Kind == ViiperAutostartKind.ScheduledTask));
    }

    [TestMethod]
    public void RemovingNothingDoesNothing()
    {
        FakeAutostartSource source = new FakeAutostartSource
        {
            RunValue = "\"C:\\VIIPER\\viiper.exe\" server",
        };

        Assert.AreEqual(0, ViiperAutostart.Remove(null, source).Count);
        Assert.AreEqual(0,
            ViiperAutostart.Remove(Array.Empty<ViiperAutostartEntry>(), source).Count);
        Assert.AreEqual(0, source.RunValueDeletions);
    }

    /// <summary>
    /// The <c>RunVIIPER</c> task is registered with <c>RunLevel Highest</c> by
    /// an elevated setup script, so deleting it can fail for a non-elevated
    /// user. That has to reach the user as a sentence, not as an unhandled
    /// exception out of a click handler.
    /// </summary>
    [TestMethod]
    public void AFailedRemovalIsReportedRatherThanThrown()
    {
        FakeAutostartSource source = new FakeAutostartSource
        {
            ScheduledTask = "C:\\VIIPER\\viiper.exe server",
            DeleteError = new UnauthorizedAccessException("needs elevation"),
        };
        ViiperAutostartEntry entry = new ViiperAutostartEntry(
            ViiperAutostartKind.ScheduledTask, "RunVIIPER", "viiper.exe server");

        IReadOnlyList<string> outcomes =
            ViiperAutostart.Remove(new[] { entry }, source);

        StringAssert.Contains(outcomes.Single(), "could not be removed");
        StringAssert.Contains(outcomes.Single(), "needs elevation");
    }

    // ---- Names are the backend's, not ours ------------------------------

    /// <summary>
    /// These strings identify somebody else's entries, which is exactly why
    /// they must not be derived from <see cref="ProductInfo"/>: our own logon
    /// task is <c>RunThrum</c>, and the two must never converge.
    /// </summary>
    [TestMethod]
    public void TheEntryNamesAreViipersOwnAndDistinctFromOurs()
    {
        Assert.AreEqual("VIIPER", ViiperAutostart.RunValueName);
        Assert.AreEqual("RunVIIPER", ViiperAutostart.ScheduledTaskName);
        Assert.AreEqual(@"Software\Microsoft\Windows\CurrentVersion\Run",
            ViiperAutostart.RunKeyPath);
        Assert.AreNotEqual(ProductInfo.StartupTaskName,
            ViiperAutostart.ScheduledTaskName,
            "Sharing a task name would let either product delete the other's entry.");
    }
}
