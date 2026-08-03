using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DS4Windows;
using DS4WinWPF.DS4Forms.ViewModels;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace DS4WinWPF.DS4Forms
{
    public sealed class ProfileFeatureSettingsChangedEventArgs : EventArgs
    {
        public ProfileFeatureSettingsChangedEventArgs(int deviceIndex) => DeviceIndex = deviceIndex;
        public int DeviceIndex { get; }
    }

    public partial class AudioHapticsControl : UserControl
    {
        private sealed class AudioSourceChoice
        {
            public string DisplayName { get; set; }
            public string StoredDisplayName { get; init; } = string.Empty;
            public AudioHapticsSourceKind Kind { get; init; }
            public string EndpointId { get; init; } = string.Empty;
            public int ProcessId { get; init; }
            public string ExecutableName { get; init; } = string.Empty;
            public string ProcessPath { get; init; } = string.Empty;
            public string SessionIdentifier { get; init; } = string.Empty;
            public string SessionInstanceIdentifier { get; init; } = string.Empty;

            /// <summary>
            /// The combo renders these through DisplayMemberPath, which is a
            /// purely visual binding: UI Automation falls back to ToString()
            /// and would otherwise report the type name for every entry, so a
            /// screen reader heard "AudioSourceChoice" twenty times and could
            /// not tell the sources apart (found on hardware, issue #57).
            /// </summary>
            public override string ToString() =>
                string.IsNullOrWhiteSpace(DisplayName)
                    ? base.ToString()
                    : DisplayName;
        }

        private int deviceIndex = -1;
        private bool loading;
        private readonly DispatcherTimer statusRefreshTimer;
        private readonly DispatcherTimer levelRefreshTimer;

        public AudioHapticsControl()
        {
            InitializeComponent();
            statusRefreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1),
            };
            statusRefreshTimer.Tick += (_, _) => UpdateStatus(CurrentSettings);
            levelRefreshTimer = new DispatcherTimer
            {
                // The UI polls one block aggregate at 20 Hz. The capture
                // callback never dispatches or raises a meter event.
                Interval = TimeSpan.FromMilliseconds(50),
            };
            levelRefreshTimer.Tick += (_, _) => UpdateInputLevel();
            Loaded += async (_, _) =>
            {
                RefreshSourcesAndSettings();
                UpdateTimerState();
                await RefreshSourceCacheAsync(force: false);
            };
            Unloaded += (_, _) => StopTimers();
            IsVisibleChanged += (_, _) => UpdateTimerState();
            SetEditorEnabled(false);
        }

        public event EventHandler<ProfileFeatureSettingsChangedEventArgs> SettingsChanged;

        public int DeviceIndex => deviceIndex;

        public void SetDevice(int index)
        {
            deviceIndex = index >= 0 && index < Global.TEST_PROFILE_ITEM_COUNT ? index : -1;
            RefreshSourcesAndSettings();
            UpdateInputLevel();
        }

        private void UpdateTimerState()
        {
            if (IsLoaded && IsVisible)
            {
                statusRefreshTimer.Start();
                levelRefreshTimer.Start();
                return;
            }

            StopTimers();
        }

        private void StopTimers()
        {
            statusRefreshTimer.Stop();
            levelRefreshTimer.Stop();
            inputLevelProgress.Value = 0;
            inputLevelValueText.Text = "0%";
        }

        private void UpdateInputLevel()
        {
            float level = 0.0f;
            if (CurrentSettings?.Enabled == true && deviceIndex >= 0 &&
                deviceIndex < ControlService.CURRENT_DS4_CONTROLLER_LIMIT &&
                Program.rootHub != null)
            {
                level = Program.rootHub.GetAudioHapticsInputLevel(deviceIndex);
            }
            int percent = (int)Math.Round(Math.Clamp(level, 0.0f, 1.0f) *
                100.0f);
            inputLevelProgress.Value = percent;
            inputLevelValueText.Text = $"{percent}%";
        }

        public void RefreshSourcesAndSettings()
        {
            loading = true;
            try
            {
                AudioHapticsProfileSettings settings = CurrentSettings;
                PopulateAudioSources(settings);
                if (settings == null)
                {
                    SetEditorEnabled(false);
                    enabledToggle.IsChecked = false;
                    UpdateStatus(null);
                    return;
                }

                settings.Normalize();
                enabledToggle.IsChecked = settings.Enabled;
                gainSlider.Value = settings.GainPercent;
                gainValueText.Text = $"{settings.GainPercent}%";
                UpdateGainPresetVisuals(settings.GainPercent);
                bassFocusCombo.SelectedIndex = (int)settings.BassFocus;
                responseCombo.SelectedIndex = (int)settings.Response;
                attackCombo.SelectedIndex = (int)settings.Attack;
                releaseCombo.SelectedIndex = (int)settings.Release;
                SelectStoredSource(settings);
                automaticGameDetectionToggle.IsChecked =
                    settings.AutomaticGameDetection;
                streamAppToSpeakerToggle.IsChecked =
                    settings.StreamAppAudioToController;
                streamAppToHeadsetOnlyToggle.IsChecked =
                    settings.StreamAppAudioToHeadsetOnly;
                SetEditorEnabled(true);
                UpdateAppSpeakerOption(settings);
                UpdateModeVisuals(settings.Mode);
                UpdateStatus(settings);
                UpdateSourceValidation(settings);
            }
            finally
            {
                loading = false;
            }
        }

        private AudioHapticsProfileSettings CurrentSettings =>
            deviceIndex >= 0 && deviceIndex < Global.TEST_PROFILE_ITEM_COUNT
                ? Global.store.audioHapticsSettings[deviceIndex]
                : null;

        private void PopulateAudioSources(AudioHapticsProfileSettings settings)
        {
            List<AudioSourceChoice> choices = new List<AudioSourceChoice>
            {
                new AudioSourceChoice
                {
                    DisplayName = "System mix  -  Default render endpoint",
                    Kind = AudioHapticsSourceKind.SystemAudio,
                },
                new AudioSourceChoice
                {
                    DisplayName = "Controller audio  -  Emulated endpoint",
                    Kind = AudioHapticsSourceKind.ControllerAudio,
                },
            };
            choices.AddRange(AudioEndpointChoiceCache.RenderEndpoints.Select(
                endpoint => new AudioSourceChoice
                {
                    DisplayName = $"Endpoint  -  {endpoint.Name}",
                    StoredDisplayName = endpoint.Name,
                    Kind = AudioHapticsSourceKind.Endpoint,
                    EndpointId = endpoint.EndpointId,
                }));
            if (settings?.AutomaticGameDetection == true)
            {
                choices.Add(new AudioSourceChoice
                {
                    DisplayName = "No fallback app selected",
                    Kind = AudioHapticsSourceKind.AppSession,
                });
            }

            try
            {
                using MMDeviceEnumerator enumerator = new MMDeviceEnumerator();
                using MMDevice endpoint = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                AudioSessionManager sessionManager = endpoint.AudioSessionManager;
                try
                {
                    SessionCollection sessions = sessionManager.Sessions;
                    for (int i = 0; i < sessions.Count; i++)
                    {
                        using AudioSessionControl session = sessions[i];
                        if (session.State == AudioSessionState.AudioSessionStateExpired) continue;
                        uint processId = session.GetProcessID;
                        if (processId == 0) continue;
                        string executableName = string.Empty;
                        string processPath = string.Empty;
                        string displayName = session.DisplayName;
                        try
                        {
                            using Process process = Process.GetProcessById((int)processId);
                            executableName = process.ProcessName;
                            processPath = process.MainModule?.FileName ?? string.Empty;
                            if (string.IsNullOrWhiteSpace(displayName)) displayName = process.MainWindowTitle;
                            if (string.IsNullOrWhiteSpace(displayName)) displayName = executableName;
                        }
                        catch
                        {
                            if (string.IsNullOrWhiteSpace(displayName)) displayName = $"Process {processId}";
                        }

                        choices.Add(new AudioSourceChoice
                        {
                            DisplayName = $"{displayName}  ·  App",
                            Kind = AudioHapticsSourceKind.AppSession,
                            StoredDisplayName = displayName,
                            ProcessId = (int)processId,
                            ExecutableName = executableName,
                            ProcessPath = processPath,
                            SessionIdentifier = session.GetSessionIdentifier ?? string.Empty,
                            SessionInstanceIdentifier = session.GetSessionInstanceIdentifier ?? string.Empty,
                        });
                    }
                }
                finally
                {
                    sessionManager.Dispose();
                }
            }
            catch
            {
                // Core Audio can briefly reject session enumeration while an
                // endpoint is being replaced. The refresh button retries it.
            }

            if (settings?.Source == AudioHapticsSourceKind.Endpoint &&
                !choices.Any(choice => SourceMatches(choice, settings)))
            {
                string endpointName = string.IsNullOrWhiteSpace(
                    settings.EndpointName) ? "Unavailable endpoint" :
                    settings.EndpointName;
                choices.Add(new AudioSourceChoice
                {
                    DisplayName = $"Endpoint  -  {endpointName}  -  Unavailable",
                    StoredDisplayName = settings.EndpointName,
                    Kind = AudioHapticsSourceKind.Endpoint,
                    EndpointId = settings.EndpointId,
                });
            }

            if (settings?.Source == AudioHapticsSourceKind.AppSession &&
                !choices.Any(choice => SourceMatches(choice, settings)))
            {
                choices.Add(new AudioSourceChoice
                {
                    DisplayName = $"{(string.IsNullOrWhiteSpace(settings.DisplayName) ? settings.ExecutableName : settings.DisplayName)}  ·  Unavailable",
                    Kind = AudioHapticsSourceKind.AppSession,
                    ProcessId = settings.ProcessId,
                    StoredDisplayName = string.IsNullOrWhiteSpace(
                        settings.DisplayName) ? settings.ExecutableName :
                        settings.DisplayName,
                    ExecutableName = settings.ExecutableName,
                    ProcessPath = settings.ProcessPath,
                    SessionIdentifier = settings.SessionIdentifier,
                    SessionInstanceIdentifier = settings.SessionInstanceIdentifier,
                });
            }

            foreach (AudioSourceChoice appChoice in choices.Where(choice =>
                choice.Kind == AudioHapticsSourceKind.AppSession))
            {
                appChoice.DisplayName = appChoice.ProcessId > 0
                    ? $"App + children  -  {appChoice.StoredDisplayName}"
                    : settings?.AutomaticGameDetection == true
                        ? "App + children  -  Automatic game detection"
                        : "App + children  -  Unavailable app";
            }

            sourceCombo.ItemsSource = choices
                .GroupBy(choice =>
                    $"{choice.Kind}:{choice.EndpointId}:{choice.ProcessId}:{choice.SessionInstanceIdentifier}")
                .Select(group => group.First())
                .ToList();
        }

        private void SelectStoredSource(AudioHapticsProfileSettings settings)
        {
            sourceCombo.SelectedItem = sourceCombo.Items.Cast<AudioSourceChoice>()
                .FirstOrDefault(choice => SourceMatches(choice, settings))
                ?? (settings.AutomaticGameDetection
                    ? sourceCombo.Items.Cast<AudioSourceChoice>()
                        .FirstOrDefault(choice => choice.Kind ==
                            AudioHapticsSourceKind.AppSession)
                    : null)
                ?? sourceCombo.Items.Cast<AudioSourceChoice>().FirstOrDefault();
        }

        private static bool SourceMatches(AudioSourceChoice choice, AudioHapticsProfileSettings settings)
        {
            if (choice.Kind != settings.Source) return false;
            if (choice.Kind == AudioHapticsSourceKind.Endpoint)
            {
                return string.Equals(choice.EndpointId, settings.EndpointId,
                    StringComparison.OrdinalIgnoreCase);
            }
            if (choice.Kind != AudioHapticsSourceKind.AppSession) return true;
            if (settings.AutomaticGameDetection && settings.ProcessId == 0 &&
                string.IsNullOrWhiteSpace(settings.ExecutableName) &&
                choice.ProcessId == 0 &&
                string.IsNullOrWhiteSpace(choice.ExecutableName)) return true;
            if (!string.IsNullOrEmpty(settings.SessionInstanceIdentifier) &&
                choice.SessionInstanceIdentifier == settings.SessionInstanceIdentifier) return true;
            if (!string.IsNullOrEmpty(settings.SessionIdentifier) &&
                choice.SessionIdentifier == settings.SessionIdentifier) return true;
            if (!string.IsNullOrEmpty(settings.ProcessPath) &&
                string.Equals(choice.ProcessPath, settings.ProcessPath, StringComparison.OrdinalIgnoreCase)) return true;
            return settings.ProcessId > 0 && choice.ProcessId == settings.ProcessId;
        }

        private AudioHapticsSourceValidationResult ValidateSource(
            AudioHapticsProfileSettings settings) =>
            AudioHapticsSourceValidator.Validate(settings,
                AudioEndpointChoiceCache.RenderEndpoints.Select(endpoint =>
                    endpoint.EndpointId).ToArray(),
                !string.IsNullOrWhiteSpace(
                    AudioEndpointChoiceCache.DefaultRenderEndpointId),
                AudioEndpointChoiceCache.RenderEndpoints.Any(endpoint =>
                    endpoint.IsControllerAudio), IsAppRunning);

        private static bool IsAppRunning(
            AudioHapticsProfileSettings settings) =>
            ProcessLoopbackWaveCapture.ResolveProcessId(settings) > 0;

        private void UpdateSourceValidation(
            AudioHapticsProfileSettings settings)
        {
            if (settings == null)
            {
                sourceValidationText.Visibility = Visibility.Collapsed;
                sourceValidationText.Text = string.Empty;
                return;
            }

            AudioHapticsSourceValidationResult result =
                ValidateSource(settings);
            sourceValidationText.Text = result.Valid ? string.Empty :
                result.Message;
            sourceValidationText.Visibility = result.Valid
                ? Visibility.Collapsed : Visibility.Visible;
        }

        private async Task RefreshSourceCacheAsync(bool force)
        {
            try
            {
                await AudioEndpointChoiceCache.RefreshAsync(force);
                if (IsLoaded)
                {
                    RefreshSourcesAndSettings();
                }
            }
            catch (Exception exception)
            {
                sourceValidationText.Text =
                    $"Audio sources could not be refreshed: {exception.Message}";
                sourceValidationText.Visibility = Visibility.Visible;
            }
        }

        private void SetEditorEnabled(bool hasDevice)
        {
            enabledToggle.IsEnabled = hasDevice;
            gainSlider.IsEnabled = hasDevice;
            sourceCombo.IsEnabled = hasDevice;
            mixModeButton.IsEnabled = hasDevice;
            replaceModeButton.IsEnabled = hasDevice;
            bassFocusCombo.IsEnabled = hasDevice;
            responseCombo.IsEnabled = hasDevice;
            attackCombo.IsEnabled = hasDevice;
            releaseCombo.IsEnabled = hasDevice;
            streamAppToSpeakerToggle.IsEnabled = hasDevice;
            streamAppToHeadsetOnlyToggle.IsEnabled = hasDevice;
            automaticGameDetectionToggle.IsEnabled = hasDevice;
        }

        private void UpdateAppSpeakerOption(
            AudioHapticsProfileSettings settings)
        {
            bool appSelected = settings?.Source ==
                AudioHapticsSourceKind.AppSession;
            streamAppToSpeakerPanel.Visibility = appSelected
                ? Visibility.Visible
                : Visibility.Collapsed;
            streamAppToSpeakerToggle.IsEnabled =
                CurrentSettings != null && appSelected;
            streamAppToSpeakerToggle.IsChecked = appSelected &&
                settings.StreamAppAudioToController;
            bool canSelectHeadset = CurrentSettings != null && appSelected &&
                settings.StreamAppAudioToController;
            streamAppToHeadsetOnlyPanel.IsEnabled = canSelectHeadset;
            streamAppToHeadsetOnlyToggle.IsChecked = canSelectHeadset &&
                settings.StreamAppAudioToHeadsetOnly;
        }

        private void UpdateModeVisuals(AudioHapticsMode mode)
        {
            mixModeButton.Style = mode == AudioHapticsMode.Mix
                ? FindResource("BridgePrimaryButtonStyle") as Style
                : FindResource("BridgeSecondaryButtonStyle") as Style;
            replaceModeButton.Style = mode == AudioHapticsMode.Replace
                ? FindResource("BridgePrimaryButtonStyle") as Style
                : FindResource("BridgeSecondaryButtonStyle") as Style;
            modeHelpText.Text = mode == AudioHapticsMode.Mix
                ? "Mix adds audio-driven detail while preserving game-provided advanced haptics."
                : "Replace ignores game-provided advanced haptics and uses only the selected audio source.";
        }

        private void UpdateGainPresetVisuals(int gainPercent)
        {
            if (lowGainButton == null || mediumGainButton == null ||
                highGainButton == null)
            {
                return;
            }
            Style primary = FindResource("ActivePresetButton") as Style;
            Style secondary = FindResource("PresetButton") as Style;
            lowGainButton.Style = gainPercent == 50 ? primary : secondary;
            mediumGainButton.Style = gainPercent == 100 ? primary : secondary;
            highGainButton.Style = gainPercent == 150 ? primary : secondary;
        }

        private void UpdateStatus(AudioHapticsProfileSettings settings)
        {
            if (settings == null)
            {
                statusText.Text = "Select a controller";
                sourceStatusText.Text = "No source";
                statusDot.Fill = FindBrush("MutedForegroundColor", Brushes.Gray);
                return;
            }

            sourceStatusText.Text = SourceDisplayName(settings);
            if (!settings.Enabled)
            {
                statusText.Text = "Off";
                statusDot.Fill = FindBrush("MutedForegroundColor", Brushes.Gray);
                return;
            }

            bool liveController = deviceIndex >= 0 &&
                deviceIndex < ControlService.CURRENT_DS4_CONTROLLER_LIMIT &&
                Program.rootHub?.DS4Controllers[deviceIndex] != null;
            if (!liveController)
            {
                statusText.Text = "Saved to profile";
                statusDot.Fill = FindBrush("AccentColor", Brushes.DodgerBlue);
                return;
            }

            AudioHapticsRuntimeStatus runtime =
                Program.rootHub.GetAudioHapticsStatus(deviceIndex);

            // Capture, the level meter and this status are all independent of
            // whether anything reaches the controller. Audio-derived haptics
            // are patched into the feedback report coming back from the virtual
            // output device (ViiperOutDevice does it on the way to the pad), so
            // with no virtual output there is no report to patch and the
            // feature produces silence while looking entirely healthy. Report
            // that instead of "Active" - a meter moving next to the word Active
            // is a stronger claim than the app can make.
            if (runtime.Active && !AudioHapticsOutputPathAvailable())
            {
                statusText.Text = "Capturing, but not reaching the controller";
                sourceStatusText.Text = SourceDisplayName(settings) +
                    " - needs virtual controller output";
                statusDot.Fill = FindBrush("WarningColor", Brushes.Goldenrod);
                return;
            }

            statusText.Text = runtime.Active &&
                !settings.AutomaticGameDetection ? "Active" : runtime.Message;
            statusDot.Fill = runtime.Active
                ? FindBrush("SuccessColor", Brushes.LimeGreen)
                : FindBrush("AccentColor", Brushes.DodgerBlue);
        }

        /// <summary>
        /// Whether a virtual output device exists for this slot, which is the
        /// only route audio-derived haptics currently have to the controller:
        /// <c>ApplyAudioHapticsToGameReport</c> is called solely from
        /// <c>ViiperOutDevice</c>'s feedback path.
        ///
        /// <para>Reads the array directly rather than through
        /// <c>OutputSlotManager.GetOutSlotDevice</c>, whose unsynchronised
        /// dictionary lookup can spin against a concurrent plug or removal.</para>
        /// </summary>
        private bool AudioHapticsOutputPathAvailable()
        {
            OutputDevice[] outputs = Program.rootHub?.outputDevices;
            return outputs != null &&
                deviceIndex >= 0 && deviceIndex < outputs.Length &&
                outputs[deviceIndex] != null;
        }

        private static string SourceDisplayName(AudioHapticsProfileSettings settings) => settings.Source switch
        {
            AudioHapticsSourceKind.ControllerAudio => "Controller audio",
            AudioHapticsSourceKind.Endpoint =>
                string.IsNullOrWhiteSpace(settings.EndpointName)
                    ? "Selected endpoint" : settings.EndpointName,
            AudioHapticsSourceKind.AppSession when
                settings.AutomaticGameDetection => "Automatic game detection",
            AudioHapticsSourceKind.AppSession => string.IsNullOrWhiteSpace(settings.DisplayName)
                ? (string.IsNullOrWhiteSpace(settings.ExecutableName) ? "Selected app" : settings.ExecutableName)
                : settings.DisplayName,
            _ => "System audio",
        };

        private Brush FindBrush(string key, Brush fallback) => TryFindResource(key) as Brush ?? fallback;

        private void Commit(Action<AudioHapticsProfileSettings> update)
        {
            if (loading || CurrentSettings == null) return;
            update(CurrentSettings);
            CurrentSettings.Normalize();
            UpdateModeVisuals(CurrentSettings.Mode);
            UpdateAppSpeakerOption(CurrentSettings);
            UpdateStatus(CurrentSettings);
            UpdateSourceValidation(CurrentSettings);
            SettingsChanged?.Invoke(this, new ProfileFeatureSettingsChangedEventArgs(deviceIndex));
        }

        private void EnabledToggle_Click(object sender, RoutedEventArgs e) => Commit(settings => settings.Enabled = enabledToggle.IsChecked == true);
        private void GainSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int value = (int)Math.Round(e.NewValue);
            gainValueText.Text = $"{value}%";
            UpdateGainPresetVisuals(value);
            Commit(settings => settings.GainPercent = value);
        }
        private void GainPreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && int.TryParse(button.Tag?.ToString(), out int value)) gainSlider.Value = value;
        }
        private async void RefreshSources_Click(object sender,
            RoutedEventArgs e) => await RefreshSourceCacheAsync(force: true);
        private void SourceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sourceCombo.SelectedItem is not AudioSourceChoice choice) return;
            if (loading || CurrentSettings == null) return;
            AudioHapticsProfileSettings proposed = CurrentSettings.Clone();
            ApplySourceChoice(proposed, choice);
            AudioHapticsSourceValidationResult validation =
                ValidateSource(proposed);
            if (!validation.Valid)
            {
                loading = true;
                try
                {
                    SelectStoredSource(CurrentSettings);
                }
                finally
                {
                    loading = false;
                }
                sourceValidationText.Text = validation.Message;
                sourceValidationText.Visibility = Visibility.Visible;
                return;
            }
            Commit(settings => ApplySourceChoice(settings, choice));
        }

        private static void ApplySourceChoice(
            AudioHapticsProfileSettings settings, AudioSourceChoice choice)
        {
            settings.Source = choice.Kind;
            if (choice.Kind != AudioHapticsSourceKind.AppSession)
            {
                settings.AutomaticGameDetection = false;
            }
            settings.EndpointId = choice.Kind ==
                AudioHapticsSourceKind.Endpoint ? choice.EndpointId :
                string.Empty;
            settings.EndpointName = choice.Kind ==
                AudioHapticsSourceKind.Endpoint ? choice.StoredDisplayName :
                string.Empty;
            settings.ProcessId = choice.Kind ==
                AudioHapticsSourceKind.AppSession ? choice.ProcessId : 0;
            settings.DisplayName = choice.Kind ==
                AudioHapticsSourceKind.AppSession
                    ? choice.StoredDisplayName : string.Empty;
            settings.ExecutableName = choice.Kind ==
                AudioHapticsSourceKind.AppSession ? choice.ExecutableName :
                string.Empty;
            settings.ProcessPath = choice.Kind ==
                AudioHapticsSourceKind.AppSession ? choice.ProcessPath :
                string.Empty;
            settings.SessionIdentifier = choice.Kind ==
                AudioHapticsSourceKind.AppSession ? choice.SessionIdentifier :
                string.Empty;
            settings.SessionInstanceIdentifier = choice.Kind ==
                AudioHapticsSourceKind.AppSession
                    ? choice.SessionInstanceIdentifier : string.Empty;
        }
        private void AutomaticGameDetectionToggle_Click(object sender,
            RoutedEventArgs e)
        {
            Commit(settings =>
            {
                settings.AutomaticGameDetection =
                    automaticGameDetectionToggle.IsChecked == true;
                if (settings.AutomaticGameDetection)
                {
                    settings.Source = AudioHapticsSourceKind.AppSession;
                }
            });
            RefreshSourcesAndSettings();
        }
        private void StreamAppToSpeakerToggle_Click(object sender,
            RoutedEventArgs e) => Commit(settings =>
            {
                settings.StreamAppAudioToController =
                    streamAppToSpeakerToggle.IsChecked == true &&
                    settings.Source == AudioHapticsSourceKind.AppSession;
                if (!settings.StreamAppAudioToController)
                {
                    settings.StreamAppAudioToHeadsetOnly = false;
                }
                if (settings.StreamAppAudioToController && deviceIndex >= 0 &&
                    deviceIndex < Global.TEST_PROFILE_ITEM_COUNT)
                {
                    Global.DualSenseEnableSpeakerOutput[deviceIndex] = true;
                }
            });
        private void StreamAppToHeadsetOnlyToggle_Click(object sender,
            RoutedEventArgs e) => Commit(settings =>
            {
                settings.StreamAppAudioToHeadsetOnly =
                    settings.StreamAppAudioToController &&
                    streamAppToHeadsetOnlyToggle.IsChecked == true;
            });
        private void MixMode_Click(object sender, RoutedEventArgs e) => Commit(settings => settings.Mode = AudioHapticsMode.Mix);
        private void ReplaceMode_Click(object sender, RoutedEventArgs e) => Commit(settings => settings.Mode = AudioHapticsMode.Replace);
        private void BassFocusCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => Commit(settings => settings.BassFocus = (AudioHapticsBassFocus)Math.Max(0, bassFocusCombo.SelectedIndex));
        private void ResponseCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => Commit(settings => settings.Response = (AudioHapticsResponse)Math.Max(0, responseCombo.SelectedIndex));
        private void AttackCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => Commit(settings => settings.Attack = (AudioHapticsAttack)Math.Max(0, attackCombo.SelectedIndex));
        private void ReleaseCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => Commit(settings => settings.Release = (AudioHapticsRelease)Math.Max(0, releaseCombo.SelectedIndex));
    }
}
