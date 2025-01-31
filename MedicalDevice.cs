using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NativeService
{
    internal class MedicalDevice
    {
        private string drive;
        private readonly string deviceType;

        public MedicalDevice(string vid, string pid)
        {
            Vid = vid;
            Pid = pid;
            drive = null;
            deviceType = $"{vid}-{pid}";
        }

        public MedicalDevice(string vid, string pid, string drive)
        {
            Vid = vid;
            Pid = pid;
            this.drive = drive;
            deviceType = $"{vid}-{pid}";
        }

        public string Vid { get; private set; }
        public string Pid { get; private set; }

        public void AssignDrive(string drive)
        {
            this.drive = drive;
        }

        public string GetSerialNumber()
        {
            Console.WriteLine($"Getting serial number for {Vid}-{Pid}");
            string serialNumber = null;

            switch (deviceType)
            {
                case DeviceIdCollection.noxId:
                    string deviceIniPath = Path.Combine(drive, "DEVICE.ini");

                    if (File.Exists(deviceIniPath))
                    {
                        Console.WriteLine("DEVICE.ini found");
                        LogWriter.Write("DEVICE.ini found", LogWriter.LogEventType.Event);
                        try
                        {
                            serialNumber = IniReader.ReadIniValue(deviceIniPath, "DeviceInfo", "SerialNumber");
                        }
                        catch (Exception)
                        {
                            LogWriter.Write("Could not get serial number from Nox DEVICE.ini", LogWriter.LogEventType.Error);
                        }
                    }
                    else
                    {
                        Console.WriteLine("DEVICE.ini not found");
                        LogWriter.Write("DEVICE.ini not found", LogWriter.LogEventType.Error);
                    }
                    break;

                default:
                    LogWriter.Write("Device type not recognized", LogWriter.LogEventType.Error);
                    break;
            }

            return serialNumber;
        }

        public string GetPatientInfoIdentifier()
        {
            string identifier = null;

            switch (deviceType)
            {
                case DeviceIdCollection.noxId:
                    string setupIniPath = Path.Combine(drive, "SETUP.ini");

                    if (File.Exists(setupIniPath))
                    {
                        try
                        {
                            LogWriter.Write("SETUP.ini found", LogWriter.LogEventType.Event);
                            string recordingValue = IniReader.ReadIniValue(setupIniPath, null, "Recording");

                            Console.WriteLine($"Recording, full value: {recordingValue}");

                            // Extract UUID (after first ';', always 36 characters long)
                            int index = recordingValue.IndexOf(";");
                            if (index >= 0 && index + 37 <= recordingValue.Length)
                            {
                                identifier = recordingValue.Substring(index + 1, 36);
                                Console.WriteLine($"Extracted UUID value: {identifier}");
                            }
                            else
                            {
                                LogWriter.Write("Invalid format in SETUP.ini recording value", LogWriter.LogEventType.Error);
                            }
                        }
                        catch (Exception)
                        {
                            LogWriter.Write("Could not get patient identifier from SETUP.ini", LogWriter.LogEventType.Error);
                        }
                    }
                    else
                    {
                        LogWriter.Write("SETUP.ini not found on the device", LogWriter.LogEventType.Error);
                    }
                    break;
            }

            return identifier;
        }

        public List<FileInfo> GetMeasuredData()
        {
            List<FileInfo> measuredData = new List<FileInfo>();

            switch (deviceType)
            {
                case DeviceIdCollection.noxId:
                    try
                    {
                        Console.WriteLine("Retrieving all files...");
                        string[] filePaths = Directory.GetFiles(drive, "*", SearchOption.AllDirectories);

                        foreach (string path in filePaths)
                        {
                            Console.WriteLine(path);
                            measuredData.Add(new FileInfo(path));
                        }
                    }
                    catch (Exception e)
                    {
                        LogWriter.Write($"Unable to retrieve device files: {e.Message}", LogWriter.LogEventType.Error);
                    }
                    break;
            }

            return measuredData;
        }

        // This method is currently specific to the Nox Device.
        // When more devices are added, consider refactoring this class so that different device types inherit from MedicalDevice.
        public void SetDeviceClockAndScheduling(string command)
        {
            if (!DeviceHandler.IsDeviceAttached())
                return;

            string commandFilePath = Path.Combine(drive, "x8aCOMMAND.NCF");

            try
            {
                using (FileStream commandFile = File.OpenWrite(commandFilePath))
                {
                    byte[] commandBytes = Encoding.UTF8.GetBytes(command);
                    commandFile.Write(commandBytes, 0, commandBytes.Length);
                }
            }
            catch (Exception e)
            {
                LogWriter.Write($"Failed to write clock/scheduling command to x8aCOMMAND.NCF: {e.Message}", LogWriter.LogEventType.Error);
            }
        }
    }
}
