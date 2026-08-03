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

using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;

namespace DS4WinWPF.DS4Forms.Controls
{
    /// <summary>
    /// A <see cref="TabControl"/> for templates that render only the selected
    /// content and draw their own navigation elsewhere.
    ///
    /// <para><b>Why this type exists.</b> WPF builds a TabControl's automation
    /// tree out of its <see cref="TabItem"/> peers, and a TabItem only has a
    /// peer once it has a visual. <c>BridgeProfileTabControlStyle</c>'s template
    /// is a single <c>ContentPresenter</c> bound to <c>SelectedContent</c> with
    /// no items host, because the profile editor draws its own workspace rail.
    /// The selected page therefore renders perfectly and is <i>invisible to UI
    /// Automation</i>: the tree stops at the TabControl with no children.</para>
    ///
    /// <para>Measured on hardware (issue #49): with the editor open the whole
    /// window exposed 42 automation elements and the rail plus every control of
    /// the selected section were absent while plainly on screen. Screen readers
    /// could not operate the editor and no UIA-driven test could reach it.</para>
    ///
    /// <para>This peer returns the peers of whatever the content presenter is
    /// actually showing, so the automation tree matches what a sighted user
    /// sees. It changes no visuals.</para>
    /// </summary>
    public class ContentHostTabControl : TabControl
    {
        protected override AutomationPeer OnCreateAutomationPeer() =>
            new ContentHostTabControlAutomationPeer(this);
    }

    internal sealed class ContentHostTabControlAutomationPeer :
        TabControlAutomationPeer
    {
        internal ContentHostTabControlAutomationPeer(TabControl owner)
            : base(owner)
        {
        }

        protected override List<AutomationPeer> GetChildrenCore()
        {
            List<AutomationPeer> children = base.GetChildrenCore()
                ?? new List<AutomationPeer>();

            // TabControlAutomationPeer returns TabItem peers and nothing else,
            // so any functional control the ControlTemplate hosts alongside the
            // items - the shell template puts the whole profile-editor
            // navigation rail there - is unreachable no matter how ordinary
            // that control is. Walk the template for the rest.
            //
            // The items host and the selected-content host are skipped: their
            // contents are already reported, by the TabItem peers above and by
            // the selected TabItem respectively, and walking them again would
            // duplicate whole subtrees.
            CollectTemplatePeers(Owner, children);

            return children;
        }

        private static void CollectTemplatePeers(DependencyObject parent,
            List<AutomationPeer> into)
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int index = 0; index < count; index++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent,
                    index);

                if (child is Panel panel && panel.IsItemsHost)
                {
                    continue;
                }

                if (child is ContentPresenter presenter &&
                    presenter.Name == SelectedContentHostName)
                {
                    continue;
                }

                // The shell keeps both sidebars permanently loaded and swaps
                // them by animating Opacity, so a template walk reaches the
                // hidden one too. Reporting it would have a screen reader
                // announce the profile-editor rail while the user is on
                // Overview - worse than the omission being fixed here.
                if (child is UIElement hidden &&
                    (!hidden.IsVisible || hidden.Opacity <= 0.0))
                {
                    continue;
                }

                if (child is UIElement element)
                {
                    AutomationPeer peer =
                        UIElementAutomationPeer.CreatePeerForElement(element);
                    if (peer != null)
                    {
                        into.Add(peer);
                        continue;
                    }
                }

                CollectTemplatePeers(child, into);
            }
        }

        private const string SelectedContentHostName =
            "PART_SelectedContentHost";

    }
}
