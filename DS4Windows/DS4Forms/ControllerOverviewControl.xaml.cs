/*
DS4Windows
Copyright (C) 2023  Travis Nickles

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.
*/

using System;
using System.Windows;
using System.Windows.Controls;

namespace DS4WinWPF.DS4Forms
{
    public sealed class OverviewProfileSelectionChangedEventArgs : EventArgs
    {
        public OverviewProfileSelectionChangedEventArgs(int selectedIndex)
        {
            SelectedIndex = selectedIndex;
        }

        public int SelectedIndex { get; }
    }

    public partial class ControllerOverviewControl : UserControl
    {
        public ControllerOverviewControl()
        {
            InitializeComponent();
        }

        public event EventHandler EditProfileRequested;
        public event EventHandler TestInputsRequested;
        public event EventHandler<OverviewProfileSelectionChangedEventArgs>
            ActiveProfileChangedRequested;
        public event EventHandler ControllerDetailsRequested;
        public event EventHandler IdentifyRequested;
        public event EventHandler LightbarRequested;
        public event EventHandler DisconnectRequested;

        private void EditProfileBtn_Click(object sender, RoutedEventArgs e) =>
            EditProfileRequested?.Invoke(this, EventArgs.Empty);

        private void TestInputsBtn_Click(object sender, RoutedEventArgs e) =>
            TestInputsRequested?.Invoke(this, EventArgs.Empty);

        private void ActiveProfileComboBox_SelectionChanged(object sender,
            SelectionChangedEventArgs e)
        {
            // Runtime profile synchronization also updates this binding. Only
            // a focused user interaction should request another profile load.
            if (!ActiveProfileComboBox.IsKeyboardFocusWithin)
            {
                return;
            }

            ActiveProfileChangedRequested?.Invoke(this,
                new OverviewProfileSelectionChangedEventArgs(
                    ActiveProfileComboBox.SelectedIndex));
        }

        private void ControllerDetailsBtn_Click(object sender, RoutedEventArgs e) =>
            ControllerDetailsRequested?.Invoke(this, EventArgs.Empty);

        private void IdentifyControllerBtn_Click(object sender,
            RoutedEventArgs e) =>
            IdentifyRequested?.Invoke(this, EventArgs.Empty);

        private void LightbarBtn_Click(object sender, RoutedEventArgs e) =>
            LightbarRequested?.Invoke(this, EventArgs.Empty);

        private void DisconnectBtn_Click(object sender, RoutedEventArgs e) =>
            DisconnectRequested?.Invoke(this, EventArgs.Empty);
    }
}
