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
        private static readonly EventArrivedEventHandler deviceRemovalHandler = new EventArrivedEventHandler(HandleUsbDeviceRemovalAsync);

        public static bool IsDeviceAttached()
        {
            return deviceAttached;
        }

        public static async Task ListenToMedicalDeviceInsertionAsync()
        {
            //Listen to USB device insertion
            Debugger.Break();
            WqlEventQuery query = new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent WHERE EventType = 2");
            using (ManagementEventWatcher watcher = new ManagementEventWatcher(query))
            {
                LogWriter.Write("Start listening to an USB insertion event asynchronously", LogWriter.LogEventType.Event);

                while (true)
                {
                    await Task.Run(watcher.WaitForNextEvent);

                    LogWriter.Write("New USB insertion event found", LogWriter.LogEventType.Event);

                    MedicalDevice medDev = CheckMedDevUSBConnections();
                    if (medDev != null) //we've found a med dev
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
            WqlEventQuery query = new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent WITHIN 1 WHERE EventType = 3"); //3 - device removal
            using (ManagementEventWatcher watcher = new ManagementEventWatcher(query))
            {
                watcher.EventArrived += deviceRemovalHandler;
                watcher.Start();
                LogWriter.Write("Device removal listening started asynchronously",LogWriter.LogEventType.Event);
            }
            
        }
        
        static async void HandleUsbDeviceRemovalAsync(object sender, EventArrivedEventArgs e)
        {
            //TODO: This seems to fire multiple times, but not sure whether that's really a problem as this causes
            //very little overhead
            Console.WriteLine("Handle USB Device Removal event fired: Usb Device has been removed");
            if (!deviceAttached)
                return;

            LogWriter.Write("Medical Device is attached, so handle the removal",LogWriter.LogEventType.Event);
            
            //check if there are still Medical USB devices connected
            //TODO: We're assuming that only one medical device can be plugged in at a time. If this changes,
            //we'll need to rework this a bit
            if (CheckMedDevUSBConnections() == null)
            {
                deviceAttached = false;

                ((ManagementEventWatcher)sender).EventArrived -= deviceRemovalHandler;
                ((ManagementEventWatcher)sender).Dispose(); //stop watching removal events once this has been triggered

                await Program.HandleDeviceRemoval();
            }
        }

        public static MedicalDevice CheckMedDevUSBConnections()
        {
            ManagementObjectCollection collection;
            using (var searcher = new ManagementObjectSearcher(@"Select * From Win32_USBHub"))
                collection = searcher.Get();

            foreach (var device in collection)
            {
                string deviceId = (string)device.GetPropertyValue("DeviceId");

                int vidIndex = deviceId.IndexOf("VID_");
                string startingAtVid = deviceId.Substring(vidIndex + 4); // + 4 to remove "VID_"                    
                string vid = startingAtVid.Substring(0, 4); // vid is four characters long

                int pidIndex = deviceId.IndexOf("PID_");
                string startingAtPid = deviceId.Substring(pidIndex + 4); // + 4 to remove "PID_"                    
                string pid = startingAtPid.Substring(0, 4); // pid is four characters long

                if (IsMedicalUSBDevice(vid +"-"+pid)) //we've found a device that fulfills our criteria
                {
                    LogWriter.Write("A new Medical Device has been found", LogWriter.LogEventType.Event);
                    deviceAttached = true;
                    return new MedicalDevice(vid, pid);
                }
            }

            LogWriter.Write("No Medical Device Found during the check", LogWriter.LogEventType.Event);
            return null; //no device matches our criteria
        }


        /*public static List<string> GetUsbDriveLetters()
        {
            List<string> usbDriveLetters = new List<string>();
            foreach (ManagementObject drive in new ManagementObjectSearcher("Select * from Win32_DiskDrive WHERE InterfaceType='USB'").Get())
                foreach (ManagementObject o in drive.GetRelated("Win32_DiskPartition"))
                    foreach (ManagementObject i in o.GetRelated("Win32_LogicalDisk"))
                    {
                        usbDriveLetters.Add(string.Format("{0}\\", i["Name"]));
                        Console.WriteLine("Add USB drive letter: " + usbDriveLetters[usbDriveLetters.Count-1]);
                    }

            return usbDriveLetters;
        }*/

        public static List<USBDeviceInfo> GetUSBDevices()
        {
            List<USBDeviceInfo> devices = new List<USBDeviceInfo>();

            ManagementObjectCollection collection;
            using (var searcher = new ManagementObjectSearcher(@"Select * From Win32_USBHub"))
                collection = searcher.Get();

            foreach (ManagementBaseObject device in collection)
            {
                devices.Add(new USBDeviceInfo(
                (string)device.GetPropertyValue("DeviceID"),
                (string)device.GetPropertyValue("PNPDeviceID"),
                (string)device.GetPropertyValue("Description")
                ));
            }

            collection.Dispose();
            return devices;
        }

        public static string GetMedicalDeviceUSBDrive(string pid, string vid)
        {
            //Get a list of available devices attached to the USB hub
            List<USBDeviceInfo> usbDevices = GetUSBDevices();

            //Enumerate the USB devices to see if any have specific VID/PID
            foreach (USBDeviceInfo usbDevice in usbDevices)
            {
                if (usbDevice.DeviceID.Contains(pid) && usbDevice.DeviceID.Contains(vid))
                {
                    foreach (string name in usbDevice.GetDiskNames())
                    {
                        return name; //NOTE: We assume that all medical devices will be assigned a single drive!
                        //If some devices use more than one drive, then we will have to change this a little bit.
                        //Unlikely though?
                    }
                }
            }

            return null;
        }

        //check whether the inserted device is a medical device
        private static bool IsMedicalUSBDevice(string vidPid)
        {
            if (Array.IndexOf(DeviceIdCollection.deviceIdList, vidPid) > 0) //the id of the device is in the list of supported devices
                return true;
            else
                return false;
        }

    }
}