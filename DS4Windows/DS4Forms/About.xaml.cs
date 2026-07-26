/*
DS4Windows
Copyright (C) 2023  Travis Nickles

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
using System.Windows;

namespace DS4WinWPF.DS4Forms
{
    /// <summary>
    /// Interaction logic for About.xaml
    /// </summary>
    public partial class About : Window
    {
        public About()
        {
            InitializeComponent();

            headerLb.Content =
                $"{ProductInfo.ProductName} {Global.exeDisplayVersion}";

            // The warranty disclaimer is not decoration. GPLv3 section 5(d)
            // requires an interactive program that normally prints one to keep
            // doing so, and the About box is where this one lives.
            licenceTb.Text =
                $"{ProductInfo.ProductName} is free software: you may redistribute " +
                "it and modify it under the terms of the GNU General Public " +
                "License, version 3 or (at your option) any later version. It " +
                "comes with ABSOLUTELY NO WARRANTY, to the extent permitted by " +
                "law. The full licence text is on the License tab, and the " +
                "corresponding source for every release is published in the " +
                "project repository.";

            lineageTb.Text = $"What {ProductInfo.ProductName} is built on";
        }

        private void SourceLink_Click(object sender, RoutedEventArgs e)
        {
            Util.StartProcessHelper(ProductInfo.ProjectUri);
        }

        private void Ryochan7Link_Click(object sender, RoutedEventArgs e)
        {
            Util.StartProcessHelper("https://github.com/Ryochan7");
        }

        private void Jays2KingsLink_Click(object sender, RoutedEventArgs e)
        {
            Util.StartProcessHelper("https://github.com/Jays2Kings/");
        }

        private void InhexSTERLink_Click(object sender, RoutedEventArgs e)
        {
            Util.StartProcessHelper("https://code.google.com/p/ds4-tool/");
        }

        private void ElectrobrainsLink_Click(object sender, RoutedEventArgs e)
        {
            Util.StartProcessHelper("https://code.google.com/r/brianfundakowskifeldman-ds4windows/");
        }

        private void HidHideLink_Click(object sender, RoutedEventArgs e)
        {
            Util.StartProcessHelper("https://github.com/nefarius/HidHide/");
        }

        private void Crc32Link_Click(object sender, RoutedEventArgs e)
        {
            Util.StartProcessHelper("https://github.com/dariogriffo/Crc32");
        }

        private void OneEuroLink_Click(object sender, RoutedEventArgs e)
        {
            Util.StartProcessHelper("http://cristal.univ-lille.fr/~casiez/1euro/");
        }

        private void FakerInputLink_Click(object sender, RoutedEventArgs e)
        {
            Util.StartProcessHelper("https://github.com/Ryochan7/FakerInput/");
        }

        private void HNotifyIconLink_Click(object sender, RoutedEventArgs e)
        {
            Util.StartProcessHelper("https://github.com/HavenDV/H.NotifyIcon/");
        }

        private void VJoyInterfaceLink_Click(object sender, RoutedEventArgs e)
        {
            Util.StartProcessHelper("https://github.com/shauleiz/vJoy/tree/master/apps/common/vJoyInterfaceCS");
        }

        private void ContributorsLink_OnClick(object sender, RoutedEventArgs e)
        {
            Util.StartProcessHelper($"{ProductInfo.ProjectUri}/blob/main/contributors.txt");
        }

        private void LicenceTextLink_Click(object sender, RoutedEventArgs e)
        {
            Util.StartProcessHelper("https://www.gnu.org/licenses/gpl-3.0.html");
        }

        private void HbashtonLink_Click(object sender, RoutedEventArgs e)
        {
            Util.StartProcessHelper("https://github.com/hbashton/DS4Windows");
        }

        private void ViiperLink_Click(object sender, RoutedEventArgs e)
        {
            Util.StartProcessHelper("https://github.com/hbashton/VIIPER");
        }

        private void SchmaldeoLink_Click(object sender, RoutedEventArgs e)
        {
            Util.StartProcessHelper("https://github.com/schmaldeo/DS4Windows");
        }
    }
}
