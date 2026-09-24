/*
DS4Windows
Copyright (C) 2026 hbashton

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.
*/

using System;
using System.Diagnostics;
using System.Windows;

namespace DS4WinWPF.DS4Forms
{
    public partial class WelcomeDialog : Window
    {
        // The optional drivers come from their vendors' release pages. This
        // dialog used to download the installers to %TEMP% and run them as
        // administrator with no integrity check, which a same-user process
        // could swap between download and launch (and which Defender flags).
        private const string HidHideDownloadPage =
            "https://github.com/nefarius/HidHide/releases/latest";
        private const string FakerInputDownloadPage =
            "https://github.com/Ryochan7/FakerInput/releases/latest";

        public WelcomeDialog(bool loadConfig = false)
        {
            if (loadConfig)
            {
                DS4Windows.Global.FindConfigLocation();
                DS4Windows.Global.Load();
            }

            InitializeComponent();
            step4HidHidePanel.IsEnabled = IsHidHideCompatible();
            step5FakerInputPanel.IsEnabled = DS4Windows.Global.IsWin8OrGreater();

            DS4Windows.ViiperPrerequisiteStatus status =
                DS4Windows.ViiperSetupManager.GetStatus(tryStartServer: true);
            if (status.Ready)
            {
                viiperInstallBtn.Content = "VIIPER is ready";
            }
        }

        private void ViiperInstallBtn_Click(object sender, RoutedEventArgs e)
        {
            DS4Windows.ViiperPrerequisiteStatus status =
                DS4Windows.ViiperSetupManager.GetStatus(tryStartServer: true);
            if (status.Ready)
            {
                viiperInstallBtn.Content = "VIIPER is ready";
                return;
            }

            bool launched = DS4Windows.ViiperSetupManager.LaunchInstaller(status, this);
            viiperInstallBtn.Content = launched
                ? "Setup opened — finish it, then click here to verify"
                : "VIIPER setup needs attention";
        }

        private void HidHideInstall_Click(object sender, RoutedEventArgs e) =>
            OpenDownloadPage(HidHideDownloadPage, hidHideInstallBtn, "HidHide");

        private void FakerInputInstallBtn_Click(object sender, RoutedEventArgs e) =>
            OpenDownloadPage(FakerInputDownloadPage, fakerInputInstallBtn,
                "FakerInput");

        private static void OpenDownloadPage(string url,
            System.Windows.Controls.Button button, string componentName)
        {
            DS4Windows.Util.StartProcessHelper(url);
            button.Content =
                $"{componentName} download page opened. Run its installer, then click Finished.";
        }

        private static bool IsHidHideCompatible() =>
            DS4Windows.Global.IsWin10OrGreater() &&
            Environment.Is64BitOperatingSystem;

        private void Step2Btn_Click(object sender, RoutedEventArgs e) =>
            DS4Windows.Util.StartProcessHelper(
                "https://support.xbox.com/help/hardware-network/controller/connect-xbox-wireless-controller-to-pc");

        private void BluetoothSetLink_Click(object sender,
            RoutedEventArgs e) => Process.Start("control", "bthprops.cpl");

        private void FinishedBtn_Click(object sender, RoutedEventArgs e) =>
            Close();
    }

    public class WelcomeDialogResourcePaths
    {
        public string PairmodePNG =>
            $"{DS4Windows.Global.RESOURCES_PREFIX}/Pairmode.png";
    }
}
