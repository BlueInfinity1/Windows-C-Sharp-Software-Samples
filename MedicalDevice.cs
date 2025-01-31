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
            deviceType = vid + "-" + pid;
        }
        public MedicalDevice(string vid, string pid, string drive)
        {
            Vid = vid;
            Pid = pid;
            this.drive = drive;
            deviceType = vid + "-" + pid;
        }

        public string Vid { get; private set; }
        public string Pid { get; private set; }

        public void AssignDrive(string drive)
        {
            this.drive = drive;
        }

        public string GetSerialNumber()
        {
            Console.WriteLine("Getting serial number for " + Vid +"-"+Pid);
            string serialNumber = null;

            switch (deviceType)
            {
                case DeviceIdCollection.noxId:
                    if (File.Exists(drive + "DEVICE.ini"))
                    {
                        Console.WriteLine("Device.ini found");
                        LogWriter.Write("Device.ini found", LogWriter.LogEventType.Event);
                        try
                        {
                            serialNumber = IniReader.ReadIniValue(drive + "DEVICE.ini", "DeviceInfo", "SerialNumber");
                        }
                        catch (Exception)
                        {
                            LogWriter.Write("Could not get serial number from Nox Device.ini", LogWriter.LogEventType.Error);
                        }
                    }
                    else
                    {
                        Console.WriteLine("Device.ini not found");
                        LogWriter.Write("Device.ini not found", LogWriter.LogEventType.Error);
                    }
                    break;

                default:
                    LogWriter.Write("Device Type not recognized", LogWriter.LogEventType.Error);
                    break;
            }
            return serialNumber;
        }
        public string GetPatientInfoIdentifier()
        {
            string recordingValue;
            string identifier = null;

            switch (deviceType)
            {
                case DeviceIdCollection.noxId:
                    if (File.Exists(drive + "SETUP.ini"))
                    {
                        try
                        {
                            LogWriter.Write("Setup.ini found", LogWriter.LogEventType.Event);
                            recordingValue = IniReader.ReadIniValue(drive + "SETUP.ini", null, "Recording");
                            Console.WriteLine("Recording, full value: " + recordingValue);
                            //get the UUID after the first ;, it will always be the same length, 36 chars
                            identifier = recordingValue.Substring(recordingValue.IndexOf(";") + 1, 36);
                            Console.WriteLine("...and the uuid value is " + identifier);
                        }
                        catch (Exception)
                        {
                            LogWriter.Write("Could not get patient identifier from Setup.ini", LogWriter.LogEventType.Error);
                        }
                    }
                    else
                        LogWriter.Write("Setup.ini not found in the device",LogWriter.LogEventType.Error);

                    break;
            }
            return identifier;
        }
        public List<FileInfo> GetMeasuredData()
        {
            List<FileInfo> measuredData = new List<FileInfo>();
            switch (deviceType)
            {
                case DeviceIdCollection.noxId: //Grab all files from the device drive
                    try
                    {
                        string[] filePaths = Directory.GetFiles(drive, "*", SearchOption.AllDirectories);
                        Console.WriteLine("Getting all files");

                        foreach (string path in filePaths)
                        {
                            Console.WriteLine(path);
                            measuredData.Add(new FileInfo(path));
                        }
                    }    
                    catch (Exception e)
                    {
                        LogWriter.Write("Unable to get device files: " + e.Message, LogWriter.LogEventType.Error);
                    }
                    break;
            }
            return measuredData;
        }

        //Specific to the Nox Device only? Refactor by making different devices inherit from MedicalDevice when more devices get added
        public void SetDeviceClockAndScheduling(string command)
        {
            if (!DeviceHandler.IsDeviceAttached())
                return;
            try
            {
                using (FileStream commandFile = File.OpenWrite(drive + "x8aCOMMAND.NCF"))
                {
                    byte[] commandAsBytes = new UTF8Encoding(true).GetBytes(command);
                    commandFile.Write(commandAsBytes, 0, commandAsBytes.Length);
                }
            }
            catch (Exception e)
            {
                LogWriter.Write("Unable to write the clock or scheduling command to x8aCOMMAND.NCF: " + e.Message,
                    LogWriter.LogEventType.Error);
            }
        }
    }
}