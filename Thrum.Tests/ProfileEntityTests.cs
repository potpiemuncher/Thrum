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

using DS4WinWPF;
using System.ComponentModel;

namespace DS4WindowsTests;

/// <summary>
/// ProfileEntity must implement INotifyPropertyChanged. Without it WPF binds
/// its Name through a PropertyDescriptor and hooks NameChanged by reflection,
/// and a ComboBox clearing its selection then hands that descriptor an empty
/// string: TargetException, process gone (Overview, service stop, 2026-09-07).
/// </summary>
[TestClass]
public class ProfileEntityTests
{
    [TestMethod]
    public void ProfileEntityNotifiesThroughINotifyPropertyChanged()
    {
        var entity = new ProfileEntity();
        Assert.IsInstanceOfType(entity, typeof(INotifyPropertyChanged),
            "WPF must bind Name through PropertyInfo, not a reflective descriptor.");

        string changed = null;
        int legacyRaised = 0;
        ((INotifyPropertyChanged)entity).PropertyChanged += (_, e) => changed = e.PropertyName;
        entity.NameChanged += (_, _) => legacyRaised++;

        entity.Name = "Default";
        Assert.AreEqual("Name", changed);
        Assert.AreEqual(1, legacyRaised);
        Assert.AreEqual("Default", entity.ToString());

        changed = null;
        entity.Name = "Default";
        Assert.IsNull(changed, "Setting the same name must not notify.");
    }
}
