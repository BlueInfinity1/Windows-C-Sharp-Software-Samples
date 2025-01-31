using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Threading.Tasks;

namespace NativeService
{
    class DeviceHandler
    {
        private static bool deviceAttached = false;
        private static readonly EventArrivedEventHandler deviceRemovalHandler = new EventArrivedEventHandler(HandleUSBDeviceRemovalAsync);

        public static bool IsDeviceAttached() => deviceAttached;

        public static async Task ListenToMedicalDeviceInsertionAsync()
        {
            Debugger.Break(); // Debugging break point for USB detection

            // Listen for USB insertion event
            WqlEventQuery query = new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent WHERE EventType = 2"); // 2 - device insertion
            using (ManagementEventWatcher watcher = new ManagementEventWatcher(query))
            {
                LogWriter.Write("Start listening for USB insertion event asynchronously", LogWriter.LogEventType.Event);

                while (true)
                {
                    await Task.Run(watcher.WaitForNextEvent);

                    LogWriter.Write("New USB insertion event detected", LogWriter.LogEventType.Event);

                    MedicalDevice medDev = CheckMedDevUSBConnections();
                    if (medDev != null) // A medical device has been found
                    {
                        watcher.Stop();
                        watcher.Dispose();
                        Program.OperateOnMedicalDevice(medDev);
                        return;
                    }
                }
            }
        }

        public static void ListenToMedicalDeviceRemovalAsync()
        {
            WqlEventQuery query = new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent WITHIN 1 WHERE EventType = 3"); // 3 - device removal
            using (ManagementEventWatcher watcher = new ManagementEventWatcher(query))
            {
                watcher.EventArrived += deviceRemovalHandler;
                watcher.Start();
                LogWriter.Write("Device removal listening started asynchronously", LogWriter.LogEventType.Event);
            }
        }

        static async void HandleUSBDeviceRemovalAsync(object sender, EventArrivedEventArgs e)
        {
            // TODO: This event might fire multiple times, but since this causes minimal overhead, it's likely not an issue.
            Console.WriteLine("USB Device Removal event fired: USB Device has been removed");

            if (!deviceAttached) return; // Ignore event if no device is attached

            LogWriter.Write("Medical Device is attached, handling removal", LogWriter.LogEventType.Event);

            // Check if any medical USB devices are still connected
            // TODO: Assuming only one medical device at a time. If multiple devices are supported, rework this logic.
            if (CheckMedDevUSBConnections() == null)
            {
                deviceAttached = false;

                // Stop listening for removal events once the device is removed
                if (sender is ManagementEventWatcher watcher)
                {
                    watcher.EventArrived -= deviceRemovalHandler;
                    watcher.Dispose();
                }

                await Program.HandleDeviceRemoval();
            }
        }

        public static MedicalDevice CheckMedDevUSBConnections()
        {
            using (var searcher = new ManagementObjectSearcher(@"Select * From Win32_USBHub"))
            {
                foreach (var device in searcher.Get())
                {
                    string deviceId = device.GetPropertyValue("DeviceId") as string;

                    // Ensure VID_ and PID_ exist in the device ID before extracting values
                    if (string.IsNullOrEmpty(deviceId) || !deviceId.Contains("VID_") || !deviceId.Contains("PID_"))
                        continue;

                    string vid = ExtractVidOrPid(deviceId, "VID_");
                    string pid = ExtractVidOrPid(deviceId, "PID_");

                    if (IsMedicalUSBDevice($"{vid}-{pid}")) // If we found a matching medical device
                    {
                        LogWriter.Write("A new Medical Device has been found", LogWriter.LogEventType.Event);
                        deviceAttached = true;
                        return new MedicalDevice(vid, pid);
                    }
                }
            }

            LogWriter.Write("No Medical Device found during the check", LogWriter.LogEventType.Event);
            return null;
        }

        private static string ExtractVidOrPid(string deviceId, string key)
        {
            int index = deviceId.IndexOf(key);
            return (index != -1 && index + 4 < deviceId.Length) ? deviceId.Substring(index + 4, 4) : "0000";
        }

        public static List<USBDeviceInfo> GetUSBDevices()
        {
            using (var searcher = new ManagementObjectSearcher(@"Select * From Win32_USBHub"))
            {
                List<USBDeviceInfo> devices = new List<USBDeviceInfo>();

                foreach (ManagementBaseObject device in searcher.Get())
                {
                    devices.Add(new USBDeviceInfo(
                        device.GetPropertyValue("DeviceID") as string,
                        device.GetPropertyValue("PNPDeviceID") as string,
                        device.GetPropertyValue("Description") as string
                    ));
                }

                return devices;
            }
        }

        public static string GetMedicalDeviceUSBDrive(string pid, string vid)
        {
            // Get a list of available USB devices
            List<USBDeviceInfo> usbDevices = GetUSBDevices();

            // Check each USB device for a matching VID/PID
            foreach (USBDeviceInfo usbDevice in usbDevices)
            {
                if (usbDevice.DeviceID.Contains(pid) && usbDevice.DeviceID.Contains(vid))
                {
                    foreach (string name in usbDevice.GetDiskNames())
                    {
                        return name; // NOTE: Assuming each medical device is assigned a single drive
                    }
                }
            }

            return null;
        }

        // Check if the inserted device matches a known medical device VID/PID.
        private static bool IsMedicalUSBDevice(string vidPid)
        {
            return Array.IndexOf(DeviceIdCollection.deviceIdList, vidPid) >= 0;
        }
    }
}
