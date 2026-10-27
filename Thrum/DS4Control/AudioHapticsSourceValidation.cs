/*
DS4Windows
Copyright (C) 2026  DS4Windows contributors

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.
*/

using System;
using System.Collections.Generic;
using System.Linq;

namespace DS4Windows
{
    public readonly struct AudioHapticsSourceValidationResult
    {
        public AudioHapticsSourceValidationResult(bool valid, string message)
        {
            Valid = valid;
            Message = message ?? string.Empty;
        }

        public bool Valid { get; }
        public string Message { get; }
    }

    /// <summary>
    /// Validates the same identities consumed by AudioHapticsService without
    /// opening an audio stream. Endpoint lists are supplied by the UI's cached
    /// background enumeration so FriendlyName reads never occur here.
    /// </summary>
    public static class AudioHapticsSourceValidator
    {
        public static AudioHapticsSourceValidationResult Validate(
            AudioHapticsProfileSettings settings,
            IReadOnlyCollection<string> activeRenderEndpointIds,
            bool defaultRenderEndpointAvailable,
            bool controllerAudioEndpointAvailable,
            Func<AudioHapticsProfileSettings, bool> appIsRunning,
            bool? processLoopbackSupported = null)
        {
            if (settings == null)
            {
                return Invalid("No Audio Haptics source is selected.");
            }

            activeRenderEndpointIds ??= Array.Empty<string>();
            switch (settings.Source)
            {
                case AudioHapticsSourceKind.SystemAudio:
                    return defaultRenderEndpointAvailable
                        ? Valid("The default system-mix endpoint is available.")
                        : Invalid("Windows has no active default render endpoint.");
                case AudioHapticsSourceKind.ControllerAudio:
                    return controllerAudioEndpointAvailable
                        ? Valid("The emulated controller audio endpoint is available.")
                        : Invalid("The emulated controller audio endpoint is not available.");
                case AudioHapticsSourceKind.Endpoint:
                    if (string.IsNullOrWhiteSpace(settings.EndpointId))
                    {
                        return Invalid("Choose an active render endpoint.");
                    }
                    return activeRenderEndpointIds.Any(id => string.Equals(id,
                            settings.EndpointId,
                            StringComparison.OrdinalIgnoreCase))
                        ? Valid("The selected render endpoint is available.")
                        : Invalid("The selected render endpoint is no longer available. Refresh sources and choose another endpoint.");
                case AudioHapticsSourceKind.AppSession:
                    // Per-app capture needs Windows' process loopback, which
                    // first shipped in build 20348 (Windows 11). Older Windows
                    // 10 accepted this source and then failed silently at
                    // every attach attempt.
                    if (!(processLoopbackSupported ??
                        OperatingSystem.IsWindowsVersionAtLeast(10, 0, 20348)))
                    {
                        return Invalid(
                            "Following a single app or detected game needs Windows 11. " +
                            "On this version of Windows, choose the system mix or a playback device instead.");
                    }
                    if (settings.AutomaticGameDetection)
                    {
                        return Valid(
                            "Automatic game detection will wait for a supported game.");
                    }
                    if (appIsRunning == null || !appIsRunning(settings))
                    {
                        // An app known by path or name is picked up again as
                        // soon as it starts, so "not running yet" is the
                        // normal state before playing, not an error.
                        if (!string.IsNullOrWhiteSpace(settings.ProcessPath) ||
                            !string.IsNullOrWhiteSpace(settings.ExecutableName))
                        {
                            return Valid(
                                "Waiting for the selected app to start.");
                        }

                        return Invalid(
                            "The selected app is not running. Start it, refresh sources, and select it again.");
                    }
                    return Valid(
                        "The selected app and its child processes are available.");
                default:
                    return Invalid("The selected Audio Haptics source is not supported.");
            }
        }

        private static AudioHapticsSourceValidationResult Valid(
            string message) => new(true, message);

        private static AudioHapticsSourceValidationResult Invalid(
            string message) => new(false, message);
    }
}
