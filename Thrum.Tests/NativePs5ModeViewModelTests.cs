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
using DS4WinWPF.DS4Forms.ViewModels;

namespace DS4WindowsTests;

/// <summary>
/// The Overview "Native PS5 mode" card (design handoff N1). The state table
/// is policy: the assertions here are the six rows of that table plus the
/// one rule the brief underlines - a DualSense on Bluetooth with Audio
/// Haptics on and no virtual audio endpoint is a working setup and reads
/// green.
/// </summary>
[TestClass]
public class NativePs5ModeViewModelTests
{
    private static NativePs5ModeInputs Inputs(
        bool hasDualSense = true,
        bool edge = false,
        OutContType output = OutContType.ViiperX360,
        ViiperDriverReadinessState? driver = ViiperDriverReadinessState.ValidatedExperimental,
        bool backendReady = true,
        bool acknowledged = true,
        bool audioAllowed = false,
        bool wireless = true,
        bool hapticsEnabled = false,
        AudioHapticsSourceKind source = AudioHapticsSourceKind.SystemAudio,
        NativePs5HidHideStatus hidHide = NativePs5HidHideStatus.Hiding) =>
        new(hasDualSense, edge, output, driver, backendReady, acknowledged,
            audioAllowed, wireless, hapticsEnabled, source, hidHide);

    private static NativePs5ModeViewModel Card(NativePs5ModeInputs inputs)
    {
        var card = new NativePs5ModeViewModel();
        card.Apply(inputs);
        return card;
    }

    [TestMethod]
    public void State1_Off_WhenEverythingIsMetAndOutputIsNotADualSense()
    {
        NativePs5ModeViewModel card = Card(Inputs());

        Assert.AreEqual(NativePs5ModeState.Off, card.State);
        Assert.IsFalse(card.IsOn);
        Assert.AreEqual("Off", card.BadgeText);
        Assert.AreEqual("Muted", card.BadgeKind);
        Assert.AreEqual("Neutral", card.CardBorderKind);
        Assert.AreEqual("Xbox 360 (virtual)", card.GamesSeeText);
        Assert.IsFalse(card.ShowSetupButton);
        Assert.IsFalse(card.HasSecondaryLine);
        StringAssert.Contains(card.PrimaryLine, "walks you through");
    }

    [TestMethod]
    public void State2_NeedsSetup_WhenTheDriverIsMissing()
    {
        NativePs5ModeViewModel card = Card(Inputs(
            driver: ViiperDriverReadinessState.Missing, backendReady: false));

        Assert.AreEqual(NativePs5ModeState.NeedsSetup, card.State);
        Assert.AreEqual("Not installed", card.BadgeText);
        Assert.AreEqual("Muted", card.BadgeKind);
        Assert.IsTrue(card.ShowSetupButton);
        Assert.AreEqual("Set up Native PS5 mode", card.SetupButtonText);
        StringAssert.Contains(card.PrimaryLine, "install step");
    }

    [TestMethod]
    public void State2_NeedsSetup_UnverifiedPackageReadsAsDanger()
    {
        NativePs5ModeViewModel card = Card(Inputs(
            driver: ViiperDriverReadinessState.DetectedUnvalidated));

        Assert.AreEqual(NativePs5ModeState.NeedsSetup, card.State);
        Assert.AreEqual("Unverified", card.BadgeText);
        Assert.AreEqual("Danger", card.BadgeKind);
        StringAssert.Contains(card.PrimaryLine, "could not confirm");
        StringAssert.Contains(card.PrimaryLine, "No new virtual controller");
    }

    [TestMethod]
    public void State2_NeedsSetup_WhenTheBackendIsNotRunning()
    {
        NativePs5ModeViewModel card = Card(Inputs(backendReady: false));

        Assert.AreEqual(NativePs5ModeState.NeedsSetup, card.State);
        Assert.AreEqual("Backend not running", card.BadgeText);
        StringAssert.Contains(card.PrimaryLine, "Repair");
    }

