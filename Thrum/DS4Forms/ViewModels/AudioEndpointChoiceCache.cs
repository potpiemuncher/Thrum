using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DS4Windows;
using NAudio.CoreAudioApi;

namespace DS4WinWPF.DS4Forms.ViewModels
{
    internal sealed class AudioEndpointSnapshot
    {
        public string Name { get; }
        public string EndpointId { get; }
        public bool IsControllerAudio { get; }

        public AudioEndpointSnapshot(string name, string endpointId,
            bool isControllerAudio)
        {
            Name = name ?? string.Empty;
            EndpointId = endpointId ?? string.Empty;
            IsControllerAudio = isControllerAudio;
        }
    }

    /// <summary>
    /// Keeps slow Windows Core Audio and property-store access off WPF's
    /// dispatcher. A profile editor used to enumerate render endpoints three
    /// times and capture endpoints once while its bindings were being attached.
    /// Some audio drivers take hundreds of milliseconds to answer an individual
    /// property query, which made the whole window appear hung.
    /// </summary>
    internal static class AudioEndpointChoiceCache
    {
        private static readonly object syncRoot = new object();
        private static readonly TimeSpan cacheLifetime = TimeSpan.FromSeconds(10);
        private static IReadOnlyList<AudioEndpointSnapshot> renderEndpoints =
            Array.Empty<AudioEndpointSnapshot>();
        private static IReadOnlyList<AudioEndpointSnapshot> captureEndpoints =
            Array.Empty<AudioEndpointSnapshot>();
        private static string defaultRenderEndpointId = string.Empty;
        private static DateTime refreshedAtUtc = DateTime.MinValue;
        private static Task refreshTask;

        public static IReadOnlyList<AudioEndpointSnapshot> RenderEndpoints
        {
            get
            {
                lock (syncRoot)
                {
                    return renderEndpoints;
                }
            }
        }

        public static IReadOnlyList<AudioEndpointSnapshot> CaptureEndpoints
        {
            get
            {
                lock (syncRoot)
                {
                    return captureEndpoints;
                }
            }
        }

        public static string DefaultRenderEndpointId
        {
            get
            {
                lock (syncRoot)
                {
                    return defaultRenderEndpointId;
                }
            }
        }

        public static Task RefreshAsync(bool force = false)
        {
            lock (syncRoot)
            {
                if (refreshTask != null && !refreshTask.IsCompleted)
                {
                    return refreshTask;
                }

                if (!force && DateTime.UtcNow - refreshedAtUtc < cacheLifetime)
                {
                    return Task.CompletedTask;
                }

                refreshTask = Task.Run(RefreshCore);
                return refreshTask;
            }
        }

        private static void RefreshCore()
        {
            var newRenderEndpoints = new List<AudioEndpointSnapshot>();
            var newCaptureEndpoints = new List<AudioEndpointSnapshot>();
            string newDefaultRenderEndpointId = string.Empty;

            try
            {
                using var enumerator = new MMDeviceEnumerator();
                CopyEndpoints(enumerator, DataFlow.Render, newRenderEndpoints);
                CopyEndpoints(enumerator, DataFlow.Capture, newCaptureEndpoints);
                try
                {
                    using MMDevice defaultEndpoint =
                        enumerator.GetDefaultAudioEndpoint(DataFlow.Render,
                            Role.Multimedia);
                    newDefaultRenderEndpointId = defaultEndpoint.ID ??
                        string.Empty;
                }
                catch
                {
                    // A device-graph transition can temporarily leave Windows
                    // without a default while the active list is still useful.
                }
            }
            catch
            {
                // Keep the last known snapshot. Audio endpoint availability can
                // change while Windows is rebuilding its device graph.
                return;
            }

            lock (syncRoot)
            {
                renderEndpoints = newRenderEndpoints;
                captureEndpoints = newCaptureEndpoints;
                defaultRenderEndpointId = newDefaultRenderEndpointId;
                refreshedAtUtc = DateTime.UtcNow;
            }
        }

        private static void CopyEndpoints(MMDeviceEnumerator enumerator,
            DataFlow flow, List<AudioEndpointSnapshot> destination)
        {
            MMDeviceCollection endpoints = enumerator.EnumerateAudioEndPoints(
                flow, DeviceState.Active);
            foreach (MMDevice endpoint in endpoints)
            {
                try
                {
                    string name = endpoint.FriendlyName ?? string.Empty;
                    string id = endpoint.ID ?? string.Empty;
                    bool controllerAudio = flow == DataFlow.Render &&
                        DualSenseAudioPassthrough.IsControllerAudioEndpoint(endpoint);
                    destination.Add(new AudioEndpointSnapshot(name, id,
                        controllerAudio));
                }
                catch
                {
                    // A single disappearing endpoint must not discard the
                    // rest of the device snapshot.
                }
                finally
                {
                    endpoint?.Dispose();
                }
            }
        }
    }
}
