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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace DS4WinWPF.DS4Forms
{
    /// <summary>
    /// Interaction logic for DupBox.xaml
    /// </summary>
    public partial class DupBox : UserControl
    {
        private string oldfilename;
        public string OldFilename
        {
            get => oldfilename;
            set
            {
                oldfilename = value;
                // The box used to keep the previous duplicate's name, so a
                // second Duplicate + Save overwrote that profile.
                profileTxt.Text = string.Empty;
            }
        }

        public event EventHandler Cancel;
        public delegate void SaveHandler(DupBox sender, string profilename);
        public event SaveHandler Save;

        public DupBox()
        {
            InitializeComponent();
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            string profile = profileTxt.Text;
            if (!string.IsNullOrWhiteSpace(profile) &&
                profile.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) == -1)
            {
                string profilesDir = System.IO.Path.Combine(DS4Windows.Global.appdatapath, "Profiles");
                string destination = System.IO.Path.Combine(profilesDir, profile + ".xml");
                // Copying used overwrite:true with no check, so an existing
                // profile was silently replaced, and copying onto the source's
                // own name threw and closed the app.
                if (string.Equals(profile, oldfilename, StringComparison.OrdinalIgnoreCase) ||
                    System.IO.File.Exists(destination))
                {
                    MessageBox.Show(Window.GetWindow(this) ?? Application.Current.MainWindow,
                        $"A profile named \"{profile}\" already exists. Choose another name.",
                        "Duplicate Profile", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                try
                {
                    System.IO.File.Copy(System.IO.Path.Combine(profilesDir, oldfilename + ".xml"),
                        destination, false);
                }
                catch (Exception ex) when (ex is System.IO.IOException || ex is UnauthorizedAccessException)
                {
                    MessageBox.Show(Window.GetWindow(this) ?? Application.Current.MainWindow,
                        $"Thrum could not copy the profile: {ex.Message}",
                        "Duplicate Profile", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Save?.Invoke(this, profile);
            }
            else
            {
                MessageBox.Show(Properties.Resources.ValidName, Properties.Resources.NotValid,
                    MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            Cancel?.Invoke(this, EventArgs.Empty);
        }
    }
}
