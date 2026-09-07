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
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Windows.Data;
using DS4Windows;

namespace DS4WinWPF.DS4Forms.ViewModels
{
    public sealed class LogCategoryFilterOption
    {
        public LogCategoryFilterOption(LogCategory? category, string label)
        {
            Category = category;
            Label = label;
        }

        public LogCategory? Category { get; }
        public string Label { get; }

        public override string ToString() => Label;
    }

    public class LogViewModel : INotifyPropertyChanged
    {
        //private object _colLockobj = new object();
        private ReaderWriterLockSlim _logListLocker = new ReaderWriterLockSlim();
        private ObservableCollection<LogItem> logItems = new ObservableCollection<LogItem>();
        private readonly ObservableCollection<LogCategoryFilterOption>
            categoryOptions = new ObservableCollection<LogCategoryFilterOption>();
        private readonly HashSet<LogCategory> categoriesPresent =
            new HashSet<LogCategory>();
        private readonly ICollectionView filteredLogItems;
        private LogCategoryFilterOption selectedCategoryOption;
        private string searchText = string.Empty;
        private bool warningsOnly;
        private int bufferGeneration;

        public ObservableCollection<LogItem> LogItems => logItems;
        public ICollectionView FilteredLogItems => filteredLogItems;
        public ObservableCollection<LogCategoryFilterOption> CategoryOptions =>
            categoryOptions;

        public ReaderWriterLockSlim LogListLocker => _logListLocker;
        public int BufferGeneration => Volatile.Read(ref bufferGeneration);

        public string SearchText
        {
            get => searchText;
            set
            {
                string next = value ?? string.Empty;
                if (searchText == next)
                {
                    return;
                }

                searchText = next;
                OnPropertyChanged(nameof(SearchText));
                filteredLogItems.Refresh();
            }
        }

        public bool WarningsOnly
        {
            get => warningsOnly;
            set
            {
                if (warningsOnly == value)
                {
                    return;
                }

                warningsOnly = value;
                OnPropertyChanged(nameof(WarningsOnly));
                filteredLogItems.Refresh();
            }
        }

        public LogCategoryFilterOption SelectedCategoryOption
        {
            get => selectedCategoryOption;
            set
            {
                if (ReferenceEquals(selectedCategoryOption, value))
                {
                    return;
                }

                selectedCategoryOption = value;
                OnPropertyChanged(nameof(SelectedCategoryOption));
                filteredLogItems.Refresh();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public LogViewModel(DS4Windows.ControlService service)
        {
            string version = DS4Windows.Global.exeDisplayVersion;
            AddInitialMessage($"{DS4Windows.ProductInfo.ProductName} version {version}");
            AddInitialMessage($"{DS4Windows.ProductInfo.ProductName} Assembly Architecture: {(Environment.Is64BitProcess ? "x64" : "x86")}");
            AddInitialMessage($"OS Version: {Environment.OSVersion}");
            AddInitialMessage($"OS Product Name: {DS4Windows.Util.GetOSProductName()}");
            AddInitialMessage($"OS Release ID: {DS4Windows.Util.GetOSReleaseId()}");
            AddInitialMessage($"System Architecture: {(Environment.Is64BitOperatingSystem ? "x64" : "x32")}");

            //logItems.Add(new LogItem { Datetime = DateTime.Now, Message = "DS4Windows version 2.0" });
            //BindingOperations.EnableCollectionSynchronization(logItems, _colLockobj);
            BindingOperations.EnableCollectionSynchronization(logItems, _logListLocker, LogLockCallback);

            filteredLogItems = CollectionViewSource.GetDefaultView(logItems);
            filteredLogItems.Filter = FilterLogItem;

            categoryOptions.Add(new LogCategoryFilterOption(null,
                "All categories"));
            foreach (LogItem item in logItems)
            {
                AddCategoryOption(item.Category);
            }

            selectedCategoryOption = categoryOptions[0];
            service.Debug += AddLogMessage;
            DS4Windows.AppLogger.GuiLog += AddLogMessage;
        }

        /// <summary>
        /// Called on MainWindow's existing per-add dispatcher callback. It
        /// changes the dropdown only when a category first enters the buffer;
        /// the producer thread does no UI work.
        /// </summary>
        public void NoteCategoryPresent(LogCategory category, int generation)
        {
            if (generation == BufferGeneration)
            {
                AddCategoryOption(category);
            }
        }

        public void Clear()
        {
            // Preserve the inherited UI-thread Clear path. Two generation
            // changes make already-queued and concurrently-raised add
            // callbacks stale without changing append marshalling or locks.
            Interlocked.Increment(ref bufferGeneration);
            try
            {
                logItems.Clear();
            }
            finally
            {
                Interlocked.Increment(ref bufferGeneration);
            }

            categoriesPresent.Clear();
            categoryOptions.Clear();
            LogCategoryFilterOption all = new LogCategoryFilterOption(null,
                "All categories");
            categoryOptions.Add(all);
            SelectedCategoryOption = all;

            // An append may overlap the inherited, unlocked UI-thread clear.
            // Rebuild once so an item that survived that pre-existing race is
            // never left without its category option. Later appends use the
            // ordinary dispatcher callback instead of scanning the buffer.
            using (ReadLocker locker = new ReadLocker(_logListLocker))
            {
                foreach (LogItem item in logItems)
                {
                    AddCategoryOption(item.Category);
                }
            }
        }

        private void LogLockCallback(IEnumerable collection, object context, Action accessMethod, bool writeAccess)
        {
            if (writeAccess)
            {
                using (WriteLocker locker = new WriteLocker(_logListLocker))
                {
                    accessMethod?.Invoke();
                }
            }
            else
            {
                using (ReadLocker locker = new ReadLocker(_logListLocker))
                {
                    accessMethod?.Invoke();
                }
            }
        }

        private void AddLogMessage(object sender, DS4Windows.DebugEventArgs e)
        {
            // Classification is paid once before the item enters the buffer.
            // ICollectionView filtering only reads this cached enum.
            LogItem item = CreateLogItem(e.Time, e.Data, e.Warning);
            _logListLocker.EnterWriteLock();
            logItems.Add(item);
            _logListLocker.ExitWriteLock();
            //lock (_colLockobj)
            //{
            //    logItems.Add(item);
            //}
        }

        private void AddInitialMessage(string message)
        {
            logItems.Add(CreateLogItem(DateTime.Now, message, false));
        }

        private static LogItem CreateLogItem(DateTime time, string message,
            bool warning)
        {
            return new LogItem
            {
                Datetime = time,
                Message = message,
                Warning = warning,
                Category = LogClassifier.Classify(message),
            };
        }

        private bool FilterLogItem(object value)
        {
            return LogFilter.Matches(value as LogItem, warningsOnly,
                selectedCategoryOption?.Category, searchText);
        }

        private void AddCategoryOption(LogCategory category)
        {
            if (categoriesPresent.Add(category))
            {
                categoryOptions.Add(new LogCategoryFilterOption(category,
                    LogClassifier.GetDisplayName(category)));
            }
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this,
                new PropertyChangedEventArgs(propertyName));
        }
    }
}
