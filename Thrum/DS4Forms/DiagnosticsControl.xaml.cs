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

using DS4Windows;
using DS4WinWPF.DS4Forms.ViewModels;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace DS4WinWPF.DS4Forms
{
    public partial class DiagnosticsControl : UserControl
    {
        private readonly DiagnosticsPageViewModel viewModel =
            new DiagnosticsPageViewModel();
        private ThrumDiagnosticsCollector collector;
        private int collectionRunning;
        private bool initialCollectionStarted;

        public DiagnosticsControl()
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        public void SetupDataContext(ControlService controlService)
        {
            collector = new ThrumDiagnosticsLiveSources(controlService)
                .CreateCollector();
        }

        private async void DiagnosticsControl_Loaded(object sender,
            RoutedEventArgs e)
        {
            if (initialCollectionStarted || collector == null)
            {
                return;
            }

            initialCollectionStarted = true;
            await CollectAsync();
        }

        private async void RefreshButton_Click(object sender,
            RoutedEventArgs e)
        {
            await CollectAsync();
        }

        private void CopyReportButton_Click(object sender, RoutedEventArgs e)
        {
            ThrumDiagnosticsSnapshot snapshot = viewModel.Snapshot;
            if (snapshot == null)
            {
                return;
            }

            try
            {
                Clipboard.SetText(
                    ThrumDiagnosticsReportFormatter.Format(snapshot));
                viewModel.SetCopyStatus(
                    "The redacted full report was copied to the clipboard.");
            }
            catch (ExternalException ex)
            {
                // Another process can temporarily own the clipboard. Keep the
                // collected snapshot and let the user retry without re-reading
                // any subsystem.
                viewModel.SetCopyStatus("The report could not be copied: " +
                    ex.Message);
            }
        }

        private async Task CollectAsync()
        {
            if (collector == null ||
                Interlocked.CompareExchange(ref collectionRunning, 1, 0) != 0)
            {
                return;
            }

            viewModel.BeginRefresh();
            try
            {
                // Audio property stores, backend loopback TCP and a cold
                // driver-readiness cache can each block for seconds. Every
                // source — including Core Audio object creation, reads and
                // disposal — therefore stays on this one worker invocation.
                // Await resumes on WPF's dispatcher and only the completed,
                // string/bool-only snapshot crosses back to the UI thread.
                ThrumDiagnosticsSnapshot snapshot = await Task.Run(() =>
                    collector.Collect(
                        ThrumDiagnosticsLiveSources.ReadEnvironment()));
                viewModel.Apply(snapshot);
            }
            catch (Exception ex)
            {
                // The collector isolates source failures, but keep the page
                // honest if its surrounding task or environment projection
                // itself ever fails.
                viewModel.ApplyUnexpectedFailure(ex);
            }
            finally
            {
                Interlocked.Exchange(ref collectionRunning, 0);
            }
        }
    }
}
