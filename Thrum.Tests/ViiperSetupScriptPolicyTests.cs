using System;
using System.Text;
using DS4Windows;

namespace DS4WindowsTests
{
    [TestClass]
    public class ViiperSetupScriptPolicyTests
    {
        private const string Publisher = "Example Publisher";
        private const string ScriptPath = @"C:\Users\Test\Thrum\extras\install-viiper-backend.ps1";

        private static ViiperSignatureTrust Signed(string signer) =>
            new ViiperSignatureTrust { Trusted = true, ObservedSignerCommonName = signer };

        private static ViiperSignatureTrust Unsigned() =>
            ViiperSignatureTrust.Untrusted("no valid signature");

        [TestMethod]
        public void UnsignedBuildKeepsBypass()
        {
            var decision = ViiperSetupScriptPolicy.Decide(Unsigned(), Unsigned());

            Assert.IsTrue(decision.Launch);
            Assert.AreEqual("Bypass", decision.ExecutionPolicy);
            Assert.AreEqual(
                $"-NoProfile -ExecutionPolicy Bypass -File \"{ScriptPath}\" -NoPause",
                ViiperSetupScriptPolicy.Arguments(decision, ScriptPath));
        }

        [TestMethod]
        public void SignedBuildRunsTheScriptUnderAllSigned()
        {
            var decision = ViiperSetupScriptPolicy.Decide(Signed(Publisher), Signed(Publisher));

            Assert.IsTrue(decision.Launch);
            Assert.AreEqual("AllSigned", decision.ExecutionPolicy);
            string arguments = ViiperSetupScriptPolicy.Arguments(decision, ScriptPath);
            StringAssert.StartsWith(arguments, "-NoProfile -ExecutionPolicy AllSigned -EncodedCommand ");
            Assert.IsFalse(arguments.Contains("Bypass"));

            string command = Decode(arguments);
            StringAssert.Contains(command, $"& '{ScriptPath}' -NoPause");
            StringAssert.Contains(command, $"software from {Publisher}. Type R (Run once)");
            StringAssert.Contains(command, "exit $LASTEXITCODE");
            StringAssert.Contains(command, "Read-Host 'Press Enter to close'");
        }

        [TestMethod]
        public void SignedBuildWithAnUnsignedScriptDoesNotRunIt()
        {
            var decision = ViiperSetupScriptPolicy.Decide(Signed(Publisher), Unsigned());

            Assert.IsFalse(decision.Launch);
            StringAssert.Contains(decision.Refusal, "is not signed, so it was not run");
        }

        [TestMethod]
        public void SignedBuildWithAScriptFromAnotherPublisherDoesNotRunIt()
        {
            var decision = ViiperSetupScriptPolicy.Decide(Signed(Publisher), Signed("Someone Else"));

            Assert.IsFalse(decision.Launch);
            StringAssert.Contains(decision.Refusal, "is signed by \"Someone Else\"");
        }

        [TestMethod]
        public void RevocationOfflineStillCountsAsASignedBuild()
        {
            // No chain verdict, but a signature and signer were found.
            var app = ViiperSignatureTrust.Untrusted("WinVerifyTrust hr=0x80092013", Publisher);

            var decision = ViiperSetupScriptPolicy.Decide(app, Unsigned());

            Assert.IsFalse(decision.Launch);
        }

        [TestMethod]
        public void QuotesInThePathCannotEndTheString()
        {
            var decision = ViiperSetupScriptPolicy.Decide(Signed(Publisher), Signed(Publisher));

            string command = Decode(ViiperSetupScriptPolicy.Arguments(decision,
                @"C:\Users\O'Brien\Thrum\setup.ps1"));

            StringAssert.Contains(command, @"& 'C:\Users\O''Brien\Thrum\setup.ps1' -NoPause");
            Assert.AreEqual("a\u2019\u2019b", ViiperSetupScriptPolicy.Quote("a\u2019b"));
        }

        private static string Decode(string arguments)
        {
            string encoded = arguments.Substring(arguments.LastIndexOf(' ') + 1);
            return Encoding.Unicode.GetString(Convert.FromBase64String(encoded));
        }
    }
}
