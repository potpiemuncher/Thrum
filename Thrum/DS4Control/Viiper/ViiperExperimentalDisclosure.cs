/*
DS4Windows
Copyright (C) 2026  DS4Windows contributors

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

using System.Text;

namespace DS4Windows
{
    /// <summary>
    /// The words shown before a user turns on something that runs on the
    /// experimental kernel driver.
    ///
    /// <para><b>This is policy text, not copy.</b> It lives in one place, as
    /// plain strings, so that it can be asserted by tests rather than reviewed
    /// by eye, and so the same sentences appear in the dialog, in the Settings
    /// card and in the log. Four rules, all enforced in
    /// <c>ViiperExperimentalDisclosureTests</c>:</para>
    /// <list type="number">
    /// <item>Say what the risk actually is — a confirmed defect in somebody
    /// else's kernel driver that can bugcheck Windows — without dressing it up
    /// as a generic "may be unstable".</item>
    /// <item>Say whose defect it is, and admit the limit of what this
    /// application can do about it. It cannot fix a kernel driver from user
    /// mode, and pretending otherwise is the failure this whole phase exists to
    /// avoid.</item>
    /// <item>Never recommend a package the manifest does not list, and never let
    /// "validated" read as "approved".</item>
    /// <item>Stay true on a machine with a different package installed. The
    /// upstream issue is referenced by number and link; no sentence claims the
    /// reader's exact release is or is not affected beyond what was
    /// observed.</item>
    /// </list>
    /// </summary>
    public static class ViiperExperimentalDisclosure
    {
        /// <summary>
        /// The upstream defect report. Referenced rather than summarised as
        /// fact about the reader's machine, because the reader may have a
        /// release nobody has examined.
        /// </summary>
        public const string UpstreamIssueUrl =
            "https://github.com/vadimgrn/usbip-win2/issues/181";

        public const string AcknowledgementTitle =
            ProductInfo.ProductName + " - experimental virtual controller backend";

        public const string AudioClassTitle =
            ProductInfo.ProductName + " - virtual audio endpoints carry a kernel-crash risk";

        /// <summary>
        /// Shown once, the first time the user asks for any VIIPER virtual
        /// output. Covers the backend as a whole and says nothing about audio:
        /// controller-only emulation does not reach the known defect, and
        /// overstating that here would make the audio disclosure meaningless.
        /// </summary>
        public const string AcknowledgementBody =
            "Virtual controllers in " + ProductInfo.ProductName +
            " are presented to Windows through usbip-win2, a third-party " +
            "kernel-mode USB/IP driver that is not developed by this project " +
            "and is not approved for production use by anyone.\n\n" +
            "A kernel driver runs inside Windows itself. If it faults, Windows " +
            "stops with a blue screen; " + ProductInfo.ProductName +
            " cannot catch that or recover from it.\n\n" +
            "Plain controller output - buttons, sticks, triggers, rumble, " +
            "lightbar - does not use the driver path that carries the known " +
            "defect, and " + ProductInfo.ProductName +
            " has run those lifecycles cleanly in testing. Virtual audio and " +
            "microphone endpoints do use it, and stay switched off until you " +
            "enable them separately.\n\n" +
            "Continue and use virtual controllers?";

        /// <summary>
        /// The line that has to survive every rewrite: a manifest match is
        /// identity evidence, never approval.
        /// </summary>
        public const string NotApprovalLine =
            "Recognising a package is not approving it. " +
            ProductInfo.ProductName +
            " has no usbip-win2 release on its approved list, and does not " +
            "suggest installing any release other than the one its own setup " +
            "installs.";

        /// <summary>
        /// The per-enablement confirmation for anything that creates or opens a
        /// virtual USB audio or microphone endpoint. Shown every time the
        /// setting is switched on, not once: the risk does not decrease with
        /// familiarity, and the installed package can change between sessions.
        /// </summary>
        /// <param name="readiness">
        /// Session readiness, used only to name what is installed. Null renders
        /// the "could not be identified" wording rather than omitting the fact.
        /// </param>
        public static string BuildAudioClassBody(ViiperDriverReadiness readiness)
        {
            StringBuilder text = new StringBuilder();

            text.Append("You are about to let ").Append(ProductInfo.ProductName)
                .Append(" create virtual USB audio and microphone endpoints ")
                .Append("(controller speaker, headset jack and pad microphone) ")
                .Append("through the usbip-win2 kernel driver.\n\n");

            text.Append("The risk, plainly: usbip-win2 has a confirmed defect ")
                .Append("in how it retires in-flight USB requests. When a ")
                .Append("virtual audio endpoint is torn down - closing a game, ")
                .Append("switching profiles, unplugging the pad, shutting down ")
                .Append("- an audio transfer that completes at the same moment ")
                .Append("can corrupt kernel memory and stop Windows with a blue ")
                .Append("screen. It has been reproduced on this project's own ")
                .Append("hardware.\n\n");

            text.Append("This is a defect in usbip-win2, not in ")
                .Append(ProductInfo.ProductName)
                .Append(". It is reported upstream as usbip-win2 issue #181 (")
                .Append(UpstreamIssueUrl).Append("). ")
                .Append(ProductInfo.ProductName)
                .Append(" orders its own teardown as carefully as it can, but ")
                .Append("the fault is inside the kernel driver and cannot be ")
                .Append("fully prevented from outside it. No usbip-win2 release ")
                .Append("is known to have fixed it.\n\n");

            text.Append("Installed package: ").Append(DescribeInstalled(readiness))
                .Append("\n").Append(NotApprovalLine).Append("\n\n");

            text.Append("You do not need this for controller support. Buttons, ")
                .Append("sticks, triggers, rumble, gyro, touchpad and lightbar ")
                .Append("all work with these endpoints switched off, and that ")
                .Append("configuration does not reach the defect.\n\n");

            text.Append("Turn virtual audio endpoints on?");

            return text.ToString();
        }

        /// <summary>
        /// What the machine actually has, in one clause. Deliberately never
        /// says "up to date" or "newer available": the manifest, not the
        /// version number, decides what is recognised.
        /// </summary>
        public static string DescribeInstalled(ViiperDriverReadiness readiness)
        {
            if (readiness == null)
            {
                return "not checked yet, so nothing is known about it.";
            }

            switch (readiness.State)
            {
                case ViiperDriverReadinessState.Missing:
                    return "none. No usbip-win2 driver is installed on this " +
                        "machine.";
                case ViiperDriverReadinessState.ValidatedExperimental:
                    return "usbip-win2 " + Release(readiness) +
                        ", an experimental baseline " + ProductInfo.ProductName +
                        " recognises.";
                case ViiperDriverReadinessState.Approved:
                    return "usbip-win2 " + Release(readiness) +
                        ", a release accepted for production use.";
                default:
                    return "a usbip-win2 driver that " + ProductInfo.ProductName +
                        " could not identify. See the driver status card in " +
                        "Settings for what could not be confirmed.";
            }
        }

        /// <summary>
        /// The short line the Settings checkbox carries next to itself, and the
        /// one written to the log when audio-class output is refused.
        /// </summary>
        public const string AudioClassSummary =
            "Off by default. Virtual speaker and microphone endpoints reach a " +
            "confirmed usbip-win2 kernel defect (upstream issue #181) that can " +
            "stop Windows with a blue screen when an endpoint is torn down. " +
            "Controller input, rumble and adaptive triggers do not need them.";

        /// <summary>
        /// The short line next to the experimental-backend checkbox.
        /// </summary>
        public const string AcknowledgementSummary =
            "Virtual controllers need usbip-win2, a third-party kernel driver " +
            "that no one has approved for production use. Turning this on " +
            "records that you have read what that means.";

        private static string Release(ViiperDriverReadiness readiness) =>
            string.IsNullOrWhiteSpace(readiness.ReleaseLabel)
                ? "(release not reported)"
                : readiness.ReleaseLabel;
    }
}
