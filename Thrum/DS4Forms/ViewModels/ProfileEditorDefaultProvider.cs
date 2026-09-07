using DS4Windows;

namespace DS4WinWPF.DS4Forms.ViewModels
{
    /// <summary>
    /// Provides a detached profile initialized by the same routine used before
    /// a new or loaded profile is populated.
    /// </summary>
    public static class ProfileEditorDefaultProvider
    {
        public const int DefaultDeviceIndex = 0;

        public static BackingStore CreateDefaultStore()
        {
            BackingStore defaults = new BackingStore();
            defaults.ResetProfile(DefaultDeviceIndex);
            return defaults;
        }
    }
}
