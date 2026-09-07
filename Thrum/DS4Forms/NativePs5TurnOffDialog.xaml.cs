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

using System.Windows;

namespace DS4WinWPF.DS4Forms
{
    /// <summary>
    /// Asks before Native PS5 mode is turned off. Two outcomes besides
    /// "keep on": confirm the change, or leave the mode alone and go to
    /// Output Slots to unplug a running virtual pad by hand.
    /// </summary>
    public partial class NativePs5TurnOffDialog : Window
    {
        public NativePs5TurnOffDialog(string previousDeviceDisplayName)
        {
            InitializeComponent();
            PreviousDeviceRun.Text = previousDeviceDisplayName;
        }

        /// <summary>True when the user chose the Output Slots link instead.</summary>
        public bool OpenOutputSlotsRequested { get; private set; }

        private void TurnOffButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void OpenOutputSlotsButton_Click(object sender, RoutedEventArgs e)
        {
            OpenOutputSlotsRequested = true;
            DialogResult = false;
        }
    }
}
