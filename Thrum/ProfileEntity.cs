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
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DS4WinWPF
{
    /// <summary>
    /// A profile as listed in the UI.
    ///
    /// <para><b>Why this implements <see cref="INotifyPropertyChanged"/>.</b>
    /// The Name/NameChanged pair alone makes WPF bind through a
    /// <c>PropertyDescriptor</c> and hook <c>NameChanged</c> by reflection.
    /// When a bound element's data item then turns into an empty string -
    /// which is what a ComboBox hands its selection box the moment the
    /// selection clears - WPF drills into the string as an empty collection,
    /// keeps the old descriptor, and reflectively adds the handler to its
    /// NullDataItem sentinel: <c>TargetException: Object does not match
    /// target type</c>, and the process dies. Seen on the Overview Active
    /// Profile combo when the service stopped (2026-09-07). With
    /// <see cref="INotifyPropertyChanged"/> WPF uses a plain PropertyInfo and
    /// never takes that path.</para>
    /// </summary>
    public class ProfileEntity : INotifyPropertyChanged
    {
        private string name;
        public string Name
        {
            get => name;
            set
            {
                if (name == value) return;
                name = value;
                NameChanged?.Invoke(this, EventArgs.Empty);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        }

        public event EventHandler NameChanged;
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler ProfileSaved;
        public event EventHandler ProfileDeleted;

        /// <summary>
        /// UI Automation uses ToString() as a list item's accessible name, so
        /// screen readers announce the profile name instead of the type name.
        /// </summary>
        public override string ToString() => name;

        public void DeleteFile()
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                string filepath = DS4Windows.Global.appdatapath + @"\Profiles\" + name + ".xml";
                if (File.Exists(filepath))
                {
                    File.Delete(filepath);
                    ProfileDeleted?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public void SaveProfile(int deviceNum)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                DS4Windows.Global.SaveProfile(deviceNum, name);
                DS4Windows.Global.CacheExtraProfileInfo(deviceNum);
            }
        }

        public void FireSaved()
        {
            ProfileSaved?.Invoke(this, EventArgs.Empty);
        }

        public void RenameProfile(string newProfileName)
        {
            string oldFilePath = Path.Combine(DS4Windows.Global.appdatapath,
                "Profiles", $"{name}.xml");

            string newFilePath = Path.Combine(DS4Windows.Global.appdatapath,
                "Profiles", $"{newProfileName}.xml");

            if (File.Exists(oldFilePath) && !File.Exists(newFilePath))
            {
                File.Move(oldFilePath, newFilePath);
                // Send NameChanged event so controls get updated with new name
                Name = newProfileName;
            }
        }
    }
}