    [TestMethod]
    public void State2_NeedsSetup_BeforeTheDriverHasBeenChecked()
    {
        NativePs5ModeViewModel card = Card(Inputs(driver: null));

        Assert.AreEqual(NativePs5ModeState.NeedsSetup, card.State);
        Assert.AreEqual("Checking", card.BadgeText);
        Assert.AreEqual("Muted", card.BadgeKind);
    }

    [TestMethod]
    public void State3_NeedsConsent_WhenTheKnownPackageIsNotAcknowledged()
    {
        NativePs5ModeViewModel card = Card(Inputs(acknowledged: false));

        Assert.AreEqual(NativePs5ModeState.NeedsConsent, card.State);
        Assert.AreEqual("Experimental - known package", card.BadgeText);
        Assert.AreEqual("Warning", card.BadgeKind);
        Assert.AreEqual("Neutral", card.CardBorderKind);
        Assert.IsTrue(card.ShowSetupButton);
        Assert.AreEqual("Read and accept", card.SetupButtonText);
        StringAssert.Contains(card.PrimaryLine, "not production-approved");
    }

    [TestMethod]
    public void State4_On_OverUsbWithoutAudioConsent()
    {
        NativePs5ModeViewModel card = Card(Inputs(
            output: OutContType.ViiperDualSense, wireless: false));

        Assert.AreEqual(NativePs5ModeState.On, card.State);
        Assert.IsTrue(card.IsOn);
        Assert.AreEqual("On", card.BadgeText);
        Assert.AreEqual("Success", card.BadgeKind);
        Assert.AreEqual("Success", card.CardBorderKind);
        Assert.AreEqual("DualSense (virtual)", card.GamesSeeText);
        Assert.AreEqual(" · physical pad hidden by HidHide", card.GamesSeeSuffix);
        Assert.IsTrue(card.HasSecondaryLine);
        StringAssert.Contains(card.SecondaryLine, "not torn down");
        Assert.IsFalse(card.ShowSetupButton);
        Assert.IsFalse(card.ShowHidHideWarning);
        Assert.IsFalse(card.ShowAudioEndpointsLine);
    }

    [TestMethod]
    public void State4_On_MentionsStep4WhenAudioHapticsIsEnabledOnUsb()
    {
        NativePs5ModeViewModel card = Card(Inputs(
            output: OutContType.ViiperDualSense, wireless: false,
            hapticsEnabled: true));

        Assert.AreEqual(NativePs5ModeState.On, card.State);
        StringAssert.Contains(card.PrimaryLine, "step 4");
    }

    [TestMethod]
    public void State5_BluetoothHapticsWithoutVirtualAudioReadsGreen()
    {
        // The rule the brief underlines: this is the safe, working default.
        NativePs5ModeViewModel card = Card(Inputs(
            output: OutContType.ViiperDualSense, wireless: true,
            hapticsEnabled: true, source: AudioHapticsSourceKind.SystemAudio));

        Assert.AreEqual(NativePs5ModeState.OnHapticsBluetooth, card.State);
        Assert.AreEqual("On · haptics via Bluetooth", card.BadgeText);
        Assert.AreEqual("Success", card.BadgeKind);
        Assert.AreEqual("Success", card.CardBorderKind);
        StringAssert.Contains(card.PrimaryLine, "needs no driver");
        Assert.IsFalse(card.ShowAudioEndpointsLine);
    }

    [TestMethod]
    public void State5_AlsoForAnEndpointSource_ButNotForAppSession()
    {
        Assert.AreEqual(NativePs5ModeState.OnHapticsBluetooth,
            NativePs5ModeViewModel.Evaluate(Inputs(
                output: OutContType.ViiperDualSense, hapticsEnabled: true,
                source: AudioHapticsSourceKind.Endpoint)));

        // The Bluetooth streamer does not serve app-session capture, so
        // that configuration is plain "On", not "haptics via Bluetooth".
        Assert.AreEqual(NativePs5ModeState.On,
            NativePs5ModeViewModel.Evaluate(Inputs(
                output: OutContType.ViiperDualSense, hapticsEnabled: true,
                source: AudioHapticsSourceKind.AppSession)));
    }

