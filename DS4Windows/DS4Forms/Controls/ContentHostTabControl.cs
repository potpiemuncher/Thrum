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
            // The base implementation returns TabItem peers. When the template
            // hosts items normally those exist and are the right answer, so
            // prefer them and only substitute the content when there are none.
            List<AutomationPeer> fromItems = base.GetChildrenCore();
            if (fromItems != null && fromItems.Count > 0)
            {
                return fromItems;
            }

            List<AutomationPeer> children = new List<AutomationPeer>();
            CollectPeers(Owner, children);
            return children;
        }

        /// <summary>
        /// Walks the visual tree and collects the topmost automation peer on
        /// each branch. Descending past an element that has its own peer would
        /// duplicate its subtree, since that peer already exposes its children.
        /// </summary>
        private static void CollectPeers(DependencyObject parent,
            List<AutomationPeer> into)
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int index = 0; index < count; index++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent,
                    index);

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

                CollectPeers(child, into);
            }
        }
    }
}
