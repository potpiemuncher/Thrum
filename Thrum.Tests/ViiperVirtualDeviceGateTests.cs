using DS4Windows;
using DS4WinWPF.DS4Control.DTOXml;
using DS4WinWPF.DS4Forms.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DS4WindowsTests
{
    /// <summary>
    /// The gating decision (plan tasks 2.3 and 2.5).
    ///
    /// <para>The decision is a pure function of six inputs, and this class
    /// enumerates <b>all 128 combinations</b> rather than sampling them. That is
    /// not thoroughness for its own sake: the failure mode of a wrong cell is a
    /// virtual USB audio endpoint created on a driver with a confirmed kernel
    /// defect, which is a bugcheck the user cannot attribute to anything. An exhaustive table is the only form of this policy that can be
    /// reviewed by reading it.</para>
    /// </summary>
    [TestClass]
    public class ViiperVirtualDeviceGateTests
    {
        private static readonly ViiperDriverReadinessState[] States =
        {
            ViiperDriverReadinessState.Missing,
            ViiperDriverReadinessState.DetectedUnvalidated,
            ViiperDriverReadinessState.ValidatedExperimental,
            ViiperDriverReadinessState.Approved,
        };

        private static readonly ViiperFeatureClass[] Classes =
        {
            ViiperFeatureClass.ControllerOnly,
            ViiperFeatureClass.Audio,
        };

        private static readonly bool[] Booleans = { false, true };

        /// <summary>
        /// The whole policy, written out as data. Read this table, not the
        /// implementation, to know what the product does.
        /// </summary>
        private static ViiperVirtualDeviceBlock Expected(
            ViiperDriverReadinessState state, ViiperFeatureClass featureClass,
            bool acknowledged, bool audioEnabled, bool carriesFixes,
            bool alreadyAttached)
        {
            if (alreadyAttached)
            {
                return ViiperVirtualDeviceBlock.None;
            }

            switch (state)
            {
                case ViiperDriverReadinessState.Missing:
                    return ViiperVirtualDeviceBlock.DriverMissing;
                case ViiperDriverReadinessState.DetectedUnvalidated:
                    return ViiperVirtualDeviceBlock.DriverUnvalidated;
                case ViiperDriverReadinessState.Approved:
                    return ViiperVirtualDeviceBlock.None;
            }

            if (!acknowledged)
            {
                return ViiperVirtualDeviceBlock.ExperimentalNotAcknowledged;
            }

            if (featureClass != ViiperFeatureClass.Audio)
            {
                return ViiperVirtualDeviceBlock.None;
            }

            if (!audioEnabled)
            {
                return ViiperVirtualDeviceBlock.AudioClassNotEnabled;
            }

            return carriesFixes
                ? ViiperVirtualDeviceBlock.None
                : ViiperVirtualDeviceBlock.AudioClassNeedsFixedDriver;
        }

        private static IEnumerable<(ViiperDriverReadinessState State,
            ViiperFeatureClass Class, bool Ack, bool Audio, bool Fixes,
            bool Attached)>
            AllCombinations()
        {
            foreach (ViiperDriverReadinessState state in States)
            {
                foreach (ViiperFeatureClass featureClass in Classes)
                {
                    foreach (bool ack in Booleans)
                    {
                        foreach (bool audio in Booleans)
                        {
                            foreach (bool fixes in Booleans)
                            {
                                foreach (bool attached in Booleans)
                                {
                                    yield return (state, featureClass, ack,
                                        audio, fixes, attached);
                                }
                            }
                        }
                    }
                }
            }
        }

        [TestMethod]
        public void EveryCombinationMatchesTheDecisionTable()
        {
            var wrong = new List<string>();
            int count = 0;

            foreach (var combination in AllCombinations())
            {
                count++;
                ViiperVirtualDeviceBlock expected = Expected(combination.State,
                    combination.Class, combination.Ack, combination.Audio,
                    combination.Fixes, combination.Attached);

                ViiperVirtualDeviceDecision actual = ViiperVirtualDeviceGate.Decide(
                    combination.State, combination.Class, combination.Ack,
                    combination.Audio, combination.Fixes, combination.Attached);

                if (actual.Block != expected)
                {
                    wrong.Add($"{Describe(combination)} => {actual.Block}, " +
                        $"expected {expected}");
                    continue;
                }

                // Allowed and Block are two views of one answer; a decision that
                // is allowed with a block reason, or refused with none, would
                // let a caller checking the wrong one do the wrong thing.
                bool expectedAllowed = expected == ViiperVirtualDeviceBlock.None;
                if (actual.Allowed != expectedAllowed)
                {
                    wrong.Add($"{Describe(combination)} => Allowed={actual.Allowed} " +
                        $"contradicts Block={actual.Block}");
                }

                if (string.IsNullOrWhiteSpace(actual.Reason))
                {
                    wrong.Add($"{Describe(combination)} => no reason given");
                }
            }

            Assert.AreEqual(128, count,
                "The table must cover 4 states x 2 classes x 2 x 2 x 2 x 2.");
            Assert.AreEqual(0, wrong.Count,
                "Gate decisions that disagree with the table:\n" +
                string.Join("\n", wrong));
        }

        /// <summary>
        /// The exemption that keeps the guardrail from becoming the hazard.
        /// Tearing down a live audio endpoint is the exact operation the kernel
        /// defect is reached through, so a gate that yanked one would cause the
        /// crash it exists to prevent. It also has to hold for the states where
        /// nothing may be created at all.
        /// </summary>
        [TestMethod]
        public void AnAlreadyAttachedDeviceIsNeverRefused()
        {
            var refused = AllCombinations()
                .Where(c => c.Attached)
                .Select(c => (c, decision: ViiperVirtualDeviceGate.Decide(
                    c.State, c.Class, c.Ack, c.Audio, c.Fixes,
                    alreadyAttached: true)))
                .Where(pair => !pair.decision.Allowed)
                .Select(pair => Describe(pair.c))
                .ToList();

            Assert.AreEqual(0, refused.Count,
                "A running session was refused:\n" + string.Join("\n", refused));
        }

        /// <summary>
        /// A recognised experimental package, the backend acknowledged, and
        /// audio turned off by the user - audio-class output must refuse while
        /// controller output proceeds.
        /// </summary>
        [TestMethod]
        public void AudioTurnedOffIsRefusedWhileControllersWork()
        {
            ViiperVirtualDeviceDecision audio = ViiperVirtualDeviceGate.Decide(
                ViiperDriverReadinessState.ValidatedExperimental,
                ViiperFeatureClass.Audio, experimentalAcknowledged: true,
                audioClassEnabled: false, driverCarriesFixes: true,
                alreadyAttached: false);

            Assert.IsFalse(audio.Allowed);
            Assert.AreEqual(ViiperVirtualDeviceBlock.AudioClassNotEnabled,
                audio.Block);

            ViiperVirtualDeviceDecision controller = ViiperVirtualDeviceGate.Decide(
                ViiperDriverReadinessState.ValidatedExperimental,
                ViiperFeatureClass.ControllerOnly,
                experimentalAcknowledged: true, audioClassEnabled: false,
                driverCarriesFixes: false, alreadyAttached: false);

            Assert.IsTrue(controller.Allowed,
                "Controller features must work without audio endpoints, or the " +
                "disclosure's promise that they do is false.");
        }

        /// <summary>
        /// Audio is on by default, so the default can no longer stand for a
        /// user risk decision: a release without the upstream fix for the
        /// teardown defect never gets audio endpoints, whatever the setting
        /// says, and says why. The same pad on 0.9.8.0 gets them.
        /// </summary>
        [TestMethod]
        public void AudioOnByDefaultNeedsAReleaseWithTheUpstreamFix()
        {
            ViiperVirtualDeviceDecision oldRelease = ViiperVirtualDeviceGate.Decide(
                ViiperDriverReadinessState.ValidatedExperimental,
                ViiperFeatureClass.Audio, experimentalAcknowledged: true,
                audioClassEnabled: true, driverCarriesFixes: false,
                alreadyAttached: false);

            Assert.IsFalse(oldRelease.Allowed);
            Assert.AreEqual(ViiperVirtualDeviceBlock.AudioClassNeedsFixedDriver,
                oldRelease.Block);
            StringAssert.Contains(oldRelease.Reason,
                ViiperExperimentalDisclosure.FixedInReleaseLabel);
            StringAssert.Contains(oldRelease.Reason, "#181");

            Assert.IsTrue(ViiperVirtualDeviceGate.Decide(
                ViiperDriverReadinessState.ValidatedExperimental,
                ViiperFeatureClass.Audio, experimentalAcknowledged: true,
                audioClassEnabled: true, driverCarriesFixes: true,
                alreadyAttached: false).Allowed);
        }

        /// <summary>
        /// Fail-closed: no amount of consent buys a virtual device on a driver
        /// whose identity is unknown or absent. Consent is about accepting a
        /// known risk, not about overriding an unproven one.
        /// </summary>
        [TestMethod]
        public void ConsentCannotUnlockAnUnprovenDriver()
        {
            foreach (ViiperDriverReadinessState state in new[]
            {
                ViiperDriverReadinessState.Missing,
                ViiperDriverReadinessState.DetectedUnvalidated,
            })
            {
                foreach (ViiperFeatureClass featureClass in Classes)
                {
                    ViiperVirtualDeviceDecision decision =
                        ViiperVirtualDeviceGate.Decide(state, featureClass,
                            experimentalAcknowledged: true,
                            audioClassEnabled: true, driverCarriesFixes: true,
                            alreadyAttached: false);

                    Assert.IsFalse(decision.Allowed,
                        $"{state}/{featureClass} was allowed with full consent.");
                }
            }
        }

        /// <summary>
        /// Audio-class output depends on the setting and the release at every
        /// state that exists today, and stops depending on them only at
        /// <c>Approved</c> - which the manifest cannot produce. That is what
        /// makes the tier meaningful rather than decorative.
        /// </summary>
        [TestMethod]
        public void OnlyApprovedIgnoresTheAudioSettingAndRelease()
        {
            foreach (ViiperDriverReadinessState state in States)
            {
                ViiperVirtualDeviceDecision decision =
                    ViiperVirtualDeviceGate.Decide(state,
                        ViiperFeatureClass.Audio,
                        experimentalAcknowledged: true,
                        audioClassEnabled: false, driverCarriesFixes: false,
                        alreadyAttached: false);

                Assert.AreEqual(
                    state == ViiperDriverReadinessState.Approved,
                    decision.Allowed,
                    $"Audio with the setting off on an old release at {state}.");
            }
        }

        /// <summary>
        /// A state the enum does not define must refuse. A new readiness state
        /// is unproven by definition, and the compiler will not point at this
        /// switch when one is added.
        /// </summary>
        [TestMethod]
        public void AnUnknownReadinessStateFailsClosed()
        {
            ViiperVirtualDeviceDecision decision = ViiperVirtualDeviceGate.Decide(
                (ViiperDriverReadinessState)9999,
                ViiperFeatureClass.ControllerOnly,
                experimentalAcknowledged: true, audioClassEnabled: true,
                driverCarriesFixes: true, alreadyAttached: false);

            Assert.IsFalse(decision.Allowed);
            Assert.AreEqual(ViiperVirtualDeviceBlock.DriverUnvalidated,
                decision.Block);
        }

        /// <summary>
        /// Every refusal is read by a non-expert, in a log line or a banner, so
        /// it has to name something they can act on. It must also never leak a
        /// path: these strings go to the log.
        /// </summary>
        [TestMethod]
        public void EveryRefusalReasonIsActionableAndCarriesNoPath()
        {
            var problems = new List<string>();

            foreach (var combination in AllCombinations())
            {
                ViiperVirtualDeviceDecision decision = ViiperVirtualDeviceGate.Decide(
                    combination.State, combination.Class, combination.Ack,
                    combination.Audio, combination.Fixes, combination.Attached);
                if (decision.Allowed)
                {
                    continue;
                }

                string reason = decision.Reason;
                if (!reason.Contains("Settings", StringComparison.Ordinal))
                {
                    problems.Add($"{Describe(combination)}: no route to a remedy");
                }

                if (Regex.IsMatch(reason, @"[A-Za-z]:\\|\\\\|%[A-Za-z]+%"))
                {
                    problems.Add($"{Describe(combination)}: contains a path");
                }
            }

            Assert.AreEqual(0, problems.Count, string.Join("\n", problems));
        }

        /// <summary>
        /// The wording policy from the VM pass, applied to the gate's own text:
        /// a refusal must not send the user off to find a driver build that the
        /// manifest does not list.
        /// </summary>
        [TestMethod]
        public void NoRefusalRecommendsAnUnlistedPackage()
        {
            string[] forbidden =
            {
                "download", "latest version", "newest", "upgrade to",
                "usbip-win2 releases", "github.com",
            };

            var problems = (from combination in AllCombinations()
                            let decision = ViiperVirtualDeviceGate.Decide(
                                combination.State, combination.Class,
                                combination.Ack, combination.Audio,
                                combination.Fixes, combination.Attached)
                            where !decision.Allowed
                            from phrase in forbidden
                            where decision.Reason.Contains(phrase,
                                StringComparison.OrdinalIgnoreCase)
                            select $"{Describe(combination)}: \"{phrase}\"")
                           .Distinct()
                           .ToList();

            Assert.AreEqual(0, problems.Count,
                "A refusal points at a package the manifest does not list:\n" +
                string.Join("\n", problems));
        }

        // ---- The live wiring ------------------------------------------------

        /// <summary>
        /// The seams exist so this test can prove the guard forwards state
        /// rather than second-guessing it. Without this, the pure table above
        /// could be perfect while the product asked the wrong question.
        /// </summary>
        [TestMethod]
        public void TheGuardForwardsTheLiveStateToTheGate()
        {
            try
            {
                ViiperVirtualDeviceGuard.ReadinessOverride = () =>
                    ViiperDriverReadinessState.ValidatedExperimental;
                ViiperVirtualDeviceGuard.AcknowledgedOverride = () => true;
                ViiperVirtualDeviceGuard.AudioEnabledOverride = () => false;
                ViiperVirtualDeviceGuard.CarriesFixesOverride = () => true;

                Assert.IsTrue(ViiperVirtualDeviceGuard
                    .Decide(ViiperFeatureClass.ControllerOnly).Allowed);
                Assert.IsFalse(ViiperVirtualDeviceGuard
                    .Decide(ViiperFeatureClass.Audio).Allowed);
                Assert.IsTrue(ViiperVirtualDeviceGuard
                    .Decide(ViiperFeatureClass.Audio, alreadyAttached: true).Allowed);

                ViiperVirtualDeviceGuard.AudioEnabledOverride = () => true;
                Assert.IsTrue(ViiperVirtualDeviceGuard
                    .Decide(ViiperFeatureClass.Audio).Allowed);

                ViiperVirtualDeviceGuard.CarriesFixesOverride = () => false;
                Assert.AreEqual(ViiperVirtualDeviceBlock.AudioClassNeedsFixedDriver,
                    ViiperVirtualDeviceGuard.Decide(ViiperFeatureClass.Audio).Block);
            }
            finally
            {
                ViiperVirtualDeviceGuard.ResetOverridesForTests();
            }
        }

        /// <summary>
        /// Xbox 360 and Switch 2 Pro have no audio persona at all, so they can
        /// never reach the audio class and the audio setting cannot affect them.
        /// </summary>
        [TestMethod]
        public void OnlySonyPersonasCanNegotiateAudioEndpoints()
        {
            Assert.IsTrue(ViiperOutDevice.CanNegotiateAudioEndpoints(
                ViiperVirtualDeviceType.DualSense));
            Assert.IsTrue(ViiperOutDevice.CanNegotiateAudioEndpoints(
                ViiperVirtualDeviceType.DualSenseEdge));
            Assert.IsTrue(ViiperOutDevice.CanNegotiateAudioEndpoints(
                ViiperVirtualDeviceType.DualShock4));
            Assert.IsFalse(ViiperOutDevice.CanNegotiateAudioEndpoints(
                ViiperVirtualDeviceType.Xbox360));
            Assert.IsFalse(ViiperOutDevice.CanNegotiateAudioEndpoints(
                ViiperVirtualDeviceType.Switch2Pro));
        }

        // ---- The persisted flags --------------------------------------------

        /// <summary>
        /// The acknowledgement is off until the user gives it; audio endpoints
        /// are on by default (2026-09-26), and the gate keeps them off any
        /// release without the upstream fix.
        /// </summary>
        [TestMethod]
        public void AcknowledgementDefaultsToOffAndAudioToOn()
        {
            Assert.IsFalse(BackingStore.DEFAULT_VIIPER_EXPERIMENTAL_ACKNOWLEDGED);
            Assert.IsTrue(BackingStore.DEFAULT_ALLOW_EXPERIMENTAL_AUDIO_ENDPOINTS);
            Assert.IsFalse(new BackingStore().viiperExperimentalAcknowledged);
            Assert.IsTrue(new BackingStore().allowExperimentalAudioEndpoints);
            Assert.IsFalse(new AppSettingsDTO().ViiperExperimentalAcknowledged);
            Assert.IsTrue(new AppSettingsDTO().AllowExperimentalAudioEndpoints);
        }

        [TestMethod]
        public void BothConsentFlagsSurviveAWriteAndReadOfTheConfig()
        {
            foreach (bool acknowledged in Booleans)
            {
                foreach (bool audio in Booleans)
                {
                    BackingStore written = new BackingStore
                    {
                        viiperExperimentalAcknowledged = acknowledged,
                        allowExperimentalAudioEndpoints = audio,
                    };

                    AppSettingsDTO dto = new AppSettingsDTO();
                    dto.MapFrom(written);

                    BackingStore read = new BackingStore();
                    RoundTrip(dto).MapTo(read);

                    Assert.AreEqual(acknowledged,
                        read.viiperExperimentalAcknowledged);
                    Assert.AreEqual(audio, read.allowExperimentalAudioEndpoints);
                }
            }
        }

        /// <summary>
        /// The upgrade case: a settings file written before these elements
        /// existed must read as "not acknowledged", so no virtual device is
        /// created until the user has read the notice. The audio setting reads
        /// as its default (on), which does nothing before the acknowledgement.
        /// </summary>
        [TestMethod]
        public void AConfigWrittenBeforeTheseSettingsExistedGrantsNoConsent()
        {
            AppSettingsDTO dto = Deserialize(
                "<Profile><UseExclusiveMode>False</UseExclusiveMode></Profile>");

            Assert.IsFalse(dto.ViiperExperimentalAcknowledged);
            Assert.IsTrue(dto.AllowExperimentalAudioEndpoints);
        }

        /// <summary>
        /// An existing settings file keeps what it says. Every file written
        /// before 2026-09-26 carries False, because that was the default, so
        /// turning the default on changes nothing for existing installations.
        /// </summary>
        [TestMethod]
        public void AnExistingConfigKeepsItsAudioSetting()
        {
            AppSettingsDTO dto = Deserialize(
                "<Profile><AllowExperimentalAudioEndpoints>False</AllowExperimentalAudioEndpoints></Profile>");

            Assert.IsFalse(dto.AllowExperimentalAudioEndpoints);
        }

        [TestMethod]
        public void AMalformedConsentValueReadsAsNoConsent()
        {
            AppSettingsDTO dto = Deserialize(
                "<Profile>" +
                "<ViiperExperimentalAcknowledged>sure</ViiperExperimentalAcknowledged>" +
                "<AllowExperimentalAudioEndpoints>why not</AllowExperimentalAudioEndpoints>" +
                "</Profile>");

            Assert.IsFalse(dto.ViiperExperimentalAcknowledged);
            Assert.IsFalse(dto.AllowExperimentalAudioEndpoints);
        }

        // ---- The Output Slots banner -----------------------------------------

        [TestMethod]
        public void TheBannerIsSilentOnlyWhenNothingIsRefused()
        {
            ViiperOutputGateBannerViewModel banner = Banner(
                ViiperDriverReadinessState.Approved, acknowledged: true,
                audio: true);

            Assert.IsFalse(banner.IsVisible);
            Assert.AreEqual("None", banner.Severity);
        }

        [TestMethod]
        public void TheBannerReportsABlockedPageAsBlocked()
        {
            ViiperOutputGateBannerViewModel banner = Banner(
                ViiperDriverReadinessState.DetectedUnvalidated,
                acknowledged: true, audio: true);

            Assert.IsTrue(banner.IsVisible);
            Assert.AreEqual("Blocked", banner.Severity);
            StringAssert.Contains(banner.Headline, "blocked");
            StringAssert.Contains(banner.Text, "Settings");
        }

        /// <summary>
        /// The audio-only refusal is a limitation, not a failure, and the banner
        /// has to say that running controllers are unaffected - otherwise a user
        /// looking at a working pad concludes the message is wrong and stops
        /// reading the next one.
        /// </summary>
        [TestMethod]
        public void TheBannerSeparatesALimitedPageFromABlockedOne()
        {
            ViiperOutputGateBannerViewModel banner = Banner(
                ViiperDriverReadinessState.ValidatedExperimental,
                acknowledged: true, audio: false);

            Assert.IsTrue(banner.IsVisible);
            // Audio turned off by the user is a working configuration they
            // chose: a neutral note, not a warning.
            Assert.AreEqual("Info", banner.Severity);
            StringAssert.Contains(banner.Headline, "audio");
            StringAssert.Contains(banner.Text, "already plugged in");
        }

        [TestMethod]
        public void TheBannerStaysEmptyBeforeItsFirstRefresh()
        {
            // Constructed by the view before the gate has been asked anything.
            // It must render nothing rather than assert a state it does not have.
            ViiperOutputGateBannerViewModel banner =
                new ViiperOutputGateBannerViewModel(
                    () => throw new InvalidOperationException(
                        "the banner must not evaluate on construction"),
                    () => throw new InvalidOperationException(
                        "the banner must not evaluate on construction"));

            Assert.IsFalse(banner.IsVisible);
            Assert.AreEqual(string.Empty, banner.Text);
            Assert.AreEqual("None", banner.Severity);
        }

        private static ViiperOutputGateBannerViewModel Banner(
            ViiperDriverReadinessState state, bool acknowledged, bool audio)
        {
            ViiperOutputGateBannerViewModel banner =
                new ViiperOutputGateBannerViewModel(
                    () => ViiperVirtualDeviceGate.Decide(state,
                        ViiperFeatureClass.ControllerOnly, acknowledged, audio,
                        true, false),
                    () => ViiperVirtualDeviceGate.Decide(state,
                        ViiperFeatureClass.Audio, acknowledged, audio, true,
                        false));
            banner.Refresh();
            return banner;
        }

        private static string Describe(
            (ViiperDriverReadinessState State, ViiperFeatureClass Class,
                bool Ack, bool Audio, bool Fixes, bool Attached) combination) =>
            $"{combination.State}/{combination.Class}/ack={combination.Ack}/" +
            $"audio={combination.Audio}/fixes={combination.Fixes}/" +
            $"attached={combination.Attached}";

        private static AppSettingsDTO RoundTrip(AppSettingsDTO source) =>
            AppSettingsRoundTrip.Write(source);

        private static AppSettingsDTO Deserialize(string xml) =>
            AppSettingsRoundTrip.Read(xml);
    }
}