    [TestMethod]
    public void State6_VirtualPadHapticsCarriesTheRiskSentenceAndUnverifiedLabel()
    {
        NativePs5ModeViewModel card = Card(Inputs(
            output: OutContType.ViiperDualSense, wireless: false,
            audioAllowed: true));

        Assert.AreEqual(NativePs5ModeState.OnHapticsVirtualPad, card.State);
        Assert.AreEqual("Experimental, unverified", card.BadgeText);
        Assert.AreEqual("Warning", card.BadgeKind);
        Assert.AreEqual("Warning", card.CardBorderKind);
        Assert.IsTrue(card.ShowAudioEndpointsLine);
        Assert.AreEqual(ViiperExperimentalDisclosure.AudioClassSummary,
            card.AudioEndpointsRiskText);
        StringAssert.Contains(NativePs5ModeViewModel.AudioEndpointsUnverifiedLine,
            "#65");
        StringAssert.Contains(card.GamesSeeSuffix, "endpoints on");
        StringAssert.Contains(card.PrimaryLine, "over USB");
    }

    [TestMethod]
    public void State6_AlsoOverBluetooth_AndOutranksAudioHapticsThere()
    {
        // 2026-09-09: the audio persona was exercised over Bluetooth on
        // usbip-win2 0.9.8.0, so the USB-only predicate went. With the
        // endpoints allowed, the game owns the pad's haptics on either link
        // and Audio Haptics no longer decides the state.
        NativePs5ModeViewModel card = Card(Inputs(
            output: OutContType.ViiperDualSense, wireless: true,
            audioAllowed: true, hapticsEnabled: true,
            source: AudioHapticsSourceKind.SystemAudio));

        Assert.AreEqual(NativePs5ModeState.OnHapticsVirtualPad, card.State);
        Assert.AreEqual("Experimental, unverified", card.BadgeText);
        Assert.IsTrue(card.ShowAudioEndpointsLine);
        StringAssert.Contains(card.PrimaryLine, "relayed over Bluetooth");
        Assert.IsFalse(card.GamesSeeSuffix.Contains("USB"),
            "The suffix must not name a transport the state no longer requires.");
    }

    [TestMethod]
    public void AudioConsentOverBluetoothWithoutAudioHapticsIsTheVirtualPadState()
    {
        // Until 2026-09-09 state 6 was the USB route only. The audio persona
        // now relays game-authored haptics over Bluetooth too, so consent
        // alone selects it on either link.
        Assert.AreEqual(NativePs5ModeState.OnHapticsVirtualPad,
            NativePs5ModeViewModel.Evaluate(Inputs(
                output: OutContType.ViiperDualSense, wireless: true,
                audioAllowed: true)));
    }

    [TestMethod]
    public void HidHideWarningIsAdviceNotABlocker()
    {
        NativePs5ModeViewModel missing = Card(Inputs(
            output: OutContType.ViiperDualSense,
            hidHide: NativePs5HidHideStatus.NotInstalled));
        Assert.IsTrue(missing.ShowHidHideWarning);
        StringAssert.Contains(missing.HidHideWarningText, "not installed");
        Assert.AreEqual(string.Empty, missing.GamesSeeSuffix);
        Assert.AreEqual(NativePs5ModeState.On, missing.State,
            "A missing HidHide changes nothing about the mode itself.");

        NativePs5ModeViewModel shared = Card(Inputs(
            output: OutContType.ViiperDualSense,
            hidHide: NativePs5HidHideStatus.NotHidingThisPad));
        Assert.IsTrue(shared.ShowHidHideWarning);
        StringAssert.Contains(shared.HidHideWarningText, "not hiding this pad");
        StringAssert.Contains(shared.HidHideWarningText, "Hide DS4 Controller");

        Assert.IsFalse(Card(Inputs(hidHide: NativePs5HidHideStatus.NotInstalled))
            .ShowHidHideWarning, "Nothing to hide from while the mode is off.");
    }

