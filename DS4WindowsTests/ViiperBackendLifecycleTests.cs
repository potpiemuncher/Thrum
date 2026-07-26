using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using DS4Windows;
using DS4WinWPF.DS4Control.DTOXml;

namespace DS4WindowsTests;

/// <summary>
/// Guards the managed VIIPER backend's lifecycle: how it is launched, who is
/// allowed to stop it, and on what evidence.
///
/// <para>Two of these are safety properties rather than behaviour checks. The
/// spawn-argument tests exist because the backend's own updater points at the
/// wrong repository and its "Update Now" path runs an unverified remote script
/// elevated (issue #8); the stop-policy tests exist because killing a backend
/// somebody else is using takes their controller away.</para>
/// </summary>
[TestClass]
public class ViiperBackendLifecycleTests
{
    // ---- Spawn arguments: issue #8 -------------------------------------

    /// <summary>
    /// The backend's self-updater must be off in a backend we start. Asserted
    /// on the argument vector rather than on a formatted string so that a
    /// future change to quoting or ordering cannot make this pass vacuously.
    /// </summary>
    [TestMethod]
    public void TheSpawnArgumentsDisableTheBackendsSelfUpdater()
    {
        ProcessStartInfo startInfo =
            ViiperBackendSpawn.BuildServerStartInfo(@"C:\somewhere\viiper.exe");

        int flagIndex = startInfo.ArgumentList.IndexOf("--update-notify");
        Assert.IsTrue(flagIndex >= 0,
            "A backend spawned without --update-notify checks Alia5/VIIPER for " +
            "updates it can never satisfy, and offers an elevated remote script.");
        Assert.AreEqual("none", startInfo.ArgumentList[flagIndex + 1],
            "Only 'none' skips the update check; 'stable' and 'prerelease' both run it.");
    }

    [TestMethod]
    public void TheSpawnEnvironmentDisablesTheBackendsSelfUpdater()
    {
        ProcessStartInfo startInfo =
            ViiperBackendSpawn.BuildServerStartInfo(@"C:\somewhere\viiper.exe");

        Assert.AreEqual("none",
            startInfo.Environment["VIIPER_UPDATE_NOTIFY"],
            "The environment form is what a re-exec of the backend inherits.");
    }

    /// <summary>
    /// The flag is declared on VIIPER's root command, so it precedes the
    /// subcommand. Verified against the real binary during development; pinned
    /// here so a reordering does not go unnoticed.
    /// </summary>
    [TestMethod]
    public void TheUpdateFlagPrecedesTheServerSubcommand()
    {
        ProcessStartInfo startInfo =
            ViiperBackendSpawn.BuildServerStartInfo(@"C:\somewhere\viiper.exe");

        CollectionAssert.AreEqual(
            new[] { "--update-notify", "none", "server" },
            startInfo.ArgumentList.ToArray());
    }

    /// <summary>
    /// <c>CreateNoWindow</c> is load-bearing twice over: it keeps a console
    /// window off the user's screen, and — because it maps to
    /// <c>CREATE_NO_WINDOW</c> — it is what gives the child a console of its
    /// own for the graceful stop to attach to.
    /// </summary>
    [TestMethod]
    public void TheBackendIsSpawnedWindowlessAndWithoutTheShell()
    {
        ProcessStartInfo startInfo =
            ViiperBackendSpawn.BuildServerStartInfo(@"C:\somewhere\viiper.exe");

        Assert.IsTrue(startInfo.CreateNoWindow);
        Assert.IsFalse(startInfo.UseShellExecute,
            "UseShellExecute would discard both the environment and CREATE_NO_WINDOW.");
        Assert.AreEqual(ProcessWindowStyle.Hidden, startInfo.WindowStyle);
        Assert.AreEqual(@"C:\somewhere\viiper.exe", startInfo.FileName);
    }

    [TestMethod]
    public void ABackendPathIsRequired()
    {
        Assert.ThrowsException<ArgumentException>(
            () => ViiperBackendSpawn.BuildServerStartInfo(null));
        Assert.ThrowsException<ArgumentException>(
            () => ViiperBackendSpawn.BuildServerStartInfo(string.Empty));
    }

    // ---- Ownership -----------------------------------------------------

    [TestMethod]
    public void OwnershipIsTheProcessIdAndItsStartTimeTogether()
    {
        DateTime start = new DateTime(2026, 7, 26, 9, 0, 0, DateTimeKind.Local);
        ViiperOwnedBackend owned = new ViiperOwnedBackend(4321, start);

        Assert.IsTrue(owned.Matches(4321, start));
        Assert.IsFalse(owned.Matches(4322, start),
            "A different process id is a different process.");
        Assert.IsFalse(owned.Matches(4321, start.AddSeconds(1)),
            "Windows reuses process ids; the start time is what makes the pair unique.");
    }

