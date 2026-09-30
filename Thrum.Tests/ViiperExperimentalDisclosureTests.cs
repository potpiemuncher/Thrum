using DS4Windows;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DS4WindowsTests
{
    /// <summary>
    /// The risk disclosure's wording (plan task 2.3).
    ///
    /// <para>Nothing in a build fails when this text is wrong, and a reviewer
    /// reading a diff will not notice that a sentence quietly stopped being
    /// true. These tests are what notices. They assert the four properties the
    /// task fixed as non-negotiable, not the prose itself, so the words can be
    /// improved without rewriting the suite. Since audio endpoints went on by
    /// default (2026-09-26) there is no per-enablement dialog; the one-time
    /// notice carries the risk, and the gate keeps old releases out.</para>
    /// </summary>
    [TestClass]
    public class ViiperExperimentalDisclosureTests
    {
        private static readonly ViiperDriverReadinessState[] States =
        {
            ViiperDriverReadinessState.Missing,
            ViiperDriverReadinessState.DetectedUnvalidated,
            ViiperDriverReadinessState.ValidatedExperimental,
            ViiperDriverReadinessState.Approved,
        };

        private static ViiperDriverReadiness Readiness(
            ViiperDriverReadinessState state, string releaseLabel) =>
            new ViiperDriverReadiness(state, Array.Empty<string>(),
                Array.Empty<ViiperDriverComponentIdentity>(), releaseLabel,
                state == ViiperDriverReadinessState.ValidatedExperimental
                    ? ViiperDriverTier.ExperimentalBaseline
                    : (ViiperDriverTier?)null,
                DateTimeOffset.UnixEpoch);

        /// <summary>
        /// Rule 1: name the actual failure. "May be unstable" is not a
        /// disclosure; "can stop Windows with a blue screen" is. With audio
        /// endpoints on by default, the one-time notice is where it is said.
        /// </summary>
        [TestMethod]
        public void TheNoticeNamesTheRealFailureMode()
        {
            string body = ViiperExperimentalDisclosure.AcknowledgementBody;

            StringAssert.Contains(body, "blue screen");
            StringAssert.Contains(body, "kernel");
            StringAssert.Contains(body, "torn down");
            StringAssert.Contains(body, "confirmed");
        }

        /// <summary>
        /// Rule 2: whose driver it is, and the honest limit of what this
        /// application can do about it. A user-mode program cannot fix a kernel
        /// driver, and a disclosure that implies otherwise is worse than none.
        /// </summary>
        [TestMethod]
        public void TheNoticeDisclaimsOwnershipAndAdmitsTheLimit()
        {
            string body = ViiperExperimentalDisclosure.AcknowledgementBody;

            StringAssert.Contains(body, "not developed by this project");
            StringAssert.Contains(body, "cannot catch that or recover from it");
        }

        /// <summary>
        /// Rule 4: the upstream report is referenced by number and link, and no
        /// sentence claims the reader's own release was examined. A machine with
        /// a package nobody has looked at must still read a true page.
        /// </summary>
        [TestMethod]
        public void TheNoticeCitesTheUpstreamIssueWithoutClaimingTheReadersRelease()
        {
            StringAssert.Contains(ViiperExperimentalDisclosure.AcknowledgementBody,
                "issue #181");
            StringAssert.Contains(ViiperExperimentalDisclosure.AcknowledgementBody,
                ViiperExperimentalDisclosure.UpstreamIssueUrl);

            foreach (string text in AllText())
            {
                Assert.IsFalse(
                    text.Contains("your version is affected",
                        StringComparison.OrdinalIgnoreCase),
                    "Claims something about the reader's exact release: " +
                    Excerpt(text, "your version"));
            }
        }

        /// <summary>
        /// The promise the gate has to keep: controller features work without
        /// audio endpoints. If the text did not say so, turning them off (or a
        /// release that cannot have them) would read as "half the app is
        /// disabled".
        /// </summary>
        [TestMethod]
        public void TheTextsPromiseControllersWorkWithoutAudio()
        {
            StringAssert.Contains(ViiperExperimentalDisclosure.AudioClassSummary,
                "do not need them");
            StringAssert.Contains(
                ViiperExperimentalDisclosure.AcknowledgementBody,
                "does not use the driver path that carries the known defect");
            StringAssert.Contains(
                ViiperVirtualDeviceGate.AudioClassNeedsFixedDriverReason,
                "still work");
            StringAssert.Contains(
                ViiperVirtualDeviceGate.AudioClassNotEnabledReason,
                "work without them");
        }

        /// <summary>
        /// Rule 3, first half: "validated" must never be readable as
        /// "approved". This is the sentence the VM pass made non-negotiable.
        /// </summary>
        [TestMethod]
        public void NoDisclosureLetsRecognitionReadAsApproval()
        {
            // Every sanctioned way this text may use the word. Anything else is
            // an affirmative approval claim, and there is no release to make one
            // about. Checked by deletion rather than by proximity matching so
            // the rule is a list a reviewer can read.
            string[] sanctioned =
            {
                "is not approved for production use by anyone",
                "that no one has approved for production use",
                "no usbip-win2 release is approved for production use today",
                "has no usbip-win2 release on its approved list",
                "not approving it",
            };

            var problems = new List<string>();
            foreach (string text in AllText())
            {
                string residue = sanctioned.Aggregate(text,
                    (current, phrase) => current.Replace(phrase,
                        string.Empty, StringComparison.OrdinalIgnoreCase));

                if (residue.Contains("approv", StringComparison.OrdinalIgnoreCase))
                {
                    problems.Add(Excerpt(residue, "approv"));
                }
            }

            Assert.AreEqual(0, problems.Count,
                "A disclosure claims something is approved:\n" +
                string.Join("\n", problems));

            // The line naming the installed package must actually carry it,
            // not merely avoid contradicting it.
            foreach (ViiperDriverReadinessState state in States)
            {
                StringAssert.Contains(
                    ViiperExperimentalDisclosure.InstalledPackageLine(
                        Readiness(state, "0.9.8.0")),
                    ViiperExperimentalDisclosure.NotApprovalLine);
            }
        }

        /// <summary>
        /// Rule 3, second half. The one link any of this text carries is the
        /// upstream defect report; a release page or a "get the latest build"
        /// would be a recommendation the manifest cannot back.
        /// </summary>
        [TestMethod]
        public void NoDisclosureRecommendsAnUnlistedPackage()
        {
            string[] forbidden =
            {
                "download", "latest version", "newest", "upgrade to",
                "update usbip", "install a newer", "/releases",
            };

            var problems = (from text in AllText()
                            from phrase in forbidden
                            where text.Contains(phrase,
                                StringComparison.OrdinalIgnoreCase)
                            select $"\"{phrase}\" in: {Excerpt(text, phrase)}")
                           .ToList();

            Assert.AreEqual(0, problems.Count, string.Join("\n", problems));
        }

        /// <summary>
        /// Every state names what is installed, including the ones with nothing
        /// to name. Silence would read as "fine".
        /// </summary>
        [TestMethod]
        public void EveryStateDescribesWhatIsInstalled()
        {
            Assert.AreEqual(4, States.Length);

            foreach (ViiperDriverReadinessState state in States)
            {
                string described = ViiperExperimentalDisclosure.DescribeInstalled(
                    Readiness(state, "0.9.7.8"));

                Assert.IsFalse(string.IsNullOrWhiteSpace(described),
                    $"{state} describes nothing.");

                if (state == ViiperDriverReadinessState.ValidatedExperimental ||
                    state == ViiperDriverReadinessState.Approved)
                {
                    StringAssert.Contains(described, "0.9.7.8",
                        $"{state} does not name the release.");
                }
            }

            StringAssert.Contains(
                ViiperExperimentalDisclosure.DescribeInstalled(null),
                "not checked yet");
            StringAssert.Contains(
                ViiperExperimentalDisclosure.DescribeInstalled(
                    Readiness(ViiperDriverReadinessState.ValidatedExperimental,
                        null)),
                "release not reported");
        }

        /// <summary>
        /// The Settings line and the log line the gate writes on an old
        /// release have to describe the same thing, or a user who read one and
        /// then saw the other will not connect them.
        /// </summary>
        [TestMethod]
        public void TheSummaryAndTheRefusalDescribeTheSameRisk()
        {
            foreach (string text in new[]
            {
                ViiperExperimentalDisclosure.AudioClassSummary,
                ViiperVirtualDeviceGate.AudioClassNeedsFixedDriverReason,
            })
            {
                StringAssert.Contains(text, "#181");
                StringAssert.Contains(text,
                    ViiperExperimentalDisclosure.FixedInReleaseLabel);
            }

            StringAssert.Contains(
                ViiperVirtualDeviceGate.AudioClassNeedsFixedDriverReason,
                "crash Windows");
            StringAssert.Contains(
                ViiperVirtualDeviceGate.AudioClassNeedsFixedDriverReason,
                "torn down");
        }

        /// <summary>
        /// Rule 4 after usbip-win2 0.9.8.0 (2026-09-07) shipped the fixes: the
        /// text names the fixed release as a fact about upstream, and says the
        /// endpoints are on by default with it and never created on an earlier
        /// release. Only a manifest-matched package at or past it counts: an
        /// unidentified package is never assumed fixed, whatever its version
        /// string claims.
        /// </summary>
        [TestMethod]
        public void TheAudioDisclosureKnowsWhichReleasesCarryTheFixes()
        {
            Assert.AreEqual("0.9.8.0",
                ViiperExperimentalDisclosure.FixedInReleaseLabel);

            Assert.IsTrue(ViiperExperimentalDisclosure.CarriesUpstreamFixes(
                Readiness(ViiperDriverReadinessState.ValidatedExperimental, "0.9.8.0")));
            Assert.IsTrue(ViiperExperimentalDisclosure.CarriesUpstreamFixes(
                Readiness(ViiperDriverReadinessState.Approved, "0.9.9.0")));
            Assert.IsFalse(ViiperExperimentalDisclosure.CarriesUpstreamFixes(
                Readiness(ViiperDriverReadinessState.ValidatedExperimental, "0.9.7.8")));
            Assert.IsFalse(ViiperExperimentalDisclosure.CarriesUpstreamFixes(
                Readiness(ViiperDriverReadinessState.DetectedUnvalidated, "0.9.8.0")),
                "An unidentified package is never assumed fixed.");
            Assert.IsFalse(ViiperExperimentalDisclosure.CarriesUpstreamFixes(null));

            string notice = ViiperExperimentalDisclosure.AcknowledgementBody;
            StringAssert.Contains(notice, "0.9.8.0 carries the upstream fixes");
            StringAssert.Contains(notice, "on by default");
            StringAssert.Contains(notice, "never creates them on an earlier release");
            StringAssert.Contains(notice, "turn them off in Settings");

            StringAssert.Contains(ViiperExperimentalDisclosure.AudioClassSummary,
                "On by default");
            StringAssert.Contains(ViiperExperimentalDisclosure.AudioClassSummary,
                "0.9.8.0");
            StringAssert.Contains(
                ViiperVirtualDeviceGate.AudioClassNeedsFixedDriverReason,
                "0.9.8.0 or later");
        }

        private static IEnumerable<string> AllText()
        {
            yield return ViiperExperimentalDisclosure.AcknowledgementBody;
            yield return ViiperExperimentalDisclosure.AcknowledgementSummary;
            yield return ViiperExperimentalDisclosure.AcknowledgementTitle;
            yield return ViiperExperimentalDisclosure.AudioClassSummary;
            yield return ViiperExperimentalDisclosure.NotApprovalLine;
            yield return ViiperVirtualDeviceGate.AudioClassNotEnabledReason;
            yield return ViiperVirtualDeviceGate.AudioClassNeedsFixedDriverReason;

            foreach (ViiperDriverReadinessState state in States)
            {
                foreach (string release in new[] { "0.9.7.8", "0.9.8.0" })
                {
                    yield return ViiperExperimentalDisclosure.InstalledPackageLine(
                        Readiness(state, release));
                    yield return ViiperExperimentalDisclosure.DescribeInstalled(
                        Readiness(state, release));
                }
            }
        }

        private static string Excerpt(string text, string phrase)
        {
            int at = text.IndexOf(phrase, StringComparison.OrdinalIgnoreCase);
            int from = Math.Max(0, at - 40);
            int length = Math.Min(text.Length - from, phrase.Length + 80);
            return text.Substring(from, length);
        }
    }
}