    [TestMethod]
    public void ProfileNamingADualSenseWhilePrerequisitesAreMissingShowsTheSwitchOnWithTheSetupBadge()
    {
        NativePs5ModeViewModel card = Card(Inputs(
            output: OutContType.ViiperDualSense,
            driver: ViiperDriverReadinessState.Missing, backendReady: false));

        Assert.IsTrue(card.IsOn, "The switch mirrors the profile.");
        Assert.AreEqual(NativePs5ModeState.NeedsSetup, card.State);
        Assert.AreEqual("Not installed", card.BadgeText);
        Assert.IsTrue(card.ShowSetupButton);
        Assert.AreEqual("On", card.SwitchLabel);
    }

    [TestMethod]
    public void AnEdgeIsPresentedAsAVirtualEdge()
    {
        NativePs5ModeViewModel card = Card(Inputs(edge: true,
            output: OutContType.ViiperDualSenseEdge));

        Assert.AreEqual("DualSense Edge", card.NativeDeviceName);
        Assert.AreEqual(OutContType.ViiperDualSenseEdge, card.NativeOutputType);
        Assert.AreEqual("DualSense Edge (virtual)", card.GamesSeeText);
        Assert.IsTrue(card.IsOn);

        NativePs5ModeViewModel plain = Card(Inputs());
        Assert.AreEqual(OutContType.ViiperDualSense, plain.NativeOutputType);
    }

    [TestMethod]
    public void GamesSeeNamesThePhysicalPadWhenThereIsNoVirtualOutput()
    {
        Assert.AreEqual("the physical pad only",
            Card(Inputs(output: OutContType.None)).GamesSeeText);
    }

    [TestMethod]
    public void OnlyADualSenseShowsTheCard()
    {
        Assert.IsFalse(Card(Inputs(hasDualSense: false)).IsVisible);
        Assert.IsTrue(Card(Inputs()).IsVisible);
        Assert.IsFalse(new NativePs5ModeViewModel().IsVisible,
            "No inputs published yet: nothing to show.");
    }

    [TestMethod]
    public void ApplyPublishesOnlyOnAChange()
    {
        var card = new NativePs5ModeViewModel();
        int raised = 0;
        card.PropertyChanged += (_, _) => raised++;

        Assert.IsTrue(card.Apply(Inputs()));
        Assert.AreEqual(1, raised);

        Assert.IsFalse(card.Apply(Inputs()),
            "Equal inputs must not republish: the Overview timer calls this four times a second.");
        Assert.AreEqual(1, raised);

        Assert.IsTrue(card.Apply(Inputs(wireless: false)));
        Assert.AreEqual(2, raised);
    }

    [TestMethod]
    public void SwitchToolTipExplainsWhatTheGestureDoes()
    {
        StringAssert.Contains(Card(Inputs()).SwitchToolTip, "Turn on");
        StringAssert.Contains(Card(Inputs(acknowledged: false)).SwitchToolTip,
            "setup");
        StringAssert.Contains(Card(Inputs(output: OutContType.ViiperDualSense))
            .SwitchToolTip, "Asks first");
    }

    [TestMethod]
    public void EvaluateCoversEveryStateNumberInTheHandoffTable()
    {
        Assert.AreEqual(1, (int)NativePs5ModeState.Off);
        Assert.AreEqual(2, (int)NativePs5ModeState.NeedsSetup);
        Assert.AreEqual(3, (int)NativePs5ModeState.NeedsConsent);
        Assert.AreEqual(4, (int)NativePs5ModeState.On);
        Assert.AreEqual(5, (int)NativePs5ModeState.OnHapticsBluetooth);
        Assert.AreEqual(6, (int)NativePs5ModeState.OnHapticsVirtualPad);
    }
}
