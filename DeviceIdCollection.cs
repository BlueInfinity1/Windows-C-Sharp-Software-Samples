namespace NativeService
{
    // Collection of known VID-PID identifiers for different devices.
    // Additional devices can be added here as needed.
    class DeviceIdCollection
    {
        // Test USB Stick (previously "058F-6387", left empty for now)
        public const string testUsbStickId = "";

        // Nox Medical Device
        public const string noxId = "058F-6387";

        /// <summary>
        /// List of known device identifiers (VID-PID format).
        /// Modify this list to support additional devices.
        /// </summary>
        public static readonly string[] deviceIdList = { testUsbStickId, noxId };
    }
}