    /// <summary>
    /// The reuse guard has to survive the real thing, not just arithmetic: a
    /// record built from a live process must resolve back to that process, and
    /// the same id with a shifted start time must resolve to nothing.
    /// </summary>
    [TestMethod]
    public void AnOwnershipRecordResolvesOnlyToTheProcessItWasTakenFrom()
    {
        using Process self = Process.GetCurrentProcess();
        ViiperOwnedBackend genuine = new ViiperOwnedBackend(self.Id, self.StartTime);

        using Process resolved = genuine.TryResolve();
        Assert.IsNotNull(resolved);
        Assert.AreEqual(self.Id, resolved.Id);

        ViiperOwnedBackend recycledId =
            new ViiperOwnedBackend(self.Id, self.StartTime.AddMinutes(-5));
        Assert.IsNull(recycledId.TryResolve(),
            "A stale record whose id has been reused must not resolve to the new owner.");
    }

    // ---- Stop policy ---------------------------------------------------

    private static ViiperBackendCensus Idle() =>
        ViiperBackendCensus.Success(Array.Empty<uint>(), Array.Empty<ViiperCensusDevice>());

    private static ViiperOwnedBackend SomeBackend() =>
        new ViiperOwnedBackend(1234, new DateTime(2026, 7, 26, 9, 0, 0, DateTimeKind.Local));

    private static ViiperBackendStopDecision Decide(
        bool settingEnabled = true,
        ViiperOwnedBackend owned = null,
        bool alive = true,
        ViiperBackendCensus census = null,
        IReadOnlyCollection<ViiperCensusDevice> ours = null) =>
        ViiperBackendStopPolicy.Decide(settingEnabled, owned ?? SomeBackend(),
            alive, census ?? Idle(), ours ?? Array.Empty<ViiperCensusDevice>());

    [TestMethod]
    public void AnIdleBackendWeStartedIsStopped()
    {
        ViiperBackendStopDecision decision = Decide();

        Assert.IsTrue(decision.ShouldStop, decision.Reason);
        StringAssert.Contains(decision.Reason, "no buses or devices");
    }

    [TestMethod]
    public void ABackendWeDidNotStartIsNeverStopped()
    {
        ViiperBackendStopDecision decision =
            ViiperBackendStopPolicy.Decide(true, null, true, Idle(),
                Array.Empty<ViiperCensusDevice>());

        Assert.IsFalse(decision.ShouldStop);
        StringAssert.Contains(decision.Reason, "not ours to stop");
    }

    [TestMethod]
    public void TheSettingBeingOffOverridesEverythingElse()
    {
        ViiperBackendStopDecision decision = Decide(settingEnabled: false);

        Assert.IsFalse(decision.ShouldStop);
        StringAssert.Contains(decision.Reason, "setting is off");
    }

    [TestMethod]
    public void ABackendThatHasAlreadyExitedIsNotStopped()
    {
        ViiperBackendStopDecision decision = Decide(alive: false);

        Assert.IsFalse(decision.ShouldStop);
        StringAssert.Contains(decision.Reason, "no longer running");
    }

    /// <summary>
    /// The point of the whole exercise: a device we did not create means
    /// somebody else is on this backend.
    /// </summary>
    [TestMethod]
    public void ADeviceWeDidNotCreateIsTreatedAsAnotherConsumer()
    {
        ViiperBackendCensus census = ViiperBackendCensus.Success(
            new uint[] { 0 },
            new[] { new ViiperCensusDevice(0, "7", "dualshock4") });

        ViiperBackendStopDecision decision = Decide(census: census);

        Assert.IsFalse(decision.ShouldStop);
        StringAssert.Contains(decision.Reason, "another consumer");
        StringAssert.Contains(decision.Reason, "dualshock4",
            "The log line has to name what stopped us, or nobody can debug it.");
    }

    /// <summary>
    /// A leftover of our own is not another consumer, but it is still a virtual
    /// device attached to a backend we were about to kill. Same answer,
    /// different reason.
    /// </summary>
    [TestMethod]
    public void OurOwnUndestroyedDeviceAlsoBlocksTheStop()
    {
        ViiperCensusDevice ourDevice = new ViiperCensusDevice(0, "3", "dualsense");
        ViiperBackendCensus census = ViiperBackendCensus.Success(
            new uint[] { 0 }, new[] { ourDevice });

        ViiperBackendStopDecision decision = Decide(census: census,
            ours: new[] { new ViiperCensusDevice(0, "3", null) });

        Assert.IsFalse(decision.ShouldStop);
        StringAssert.Contains(decision.Reason, "our own");
        StringAssert.Contains(decision.Reason, "teardown has not finished");
    }

