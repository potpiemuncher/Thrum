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
using DS4WinWPF.DS4Control;
using DS4WinWPF.DS4Forms.ViewModels;
using NAudio.CoreAudioApi;

namespace DS4WindowsTests;

/// <summary>
/// Hardware-free tests for the live-reader policy boundaries. These exercise
/// the traps rather than Windows itself: no backend launch, a guard before all
/// six default endpoints, reference identity instead of the manager's unsafe
/// dictionary, and no identifier-bearing convenience strings.
/// </summary>
[TestClass]
public class ThrumDiagnosticsLiveSourcesTests
{
    [TestMethod]
    public void DriverReaderRedactsUserPathsBeforeTheyReachTheSnapshot()
    {
        ViiperDriverReadiness readiness = new ViiperDriverReadiness(
            ViiperDriverReadinessState.DetectedUnvalidated,
            new[]
            {
                @"usbip.exe failed at C:\Users\patrick\Tools\usbip.exe",
            }, Array.Empty<ViiperDriverComponentIdentity>(), null, null,
            DateTimeOffset.UnixEpoch);
        ThrumDiagnosticsLiveSources sources = Sources(
            driverReadiness: readiness);

        DiagnosticsDriverSection section = sources.ReadDriver();

        Assert.AreEqual(1, section.Reasons.Count);
        Assert.IsFalse(section.Reasons[0].Contains("patrick"),
            "a user path reached the pre-redacted snapshot: " +
            section.Reasons[0]);
        StringAssert.Contains(section.Reasons[0], @"\Users\<user>\");
    }

    [TestMethod]
    public void BackendReaderExplicitlyRefusesToStartOrClaimAServer()
    {
        bool? tryStartServer = null;
        ThrumDiagnosticsLiveSources sources = Sources(
            readBackendStatus: requested =>
            {
                tryStartServer = requested;
                return new ViiperPrerequisiteStatus
                {
                    ViiperInstalled = true,
                    ServerRunning = false,
                };
            },
            assessBackend: _ => BackendReport(
                ViiperUnownedBackendState.NoBackend));

        DiagnosticsBackendSection section = sources.ReadBackend();

        Assert.AreEqual(false, tryStartServer,
            "a diagnostics read requested the mutating backend-start path");
        Assert.IsFalse(section.ServerRunning);
        Assert.AreEqual(ViiperInstallerPins.ViiperBackend.ReleaseLabel,
            section.PinnedVersion);
        Assert.AreEqual(
            ViiperInstallerPins.ViiperBackendExpectedEmbeddedVersionStamp,
            section.ExpectedEmbeddedVersionStamp);
        StringAssert.Contains(section.Detail, "not enumerated");
    }

    [TestMethod]
    public void BackendHoldingsUseOnlyCountsAndTypes()
    {
        ViiperUnownedBackendReport report =
            new ViiperUnownedBackendReport(
                ViiperUnownedBackendState.UnownedInUse,
                new[]
                {
                    new ViiperCensusDevice(73, "foreign-secret-id",
                        "DualSense"),
                    new ViiperCensusDevice(74, "another-secret-id",
                        "DualSense"),
                },
                new[]
                {
                    new ViiperCensusDevice(75, "our-secret-id", "Xbox360"),
                },
                new uint[] { 9981 }, null);
        ThrumDiagnosticsLiveSources sources = Sources(
            readBackendStatus: _ => new ViiperPrerequisiteStatus
            {
                ViiperInstalled = true,
                ServerRunning = true,
            },
            assessBackend: _ => report);

        DiagnosticsBackendSection section = sources.ReadBackend();
        string rendered = string.Join("\n", section.Holdings);

        StringAssert.Contains(rendered, "2 DualSense");
        StringAssert.Contains(rendered, "1 Xbox360");
        StringAssert.Contains(rendered, "1 empty bus");
        Assert.IsFalse(rendered.Contains("secret-id"),
            "a backend DevId reached the holdings projection: " + rendered);
        Assert.IsFalse(rendered.Contains("9981"),
            "a backend bus ID reached the holdings projection: " + rendered);
    }

    [TestMethod]
    public void HidHideWhitelistFailureStaysAnExplicitSectionState()
    {
        ThrumDiagnosticsLiveSources sources = Sources(
            readHidHideInstalled: () => true,
            readThisAppWhitelisted: () => throw
                new InvalidOperationException(
                    @"denied C:\Users\patrick\Games\private.exe"));

        DiagnosticsHidHideSection section = sources.ReadHidHide();

        Assert.IsTrue(section.Installed);
        Assert.IsNull(section.ThisAppWhitelisted);
        StringAssert.Contains(section.ReadFailure, "InvalidOperationException");
        Assert.IsFalse(section.ReadFailure.Contains("patrick"),
            "a user path survived the whitelist failure: " +
            section.ReadFailure);
        StringAssert.Contains(section.ReadFailure, @"\Users\<user>\");
    }

    [TestMethod]
    public void AudioReaderGuardsEveryDefaultSlotAndDisposesEveryEndpoint()
    {
        FakeAudioReader audio = new FakeAudioReader(
            (DataFlow.Render, Role.Console),
            (DataFlow.Capture, Role.Communications));
        audio.ActiveEndpoints.Add(new FakeAudioEndpoint("Desktop", false));
        audio.ActiveEndpoints.Add(new FakeAudioEndpoint(
            "Wireless Controller", true));
        ThrumDiagnosticsLiveSources sources = Sources(
            createAudioReader: () => audio);

        DiagnosticsAudioSection section = sources.ReadAudio();

        Assert.AreEqual(6, audio.GuardCalls.Count,
            "not every flow/role pair was guarded");
        Assert.AreEqual(2, audio.DefaultReadCalls.Count,
            "GetDefaultAudioEndpoint ran for a slot the guard said was empty");
        Assert.AreEqual(6, section.DefaultEndpoints.Count);
        Assert.AreEqual(4, section.DefaultEndpoints.Count(line =>
            line.EndsWith(": none", StringComparison.Ordinal)));
        Assert.IsTrue(section.ControllerRenderEndpointPresent);
        Assert.IsTrue(audio.Disposed);
        Assert.IsTrue(audio.CreatedEndpoints.All(endpoint =>
            endpoint.Disposed), "an MMDevice-shaped endpoint was not disposed");
    }

    [TestMethod]
    public void OneDisappearingDefaultEndpointDoesNotCostTheOtherFive()
    {
        FakeAudioReader audio = new FakeAudioReader(
            (DataFlow.Render, Role.Console),
            (DataFlow.Render, Role.Multimedia));
        audio.ThrowFriendlyNameFor.Add(
            (DataFlow.Render, Role.Console));
        ThrumDiagnosticsLiveSources sources = Sources(
            createAudioReader: () => audio);

        DiagnosticsAudioSection section = sources.ReadAudio();

        Assert.AreEqual(6, section.DefaultEndpoints.Count);
        Assert.IsTrue(section.DefaultEndpoints.Any(line =>
            line.Contains("Render/Console: (could not read:")));
        Assert.IsTrue(section.DefaultEndpoints.Any(line =>
            line == "Render/Multimedia: Render Multimedia"));
        Assert.IsTrue(audio.CreatedEndpoints.All(endpoint =>
            endpoint.Disposed));
    }

    [TestMethod]
    public void ReferenceScanUsesIdentityEvenWhenEqualityLies()
    {
        AlwaysEqual first = new AlwaysEqual();
        AlwaysEqual target = new AlwaysEqual();

        int index = ThrumDiagnosticsLiveSources.FindReferenceIndex(target,
            new[] { first, target });

        Assert.AreEqual(1, index,
            "the slot scan used value equality instead of ReferenceEquals");
    }

    [TestMethod]
    public void SlotProjectionScansInputOutputsAndNeverReadsTheMacString()
    {
        TestOutputDevice other = new TestOutputDevice("Other");
        TestOutputDevice output = new TestOutputDevice("VIIPER");
        OutSlotDevice slot = new OutSlotDevice(0);
        slot.AttachedDevice(output, OutContType.ViiperDS4,
            inIdx: 7,
            inDisplayString: "Wireless Controller [AA:BB:CC:DD:EE:FF]");
        slot.CurrentInputBound = OutSlotDevice.InputBound.Bound;
        int displayNameIndex = -1;

        IReadOnlyList<DiagnosticsSlotRow> rows =
            ThrumDiagnosticsLiveSources.ProjectSlots(
                new[] { slot }, new OutputDevice[] { other, output },
                index =>
                {
                    displayNameIndex = index;
                    return "Wireless Controller";
                });

        Assert.AreEqual(1, rows.Count);
        Assert.AreEqual(1, rows[0].Index, "slot index was not made one-based");
        Assert.AreEqual(1, displayNameIndex,
            "the physical input was not found by scanning output references");
        Assert.AreEqual("Wireless Controller", rows[0].InputDisplayName);
        Assert.IsFalse(string.Join(" ", rows.Select(row =>
            row.InputDisplayName)).Contains("AA:BB:CC:DD:EE:FF"),
            "OutSlotDevice.InputDisplayString leaked into the snapshot");
    }

    [TestMethod]
    public void LinkHealthReadsTheAccessorWithoutPhysicalControllerIdentity()
    {
        ViiperOutDevice output = new ViiperOutDevice(
            OutContType.ViiperDualSense,
            ViiperVirtualDeviceType.DualSense);
        byte[] payload = new byte[4];
        Assert.IsTrue(output.FeedbackDispatchBuffer.TryEnqueueSpeaker(
            payload, payload.Length, generation: 1));
        Assert.IsFalse(output.FeedbackDispatchBuffer.TryEnqueueSpeaker(
            payload, length: payload.Length + 1, generation: 2));
        Assert.IsTrue(output.FeedbackDispatchBuffer.QueueControl(
            payload, payload.Length, generation: 1, deviceIndex: 3));
        Assert.IsTrue(output.FeedbackDispatchBuffer.QueueControl(
            payload, payload.Length, generation: 2, deviceIndex: 4));
        OutSlotDevice slot = new OutSlotDevice(2);
        slot.AttachedDevice(output, OutContType.ViiperDualSense, 4,
            "Personal Pad [AA:BB:CC:DD:EE:FF]");

        DiagnosticsLinkHealthRow row =
            ThrumDiagnosticsLiveSources.ProjectLinkHealth(
                new[] { slot }).Single();

        Assert.AreEqual("slot 3 DualSense", row.Device);
        Assert.AreEqual(1, row.SpeakerEnqueued);
        Assert.AreEqual(1, row.SpeakerDropped);
        Assert.AreEqual(1, row.SpeakerHighWater);
        Assert.AreEqual(2, row.ControlEnqueued);
        Assert.AreEqual(1, row.ControlCoalesced);
        Assert.IsFalse(row.Device.Contains("Personal Pad"));
        Assert.IsFalse(row.Device.Contains("AA:BB"));
    }

    [TestMethod]
    public void PageFailureCardCannotLookLikeAnEmptyHealthySlotList()
    {
        ThrumDiagnosticsSnapshot snapshot = new ThrumDiagnosticsSnapshot
        {
            TimestampUtc = DateTimeOffset.UnixEpoch,
            Slots = Array.Empty<DiagnosticsSlotRow>(),
            CollectionFailures = new[]
            {
                "output slots: InvalidOperationException: service unavailable",
            },
        };

        IReadOnlyList<DiagnosticsCardViewModel> cards =
            DiagnosticsPageViewModel.BuildCards(snapshot);
        DiagnosticsCardViewModel slots = cards.Single(card =>
            card.Title == "Output slots");

        Assert.AreEqual(6, cards.Count);
        Assert.AreEqual("Could not read", slots.BadgeText);
        Assert.IsTrue(slots.Lines.Any(line =>
            line.Contains("service unavailable")));
        Assert.IsFalse(slots.Lines.Any(line =>
            line.Contains("No output slots")),
            "a failed section rendered as a successful empty section");
    }

    private static ThrumDiagnosticsLiveSources Sources(
        ViiperDriverReadiness driverReadiness = null,
        Func<bool, ViiperPrerequisiteStatus> readBackendStatus = null,
        Func<bool?, ViiperUnownedBackendReport> assessBackend = null,
        Func<bool> readHidHideInstalled = null,
        Func<bool> readThisAppWhitelisted = null,
        Func<IThrumDiagnosticsAudioEndpointReader> createAudioReader = null)
    {
        ViiperDriverReadiness readiness = driverReadiness ??
            new ViiperDriverReadiness(ViiperDriverReadinessState.Missing,
                Array.Empty<string>(),
                Array.Empty<ViiperDriverComponentIdentity>(), null, null,
                DateTimeOffset.UnixEpoch);
        return new ThrumDiagnosticsLiveSources(null,
            () => readiness,
            readBackendStatus ?? (_ => new ViiperPrerequisiteStatus()),
            assessBackend ?? (_ => BackendReport(
                ViiperUnownedBackendState.NoBackend)),
            readHidHideInstalled ?? (() => false),
            readThisAppWhitelisted ?? (() => false),
            createAudioReader ?? (() => new FakeAudioReader()));
    }

    private static ViiperUnownedBackendReport BackendReport(
        ViiperUnownedBackendState state) =>
        new ViiperUnownedBackendReport(state, null, null, null, null);

    private sealed class AlwaysEqual
    {
        public override bool Equals(object obj) => true;

        public override int GetHashCode() => 0;
    }

    private sealed class TestOutputDevice : OutputDevice
    {
        private readonly string type;

        public TestOutputDevice(string type)
        {
            this.type = type;
        }

        public override void ConvertandSendReport(DS4State state, int device)
        {
        }

        public override void Connect()
        {
        }

        public override void Disconnect()
        {
        }

        public override void ResetState(bool submit = true)
        {
        }

        public override string GetDeviceType() => type;

        public override void RemoveFeedbacks()
        {
        }

        public override void RemoveFeedback(int inIdx)
        {
        }
    }

    private sealed class FakeAudioReader :
        IThrumDiagnosticsAudioEndpointReader
    {
        private readonly HashSet<(DataFlow Flow, Role Role)> present;

        public FakeAudioReader(
            params (DataFlow Flow, Role Role)[] present)
        {
            this.present = new HashSet<(DataFlow, Role)>(present);
        }

        public List<(DataFlow Flow, Role Role)> GuardCalls { get; } = new();

        public List<(DataFlow Flow, Role Role)> DefaultReadCalls { get; } =
            new();

        public HashSet<(DataFlow Flow, Role Role)> ThrowFriendlyNameFor
        {
            get;
        } = new();

        public List<FakeAudioEndpoint> ActiveEndpoints { get; } = new();

        public List<FakeAudioEndpoint> CreatedEndpoints { get; } = new();

        public bool Disposed { get; private set; }

        public bool HasDefaultAudioEndpoint(DataFlow flow, Role role)
        {
            GuardCalls.Add((flow, role));
            return present.Contains((flow, role));
        }

        public IThrumDiagnosticsAudioEndpoint GetDefaultAudioEndpoint(
            DataFlow flow, Role role)
        {
            DefaultReadCalls.Add((flow, role));
            if (!present.Contains((flow, role)))
            {
                throw new InvalidOperationException(
                    "the default endpoint guard was bypassed");
            }

            FakeAudioEndpoint endpoint = new FakeAudioEndpoint(
                flow + " " + role, false,
                ThrowFriendlyNameFor.Contains((flow, role)));
            CreatedEndpoints.Add(endpoint);
            return endpoint;
        }

        public IEnumerable<IThrumDiagnosticsAudioEndpoint>
            EnumerateActiveRenderEndpoints()
        {
            foreach (FakeAudioEndpoint endpoint in ActiveEndpoints)
            {
                CreatedEndpoints.Add(endpoint);
                yield return endpoint;
            }
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }

    private sealed class FakeAudioEndpoint :
        IThrumDiagnosticsAudioEndpoint
    {
        private readonly string friendlyName;
        private readonly bool throwFriendlyName;

        public FakeAudioEndpoint(string friendlyName,
            bool controllerAudio, bool throwFriendlyName = false)
        {
            this.friendlyName = friendlyName;
            IsControllerAudioEndpoint = controllerAudio;
            this.throwFriendlyName = throwFriendlyName;
        }

        public string FriendlyName => throwFriendlyName
            ? throw new InvalidOperationException("endpoint disappeared")
            : friendlyName;

        public bool IsControllerAudioEndpoint { get; }

        public bool Disposed { get; private set; }

        public void Dispose()
        {
            Disposed = true;
        }
    }
}
