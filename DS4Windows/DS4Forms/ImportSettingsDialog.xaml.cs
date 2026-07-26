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

using System.Windows;
using DS4Windows;

namespace DS4WinWPF.DS4Forms
{
    /// <summary>
    /// The one-time offer to copy an existing DS4Windows configuration into
    /// this product's data folder.
    ///
    /// <para><b>Shown once, and only on a pristine appdata install.</b> The
    /// caller decides; this window only asks. Declining is recorded by a marker
    /// file in the data folder, and anything other than pressing Import — the
    /// Start fresh button, Escape, or the title bar's close button — counts as
    /// declining, so the question cannot come back on the next launch.</para>
    ///
    /// <para><b>Portable installs never see this.</b> In portable mode the
    /// configuration folder is the executable's own directory: the user asked
    /// for a self-contained copy that can be moved to another machine, or run
    /// beside a real DS4Windows install for testing. Seeding that folder from
    /// one roaming profile's per-user state would quietly make the portable
    /// copy carry someone's configuration to wherever it travels, and the
    /// save-location dialog already offers to adopt a configuration that is
    /// sitting next to the executable. So the offer is gated on the resolved
    /// data folder being the appdata one, in <c>App.xaml.cs</c>.</para>
    ///
    /// <para>The text is English-only; the strings move to <c>.resx</c> with
    /// the rest of the user-visible prose in the localization pull
    /// request.</para>
    /// </summary>
    public partial class ImportSettingsDialog : Window
    {
        /// <summary>
        /// True only when the user pressed Import. The caller treats every
        /// other exit as a decline.
        /// </summary>
        public bool ImportRequested { get; private set; }

        public ImportSettingsDialog(ImportPlan plan)
        {
            InitializeComponent();

            Title = ProductInfo.ProductName + " - import existing settings";
            headingTxt.Text =
                $"Import your existing {ImportPlanner.LegacySourceFolderName} settings?";
            sourceTxt.Text =
                $"{ProductInfo.ProductName} keeps its configuration in its own " +
                $"folder, so it starts empty. An existing " +
                $"{ImportPlanner.LegacySourceFolderName} configuration was " +
                $"found in:\n{plan.SourceDirectory}";
            foundList.ItemsSource = ImportPlanSummary.Describe(plan);
            footerTxt.Text =
                "Importing copies these files. Nothing in the " +
                $"{ImportPlanner.LegacySourceFolderName} folder is changed, " +
                "moved or deleted, and that install keeps working. Older " +
                "profiles are upgraded as they load.\n\nThis is asked once.";

            // Import is the default button, so Enter takes it and Tab reaches
            // both buttons in order; Escape maps to Start fresh via IsCancel.
            // Focus lands on Import explicitly because the window has no other
            // focusable content.
            Loaded += (_, _) => importBtn.Focus();
        }

        private void ImportBtn_Click(object sender, RoutedEventArgs e)
        {
            ImportRequested = true;
            DialogResult = true;
        }
    }
}