    [TestMethod]
    public void ABusWithNoDevicesStillBlocksTheStop()
    {
        ViiperBackendCensus census = ViiperBackendCensus.Success(
            new uint[] { 4 }, Array.Empty<ViiperCensusDevice>());

        ViiperBackendStopDecision decision = Decide(census: census);

        Assert.IsFalse(decision.ShouldStop);
        StringAssert.Contains(decision.Reason, "empty bus");
    }

    /// <summary>
    /// Every unknown resolves the same way. A backend left running costs a few
    /// megabytes; a backend killed under a live consumer costs that consumer
    /// its controller.
    /// </summary>
    [TestMethod]
    public void ACensusThatCouldNotBeTakenLeavesTheBackendRunning()
    {
        ViiperBackendStopDecision failed =
            Decide(census: ViiperBackendCensus.Failed("connection refused"));
        Assert.IsFalse(failed.ShouldStop);
        StringAssert.Contains(failed.Reason, "connection refused");

        ViiperBackendStopDecision missing = ViiperBackendStopPolicy.Decide(
            true, SomeBackend(), true, null, Array.Empty<ViiperCensusDevice>());
        Assert.IsFalse(missing.ShouldStop);
        StringAssert.Contains(missing.Reason, "could not confirm");
    }

    // ---- Census over the API -------------------------------------------

    [TestMethod]
    public void AnEmptyBusListIsAnIdleBackend()
    {
        ViiperBackendCensus census = Census(new Dictionary<string, string>
        {
            ["bus/list"] = "{\"buses\":[]}",
        });

        Assert.IsTrue(census.Succeeded, census.FailureReason);
        Assert.AreEqual(0, census.Buses.Count);
        Assert.AreEqual(0, census.Devices.Count);
    }

    [TestMethod]
    public void DevicesAreCollectedFromEveryBus()
    {
        ViiperBackendCensus census = Census(new Dictionary<string, string>
        {
            ["bus/list"] = "{\"buses\":[0,1]}",
            ["bus/0/list"] = "{\"devices\":[{\"busId\":0,\"devId\":\"1\"," +
                "\"vid\":\"0x054c\",\"pid\":\"0x0ce6\",\"type\":\"dualsense\"}]}",
            ["bus/1/list"] = "{\"devices\":[{\"busId\":1,\"devId\":\"1\"," +
                "\"vid\":\"0x045e\",\"pid\":\"0x028e\",\"type\":\"xbox360\"}]}",
        });

        Assert.IsTrue(census.Succeeded, census.FailureReason);
        CollectionAssert.AreEquivalent(new uint[] { 0, 1 }, census.Buses.ToArray());
        CollectionAssert.AreEquivalent(new[] { "dualsense", "xbox360" },
            census.Devices.Select(device => device.Type).ToArray());
    }

    /// <summary>
    /// A bus can be removed between the two calls. That is a race, not a
    /// consumer, and the 404 the server returns for it says so precisely.
    /// </summary>
    [TestMethod]
    public void ABusThatDisappearsBetweenTheTwoCallsIsNotCountedAsAConsumer()
    {
        ViiperBackendCensus census = Census(new Dictionary<string, string>
        {
            ["bus/list"] = "{\"buses\":[9]}",
            ["bus/9/list"] = "{\"status\":404,\"title\":\"Not Found\"," +
                "\"detail\":\"bus 9 not found\"}",
        });

        Assert.IsTrue(census.Succeeded, census.FailureReason);
        Assert.AreEqual(0, census.Devices.Count);
    }

    [TestMethod]
    public void AProblemResponseToBusListIsAFailedCensusNotAnEmptyOne()
    {
        ViiperBackendCensus census = Census(new Dictionary<string, string>
        {
            ["bus/list"] = "{\"status\":500,\"title\":\"Internal Server Error\"," +
                "\"detail\":\"boom\"}",
        });

        Assert.IsFalse(census.Succeeded);
        StringAssert.Contains(census.FailureReason, "500");
    }

    [TestMethod]
    public void ASilentBackendIsAFailedCensus()
    {
        ViiperBackendCensus census = Census(new Dictionary<string, string>());

        Assert.IsFalse(census.Succeeded);
        StringAssert.Contains(census.FailureReason, "bus/list");
    }

    [TestMethod]
    public void UnparseableJsonIsAFailedCensus()
    {
        ViiperBackendCensus census = Census(new Dictionary<string, string>
        {
            ["bus/list"] = "not json at all",
        });

        Assert.IsFalse(census.Succeeded);
    }

    private static ViiperBackendCensus Census(IDictionary<string, string> responses) =>
        new ViiperApiBackendCensusSource(
            path => responses.TryGetValue(path, out string body) ? body : null)
            .TakeCensus();

