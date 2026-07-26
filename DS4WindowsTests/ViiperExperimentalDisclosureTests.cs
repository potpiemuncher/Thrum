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
    /// improved without rewriting the suite.</para>
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
        /// disclosure; "can stop Windows with a blue screen" is.
        /// </summary>
        [TestMethod]
        public void TheAudioDisclosureNamesTheRealFailureMode()
        {
            string body = ViiperExperimentalDisclosure.BuildAudioClassBody(
                Readiness(ViiperDriverReadinessState.ValidatedExperimental,
                    "0.9.7.8"));

            StringAssert.Contains(body, "blue screen");
            StringAssert.Contains(body, "kernel");
            StringAssert.Contains(body, "torn down");
            StringAssert.Contains(body, "confirmed");
        }

        /// <summary>
        /// Rule 2: whose defect it is, and the honest limit of what this
        /// application can do about it. A user-mode program cannot fix a kernel
        /// driver, and a disclosure that implies otherwise is worse than none.
        /// </summary>
        [TestMethod]
        public void TheAudioDisclosureDisclaimsOwnershipAndAdmitsTheLimit()
        {
            string body = ViiperExperimentalDisclosure.BuildAudioClassBody(
                Readiness(ViiperDriverReadinessState.ValidatedExperimental,
                    "0.9.7.8"));

            StringAssert.Contains(body,
                "defect in usbip-win2, not in " + ProductInfo.ProductName);
            StringAssert.Contains(body, "cannot be fully prevented");
        }

        /// <summary>
        /// Rule 4: the upstream report is referenced by number and link, and no
        /// sentence claims the reader's own release was examined. A machine with
        /// a package nobody has looked at must still read a true page.
        /// </summary>
        [TestMethod]
        public void TheAudioDisclosureCitesTheUpstreamIssueWithoutClaimingTheReadersRelease()
        {
            foreach (ViiperDriverReadinessState state in States)
            {
                string body = ViiperExperimentalDisclosure.BuildAudioClassBody(
                    Readiness(state, "1.2.3.4"));

                StringAssert.Contains(body, "issue #181");
                StringAssert.Contains(body,
                    ViiperExperimentalDisclosure.UpstreamIssueUrl);

                Assert.IsFalse(
                    body.Contains("your version is affected",
                        StringComparison.OrdinalIgnoreCase),
                    $"{state}: claims something about the reader's exact release.");
            }
        }

        /// <summary>
        /// The promise the gate has to keep: controller features work without
        /// this. If the text did not say so, the default-off setting would read
        /// as "half the app is disabled".
        /// </summary>
        [TestMethod]
        public void BothDisclosuresPromiseControllersWorkWithoutAudio()
        {
            string audio = ViiperExperimentalDisclosure.BuildAudioClassBody(
                Readiness(ViiperDriverReadinessState.ValidatedExperimental,
                    "0.9.7.8"));

            StringAssert.Contains(audio, "do not need this for controller");
            StringAssert.Contains(
                ViiperExperimentalDisclosure.AcknowledgementBody,
                "does not use the driver path that carries the known defect");
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

            // The audio disclosure must actually carry the line, not merely
            // avoid contradicting it.
            StringAssert.Contains(
                ViiperExperimentalDisclosure.BuildAudioClassBody(
                    Readiness(ViiperDriverReadinessState.ValidatedExperimental,
                        "0.9.7.8")),
                ViiperExperimentalDisclosure.NotApprovalLine);
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
        /// The disclosure and the log line the gate produces have to describe
        /// the same thing, or a user who read one and then saw the other will
        /// not connect them.
        /// </summary>
        [TestMethod]
        public void TheSummaryAndTheRefusalDescribeTheSameRisk()
        {
            StringAssert.Contains(
                ViiperExperimentalDisclosure.AudioClassSummary, "#181");
            StringAssert.Contains(
                ViiperExperimentalDisclosure.AudioClassSummary, "blue screen");
            StringAssert.Contains(
                ViiperVirtualDeviceGate.AudioClassNotEnabledReason,
                "crash Windows");
            StringAssert.Contains(
                ViiperVirtualDeviceGate.AudioClassNotEnabledReason,
                "torn down");
        }

        private static IEnumerable<string> AllText()
        {
            yield return ViiperExperimentalDisclosure.AcknowledgementBody;
            yield return ViiperExperimentalDisclosure.AcknowledgementSummary;
            yield return ViiperExperimentalDisclosure.AcknowledgementTitle;
            yield return ViiperExperimentalDisclosure.AudioClassSummary;
            yield return ViiperExperimentalDisclosure.AudioClassTitle;
            yield return ViiperExperimentalDisclosure.NotApprovalLine;

            foreach (ViiperDriverReadinessState state in States)
            {
                yield return ViiperExperimentalDisclosure.BuildAudioClassBody(
                    Readiness(state, "0.9.7.8"));
                yield return ViiperExperimentalDisclosure.DescribeInstalled(
                    Readiness(state, "0.9.7.8"));
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
