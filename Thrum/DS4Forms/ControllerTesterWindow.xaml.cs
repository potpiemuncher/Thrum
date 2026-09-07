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
using DS4WinWPF.DS4Forms.ViewModels;

namespace DS4WinWPF.DS4Forms
{
    public partial class ControllerTesterWindow : Window
    {
        internal ControllerTesterWindow(CompositeDeviceModel controller)
        {
            InitializeComponent();
            TesterControl.UseController(controller ??
                throw new ArgumentNullException(nameof(controller)));
        }

        internal int DeviceIndex => TesterControl.DeviceIndex;

        internal bool UsesController(CompositeDeviceModel controller) =>
            TesterControl.UsesController(controller);

        private void ControllerTesterWindow_StateChanged(object sender,
            EventArgs e) =>
            TesterControl.SetHostVisible(WindowState !=
                WindowState.Minimized);
    }
}