    // ---- Owned-device registry -----------------------------------------

    /// <summary>
    /// The registry is what lets the log distinguish "somebody else's device"
    /// from "ours, still tearing down". Both block the stop, so the value is in
    /// the diagnosis rather than the decision.
    /// </summary>
    [TestMethod]
    public void TheOwnedDeviceRegistryTracksWhatWeCreatedAndReleased()
    {
        ViiperOwnedDeviceRegistry.ResetForTests();
        try
        {
            ViiperOwnedDeviceRegistry.Register(0, "1");
            ViiperOwnedDeviceRegistry.Register(1, "1");
            CollectionAssert.AreEquivalent(
                new[]
                {
                    new ViiperCensusDevice(0, "1", null),
                    new ViiperCensusDevice(1, "1", null),
                },
                ViiperOwnedDeviceRegistry.Snapshot().ToArray());

            ViiperOwnedDeviceRegistry.Unregister(0, "1");
            CollectionAssert.AreEquivalent(
                new[] { new ViiperCensusDevice(1, "1", null) },
                ViiperOwnedDeviceRegistry.Snapshot().ToArray());
        }
        finally
        {
            ViiperOwnedDeviceRegistry.ResetForTests();
        }
    }

    /// <summary>
    /// Identity is (bus, device). The type string is descriptive only — the
    /// census reports one and the registry does not, so comparing them must not
    /// depend on it.
    /// </summary>
    [TestMethod]
    public void DeviceIdentityIgnoresTheReportedType()
    {
        Assert.AreEqual(new ViiperCensusDevice(2, "5", null),
            new ViiperCensusDevice(2, "5", "dualsenseaudioonlyduplexv4"));
        Assert.AreNotEqual(new ViiperCensusDevice(2, "5", null),
            new ViiperCensusDevice(3, "5", null));
    }

    // ---- The setting ----------------------------------------------------

    [TestMethod]
    public void TheSettingDefaultsToStopping()
    {
        Assert.IsTrue(BackingStore.DEFAULT_STOP_VIIPER_BACKEND_ON_EXIT);
        Assert.IsTrue(new BackingStore().stopViiperBackendOnExit);
        Assert.IsTrue(new AppSettingsDTO().StopViiperBackendOnExit);
    }

    [TestMethod]
    public void TheSettingSurvivesAWriteAndReadOfTheConfig()
    {
        foreach (bool value in new[] { true, false })
        {
            BackingStore written = new BackingStore { stopViiperBackendOnExit = value };
            AppSettingsDTO dto = new AppSettingsDTO();
            dto.MapFrom(written);

            BackingStore read = new BackingStore();
            RoundTrip(dto).MapTo(read);

            Assert.AreEqual(value, read.stopViiperBackendOnExit,
                "The setting is read during shutdown, so a value that does not " +
                "survive a save is a setting that does nothing.");
        }
    }

    /// <summary>
    /// The upgrade case: a settings file written before this element existed.
    /// The absent element must leave the default in place, not read as false.
    /// </summary>
    [TestMethod]
    public void AConfigWrittenBeforeThisSettingExistedKeepsTheDefault()
    {
        AppSettingsDTO dto = Deserialize("<Profile><UseExclusiveMode>False</UseExclusiveMode></Profile>");

        Assert.IsTrue(dto.StopViiperBackendOnExit);
    }

    /// <summary>
    /// A hand-edited or corrupted value falls back to the default instead of
    /// throwing out of <c>Deserialize</c>, which <c>BackingStore.Load</c>
    /// catches by abandoning the entire settings file.
    /// </summary>
    [TestMethod]
    public void AMalformedValueFallsBackToTheDefaultInsteadOfLosingTheFile()
    {
        AppSettingsDTO dto = Deserialize(
            "<Profile><StopViiperBackendOnExit>yes please</StopViiperBackendOnExit></Profile>");

        Assert.IsTrue(dto.StopViiperBackendOnExit);
    }

    private static AppSettingsDTO RoundTrip(AppSettingsDTO source)
    {
        XmlSerializer serializer = new XmlSerializer(typeof(AppSettingsDTO));
        using StringWriter writer = new StringWriter();
        source.SerializeAppAttrs = false;
        serializer.Serialize(writer, source,
            new XmlSerializerNamespaces(new[] { XmlQualifiedName.Empty }));
        return Deserialize(writer.ToString());
    }

    private static AppSettingsDTO Deserialize(string xml)
    {
        XmlSerializer serializer = new XmlSerializer(typeof(AppSettingsDTO));
        using StringReader reader = new StringReader(xml);
        return (AppSettingsDTO)serializer.Deserialize(reader);
    }
}
