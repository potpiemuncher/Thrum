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

using DS4WinWPF.DS4Forms.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DS4WinWPF.DS4Forms
{
    /// <summary>A consent switch moved; <see cref="Requested"/> is the new value.</summary>
    public sealed class NativePs5ConsentEventArgs : EventArgs
    {
        public NativePs5ConsentEventArgs(bool requested)
        {
            Requested = requested;
        }

        public bool Requested { get; }
    }

    /// <summary>
    /// The Native PS5 mode setup sheet. Owns nothing but its step position:
    /// every action is raised as an event for <c>MainWindow</c>, where the
    /// existing install, consent and output-type handlers live.
    /// </summary>
    public partial class NativePs5SetupSheet : UserControl
    {
        public NativePs5SetupSheet()
        {
            InitializeComponent();
            DataContextChanged += Sheet_DataContextChanged;
        }

        public event EventHandler CloseRequested;
        public event EventHandler InstallRequested;
        public event EventHandler RecheckRequested;
        public event EventHandler<NativePs5ConsentEventArgs> AcknowledgementChanged;
        public event EventHandler<NativePs5ConsentEventArgs> AudioConsentChanged;
        public event EventHandler TurnOnRequested;

        private NativePs5SetupViewModel ViewModel =>
            DataContext as NativePs5SetupViewModel;

        private void Sheet_DataContextChanged(object sender,
            DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is NativePs5SetupViewModel old)
            {
                old.PropertyChanged -= ViewModel_PropertyChanged;
            }

            if (e.NewValue is NativePs5SetupViewModel current)
            {
                current.PropertyChanged += ViewModel_PropertyChanged;
                SyncConsentBoxes(current);
            }
        }

        /// <summary>
        /// The consent boxes bind OneWay, and a user click leaves a local
        /// value on the box. Re-syncing on every publish keeps a box that was
        /// declined, or withdrawn from Settings, from showing stale state.
        /// </summary>
        private void ViewModel_PropertyChanged(object sender,
            System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is NativePs5SetupViewModel vm)
            {
                SyncConsentBoxes(vm);
            }
        }

        private void SyncConsentBoxes(NativePs5SetupViewModel vm)
        {
            if (AcknowledgeCheckBox.IsChecked != vm.Acknowledged)
            {
                AcknowledgeCheckBox.IsChecked = vm.Acknowledged;
            }

            if (AudioConsentCheckBox.IsChecked != vm.AudioEndpointsAllowed)
            {
                AudioConsentCheckBox.IsChecked = vm.AudioEndpointsAllowed;
            }
        }

        /// <summary>Moves keyboard focus into the sheet, onto the first useful control.</summary>
        public void FocusFirstControl()
        {
            NativePs5SetupViewModel vm = ViewModel;
            UIElement target = vm == null ? CloseButton : vm.CurrentStep switch
            {
                NativePs5SetupViewModel.InstallStep => InstallButton,
                NativePs5SetupViewModel.ConsentStep => AcknowledgementScroller,
                _ => PrimaryButton.IsEnabled ? PrimaryButton : CloseButton,
            };

            Dispatcher.BeginInvoke(new Action(() => Keyboard.Focus(target)),
                System.Windows.Threading.DispatcherPriority.Input);
        }

        private void Sheet_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) =>
            CloseRequested?.Invoke(this, EventArgs.Empty);

        private void InstallButton_Click(object sender, RoutedEventArgs e) =>
            InstallRequested?.Invoke(this, EventArgs.Empty);

        private void RecheckButton_Click(object sender, RoutedEventArgs e) =>
            RecheckRequested?.Invoke(this, EventArgs.Empty);

        private void AcknowledgeCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            NativePs5SetupViewModel vm = ViewModel;
            bool requested = AcknowledgeCheckBox.IsChecked == true;
            // Echo of the stored value (binding at load, the re-sync above):
            // nothing moved, nothing to record.
            if (vm == null || requested == vm.Acknowledged)
            {
                return;
            }

            AcknowledgementChanged?.Invoke(this,
                new NativePs5ConsentEventArgs(requested));
        }

        private void AudioConsentCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            NativePs5SetupViewModel vm = ViewModel;
            bool requested = AudioConsentCheckBox.IsChecked == true;
            if (vm == null || requested == vm.AudioEndpointsAllowed)
            {
                return;
            }

            AudioConsentChanged?.Invoke(this,
                new NativePs5ConsentEventArgs(requested));
        }

        private void BackButton_Click(object sender, RoutedEventArgs e) =>
            ViewModel?.Back();

        private void Step4LinkButton_Click(object sender, RoutedEventArgs e) =>
            ViewModel?.GoToStep(NativePs5SetupViewModel.HapticsStep);

        private void PrimaryButton_Click(object sender, RoutedEventArgs e)
        {
            NativePs5SetupViewModel vm = ViewModel;
            if (vm == null)
            {
                return;
            }

            switch (vm.PrimaryAction)
            {
                case NativePs5SetupAction.Continue:
                    vm.Advance();
                    break;
                case NativePs5SetupAction.TurnOn:
                    TurnOnRequested?.Invoke(this, EventArgs.Empty);
                    break;
                default:
                    CloseRequested?.Invoke(this, EventArgs.Empty);
                    break;
            }
        }
    }
}
