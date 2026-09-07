using System;

namespace DS4Windows
{
    internal interface IFirstRunStartupSnapshotSource
    {
        void FindConfigLocation();
        bool FirstRun { get; }
        string AppDataPath { get; }
        bool IsTargetPristine(string path);
    }

    internal sealed class GlobalFirstRunStartupSnapshotSource :
        IFirstRunStartupSnapshotSource
    {
        public bool FirstRun => Global.firstRun;
        public string AppDataPath => Global.appDataPpath;

        public void FindConfigLocation() => Global.FindConfigLocation();

        public bool IsTargetPristine(string path) =>
            new ImportPlanner().IsTargetPristine(path);
    }

    internal sealed class FirstRunStartupSnapshot
    {
        public FirstRunStartupSnapshot(bool firstRun,
            bool appDataConfigPristine)
        {
            FirstRun = firstRun;
            AppDataConfigPristine = appDataConfigPristine;
        }

        public bool FirstRun { get; }
        public bool AppDataConfigPristine { get; }
    }

    /// <summary>
    /// Pins the load-bearing first-run sampling order in one testable seam.
    /// The continuation is the first code allowed to show a dialog or write a
    /// data-location choice, so the pristine observation necessarily predates
    /// SaveDefault's appdata Profiles.xml stub.
    /// </summary>
    internal static class FirstRunStartupCoordinator
    {
        public static FirstRunStartupSnapshot CaptureAndContinue(
            IFirstRunStartupSnapshotSource source,
            Action<FirstRunStartupSnapshot> continuation)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(continuation);

            source.FindConfigLocation();
            var snapshot = new FirstRunStartupSnapshot(source.FirstRun,
                source.IsTargetPristine(source.AppDataPath));

            continuation(snapshot);
            return snapshot;
        }
    }
}
