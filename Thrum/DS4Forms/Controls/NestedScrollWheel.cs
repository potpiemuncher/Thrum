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
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DS4WinWPF.DS4Forms.Controls
{
    /// <summary>
    /// Hands the mouse wheel to the enclosing scroll area when an inner
    /// <see cref="ScrollViewer"/> has nothing left to scroll that way.
    ///
    /// <para>WPF's ScrollViewer marks every wheel event handled, even one it
    /// cannot use, so a notice box inside a scrolling page stopped the page
    /// scrolling whenever the pointer was over the box - and when the notice
    /// fit without scrolling, the page never scrolled from there at all.</para>
    /// </summary>
    public static class NestedScrollWheel
    {
        public static readonly DependencyProperty PassAtEdgesProperty =
            DependencyProperty.RegisterAttached("PassAtEdges", typeof(bool),
                typeof(NestedScrollWheel), new PropertyMetadata(false, OnPassAtEdgesChanged));

        public static bool GetPassAtEdges(DependencyObject element) =>
            (bool)element.GetValue(PassAtEdgesProperty);

        public static void SetPassAtEdges(DependencyObject element, bool value) =>
            element.SetValue(PassAtEdgesProperty, value);

        private static void OnPassAtEdgesChanged(DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            if (d is not ScrollViewer viewer)
            {
                return;
            }

            viewer.PreviewMouseWheel -= Viewer_PreviewMouseWheel;
            if ((bool)e.NewValue)
            {
                viewer.PreviewMouseWheel += Viewer_PreviewMouseWheel;
            }
        }

        private static void Viewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            ScrollViewer viewer = (ScrollViewer)sender;
            if (e.Handled ||
                !NestedScrollWheelPolicy.ShouldPassToParent(e.Delta,
                    viewer.VerticalOffset, viewer.ScrollableHeight) ||
                VisualTreeHelper.GetParent(viewer) is not UIElement parent)
            {
                return;
            }

            e.Handled = true;
            parent.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = viewer,
            });
        }
    }

    internal static class NestedScrollWheelPolicy
    {
        // Offsets are layout doubles; half a pixel counts as the edge.
        private const double EdgeTolerance = 0.5;

        /// <summary>
        /// True when an inner scroll area cannot move in the wheel's direction
        /// (positive delta scrolls up), so the page should scroll instead.
        /// </summary>
        internal static bool ShouldPassToParent(int delta, double verticalOffset,
            double scrollableHeight)
        {
            if (scrollableHeight <= EdgeTolerance)
            {
                return true;
            }

            return delta > 0
                ? verticalOffset <= EdgeTolerance
                : verticalOffset >= scrollableHeight - EdgeTolerance;
        }
    }
}
