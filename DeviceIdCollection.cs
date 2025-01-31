namespace NativeService
{
    class DeviceIdCollection //collections of vid-pid identifiers of different devices
    {
        //more devices can be added here later on, and possibly more info that is needed
        public const string testUsbStickId = "";//"058F-6387"; //test USB Stick
        public const string noxId = "058F-6387";//"8765-1000"; <- This is the real Nox device id

        public static readonly string[] deviceIdList = {testUsbStickId, noxId};
    }
}
