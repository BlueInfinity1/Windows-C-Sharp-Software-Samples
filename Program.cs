using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Text;
using System.Threading;
using System.Security.Cryptography;
using zlib;
using System.Windows.Forms;
using System.Diagnostics;

namespace NativeService
{
    static class Program
    {
        private static Guid instanceGuid;
        private static TaskBarNotifier taskBarNotifier;
        private static readonly string serverUrl = "wss://localhost:8080"; // TODO: Change to the proper server URL

        private static MedicalDevice medDev;
        private static string serialNumber; // medDev SerialNumber
        private static string patientInfoId;
        private static List<FileInfo> measuredData;

        // Some variables that multiple methods of this class need to access
        private static CryptographyHandler cryptoHandler;
        private static byte[] encryptedDataHashSum;
        private static byte[] packedDataHashSum;
        private static byte[] outFileAsBytes;

        public static bool deviceInitialized = false; // We're assuming we handle only 1 device at a time
        public static bool deviceInsertionMessageSent = false;

        // The next variables are used to make sure the WebSocket messages are sent in an organized way depending on the program state,
        // and that there are never overlapping send and receive WebSocket operations
        public static bool allowRemovalMessageSending = false;
        public static bool removalMessageSendingQueued = false;
        public static bool allowInsertionMessageSending = false;
        public static bool insertionMessageSendingQueued = false;

        private static ProgramState state = ProgramState.Initialized;
        private static ProgramState stateBeforeReconnecting;

        public enum ProgramState
        {
            Initialized,
            DeviceRecognized,
            DataPacked,
            DataSent,
            DeviceInitialized,
            ConnectionLost,
            Reconnected
        }

        public static void ChangeProgramState(ProgramState newState)
        {
            state = newState;
        }

        public static ProgramState GetProgramState()
        {
            return state;
        }

        [STAThread]
        static void Main()
        {
            LogWriter.Write("Service Started", LogWriter.LogEventType.Event);
            Task mainTask = Task.Run(RunApplicationMainLoop);

            // The task bar notifier is started in another thread than the main logic,
            // as Windows Forms requires the thread that handles the form logic to be single-threaded (blocking).
            StartTaskBarNotifier();
        }

        private static void StartTaskBarNotifier()
        {
            LogWriter.Write("Starting Taskbar GUI", LogWriter.LogEventType.Event);
            Application.Run(taskBarNotifier = new TaskBarNotifier());
        }

        public static async Task RunApplicationMainLoop()
        {
            LogWriter.Write("Starting main logic loop", LogWriter.LogEventType.Event);

            // Create cryptographic keys to be used later for encrypting data packages.
            cryptoHandler = new CryptographyHandler();

            LogWriter.Write("Starting connection task", LogWriter.LogEventType.Event);

            // Establish WebSocket connection.
            Task connectionTask = ConnectionHandler.OpenClientConnectionAsync(serverUrl);

            taskBarNotifier.ShowBalloonTip("Attempting connect to the server...", 1000);

            // Start the app.
            if (medDev != null)
            {
                insertionMessageSendingQueued = true; // Queue sending the insertion message for when we've established connection.
            }
            
            /* The states get changed as follows:
             * - Whenever the device is removed, set the state back to "Initialized", because we will need to
             *   recognize the device again and possibly check whether it contains new data and whether we need to send the data
             *   and initialize the device.
             *   
             * - Whenever connection gets cut off during the data sending or device configuration phases, we'll reconnect
             *   and continue from whichever online operation was stopped midway.
             */

            while (true)
            {
                Console.WriteLine("*****************Program state is " + state + "**********************");
                switch (state)
                {
                    case ProgramState.Initialized:
                        try
                        {
                            medDev = DeviceHandler.CheckMedDevUSBConnections(); // Check if a device is already attached.
                            if (medDev == null)
                            {
                                // If not, start listening. The method will call OperateOnMedicalDevice(medDev) upon completion.
                                await DeviceHandler.ListenToMedicalDeviceInsertionAsync();
                            }
                            else
                            {
                                OperateOnMedicalDevice(medDev);
                            }

                            // After the device has been detected, listen to its removal.
                            DeviceHandler.ListenToMedicalDeviceRemovalAsync();
                            ChangeProgramState(ProgramState.DeviceRecognized);
                        }
                        catch (Exception e) // If there's a problem fetching Medical Device data, try this phase again.
                        {
                            LogWriter.Write("Try device recognition and data fetching again due to an error: " + e.Message, LogWriter.LogEventType.Error);
                        }
                        break;

                    case ProgramState.DeviceRecognized:
                        if (DeviceHandler.IsDeviceAttached())
                        {
                            CreateDataPackage();
                            ChangeProgramState(ProgramState.DataPacked);
                        }
                        else
                        {
                            ChangeProgramState(ProgramState.Initialized);
                        }
                        break;

                    case ProgramState.DataPacked:
                        if (ConnectionHandler.IsConnectionEstablished())
                        {
                            try
                            {
                                if (insertionMessageSendingQueued)
                                {
                                    await ConnectionHandler.SendDeviceInsertionOrRemovalMessageAsync(instanceGuid.ToString(), serialNumber, true);
                                }
                                await SendDataPackage();
                                ChangeProgramState(ProgramState.DataSent);
                            }
                            catch (Exception e)
                            {
                                LogWriter.Write("State machine: Connection has been lost in the middle of main chunk sending task (novelty check / chunk sendings / verification) or while sending insertion/removal message: " + e.Message,
                                    LogWriter.LogEventType.Error);
                                stateBeforeReconnecting = state;
                                ChangeProgramState(ProgramState.ConnectionLost);
                            }
                        }
                        else
                        {
                            if (!connectionTask.IsCompleted)
                            {
                                await connectionTask;
                            }
                            else
                            {
                                // The initial connection task has been completed, but we still don't have the connection, so use RetryConnection methods.
                                stateBeforeReconnecting = state;
                                ChangeProgramState(ProgramState.ConnectionLost);
                            }
                        }
                        break;

                    case ProgramState.DataSent:
                        if (DeviceHandler.IsDeviceAttached())
                        {
                            try
                            {
                                // The task will need to be canceled if connection is lost or the device is unplugged.
                                await ConnectionHandler.SendRecordingInitializationMessageAsync(instanceGuid.ToString(), serialNumber);
                                ChangeProgramState(ProgramState.DeviceInitialized);
                            }
                            catch (OperationCanceledException)
                            {
                                LogWriter.Write("Initialization message listening was canceled due to device removal, revert back to listening to device insertions",
                                    LogWriter.LogEventType.Error);
                                ChangeProgramState(ProgramState.Initialized);
                            }
                            catch (System.Net.WebSockets.WebSocketException)
                            {
                                LogWriter.Write("Program state machine: Init message task aborted due to connection problems",
                                    LogWriter.LogEventType.Error);
                                stateBeforeReconnecting = state;
                                ChangeProgramState(ProgramState.ConnectionLost);
                            }
                        }
                        else
                        {
                            // If the device was removed during the data sending phase, send the removal message.
                            allowRemovalMessageSending = true;
                            if (removalMessageSendingQueued)
                            {
                                await ConnectionHandler.ResendPendingInsertionOrRemovalMessages(instanceGuid.ToString(), serialNumber, false);
                            }
                            ChangeProgramState(ProgramState.Initialized);
                        }
                        break;

                    case ProgramState.DeviceInitialized:
                        // Do nothing but keep on listening to parametrization messages.
                        taskBarNotifier.ChangeIconHoverText(""); // Empty the hover text.
                        try
                        {
                            await ConnectionHandler.ListenToDeviceParametrizationMessagesAsync(medDev);
                        }
                        catch (OperationCanceledException)
                        {
                            LogWriter.Write("Program state machine: Device Parametrization message listening cancelled",
                                LogWriter.LogEventType.Event);
                            ChangeProgramState(ProgramState.Initialized);
                        }
                        catch (System.Net.WebSockets.WebSocketException)
                        {
                            LogWriter.Write("Program state machine: Device Parametrization message listening aborted due to connection problems",
                                LogWriter.LogEventType.Error);
                            stateBeforeReconnecting = state;
                            ChangeProgramState(ProgramState.ConnectionLost);
                        }
                        break;

                    case ProgramState.ConnectionLost:
                        await ConnectionHandler.RetryClientConnectionAsync(serverUrl); // This will actually be awaited forever if reconnection fails.
                        ChangeProgramState(ProgramState.Reconnected);

                        // We don't catch exceptions here because the reason for these failing is most likely a lost connection,
                        // and we should be reconnected by now.
                        if (insertionMessageSendingQueued)
                        {
                            await ConnectionHandler.ResendPendingInsertionOrRemovalMessages(instanceGuid.ToString(), serialNumber, true);
                        }
                        if (removalMessageSendingQueued)
                        {
                            await ConnectionHandler.ResendPendingInsertionOrRemovalMessages(instanceGuid.ToString(), serialNumber, false);
                        }
                        break;

                    case ProgramState.Reconnected:
                        // After reconnection, revert back to the phase we were performing previously.
                        ChangeProgramState(stateBeforeReconnecting);
                        break;
                }
            }
        }

        public static void OperateOnMedicalDevice(MedicalDevice medDev)
        {
            // At this point, we are sure that a medical device has been detected, but it may not have been assigned a drive yet,
            // which is why we wait until the drive exists and can be recognized.
            string medDevDrive;

            while (true)
            {
                medDevDrive = DeviceHandler.GetMedicalDeviceUSBDrive(medDev.Vid, medDev.Pid);

                if (medDevDrive != null)
                {
                    LogWriter.Write("Medical device has been assigned a drive (" + medDevDrive + ")", LogWriter.LogEventType.Event);
                    break;
                }
                else
                {
                    Console.WriteLine("Waiting 1 sec to see if the Medical Device has been assigned a drive already so that we can find it");
                    LogWriter.Write("Medical device has been detected but not yet assigned a drive, trying again in a second...", LogWriter.LogEventType.Event);
                    Task.Delay(1000).Wait();
                }
            }

            medDev.AssignDrive(medDevDrive);

            // Create GUID for the current session instance.
            instanceGuid = Guid.NewGuid();

            try
            {
                // Get medical device data.
                LogWriter.Write("Getting serial number of the device", LogWriter.LogEventType.Event);
                serialNumber = medDev.GetSerialNumber();
                LogWriter.Write("Getting recording ID", LogWriter.LogEventType.Event);
                patientInfoId = medDev.GetPatientInfoIdentifier();
                LogWriter.Write("Fetching measured Data", LogWriter.LogEventType.Event);
                measuredData = medDev.GetMeasuredData();
            }
            catch (Exception e)
            {
                taskBarNotifier.ShowBalloonTip("There is a problem fetching the Medical Device data.", 5000,
                    "There is a problem fetching the Medical Device data. Please make sure that the device is ready. " +
                    "Try removing and reinserting the device.");
                LogWriter.Write("Problem fetching medical device data: " + e.Message, LogWriter.LogEventType.Error);
                throw;
            }
        }

        // Although separate DataPacker and CryptoHandler classes exist, this method performs both packing and encrypting in a single process.
        // By handling multiple streams concurrently, it manages encryption, compression, and hash calculation sequentially in one pass.
        // Separating these operations would require multiple data passes, reducing efficiency, so they are combined for optimal performance.
        private static void CreateDataPackage()
        {
            Aes myAes = Aes.Create();
            myAes.KeySize = 256; // 32 bytes
            myAes.BlockSize = 128; // 16 bytes
            myAes.Mode = CipherMode.CBC;
            myAes.Padding = PaddingMode.PKCS7;

            ICryptoTransform aesEncryptor = myAes.CreateEncryptor();

            try
            {
                string targetDirectoryPath = @"C:\Pack Test"; // TODO: Correct directory

                if (!Directory.Exists(targetDirectoryPath))
                    Directory.CreateDirectory(targetDirectoryPath);

                string outFilePath = Path.Combine(targetDirectoryPath, "Packed Data.cmp");

                SHA1CryptoServiceProvider packedDataSHA1 = new SHA1CryptoServiceProvider();
                MemoryStream msSHA1Packed = new MemoryStream();
                CryptoStream packedDataSHA1Stream = new CryptoStream(msSHA1Packed, packedDataSHA1, CryptoStreamMode.Write);

                long totalBytes = 0;
                byte[] guidAsBytes;

                for (int i = 0; i < measuredData.Count; i++)
                    totalBytes += measuredData[i].Length;

                LogWriter.Write("Ready to pack data", LogWriter.LogEventType.Event);
                taskBarNotifier.ShowBalloonTip("Preparing device data for transfer...", 5000);

                int chunkSize = 65536;

                using (FileStream outFileStream = File.Create(outFilePath))
                {
                    guidAsBytes = Encoding.ASCII.GetBytes(patientInfoId);
                    outFileStream.Write(guidAsBytes, 0, guidAsBytes.Length);
                    Console.WriteLine("Guid as bytes is: " + ConvertDataToString(guidAsBytes) +
                        "\nAes init vector is " + ConvertDataToString(myAes.IV));

                    outFileStream.Write(myAes.IV, 0, myAes.IV.Length); // Write AES init vector in the beginning
                    Console.WriteLine("IV: " + ConvertDataToString(myAes.IV) + "\nKey: " + ConvertDataToString(myAes.Key));

                    outFileStream.CopyTo(packedDataSHA1Stream);

                    using (CryptoStream encryptingStream = new CryptoStream(outFileStream, aesEncryptor, CryptoStreamMode.Write))
                    {
                        byte[] fileStreamBuffer = new byte[chunkSize];
                        byte[] compressionBuffer = new byte[chunkSize]; // The compressed block size will usually be less than the original block.
                        int readBytes;
                        int totalReadBytes = 0;
                        int compressedBytes = 0;
                        int totalCompressedBytes = 0;

                        ZStream compressStream = new ZStream();
                        compressStream.deflateInit(6); // Default compress level (6) is a compromise between speed and compression.

                        int fileCounter = 0;
                        LogWriter.Write("Starting file compression", LogWriter.LogEventType.Event);

                        while (fileCounter < measuredData.Count)
                        {
                            FileInfo currentFile = measuredData[fileCounter];
                            LogWriter.Write("Handling file number " + fileCounter + ": " + currentFile.FullName, LogWriter.LogEventType.Event);

                            int percentage = 0;
                            using (FileStream inFileStream = currentFile.OpenRead())
                            {
                                while ((readBytes = inFileStream.Read(fileStreamBuffer, 0, fileStreamBuffer.Length)) != 0)
                                {
                                    totalReadBytes += readBytes;
                                    Console.WriteLine("Read " + readBytes + " Total Progress: " + totalReadBytes + " / " + totalBytes + " bytes");
                                    percentage = (int)Math.Round(((double)totalReadBytes / totalBytes) * 100);
                                    Console.WriteLine("Percentage: " + percentage);
                                    taskBarNotifier.ChangeIconHoverText(percentage + " % complete");

                                    compressStream.next_in = fileStreamBuffer;
                                    compressStream.next_in_index = 0;
                                    compressStream.avail_in = readBytes;
                                    compressStream.next_out = compressionBuffer;
                                    compressStream.avail_out = compressionBuffer.Length;
                                    compressStream.next_out_index = 0;

                                    int deflateState = compressStream.deflate(3); // 3 is "Z_FULL_FLUSH"
                                    compressedBytes = (int)compressStream.total_out - totalCompressedBytes;
                                    Console.WriteLine("Which were compressed into " + compressedBytes + " bytes");
                                    totalCompressedBytes += compressedBytes;
                                    packedDataSHA1Stream.Write(compressionBuffer, 0, compressedBytes);
                                    encryptingStream.Write(compressionBuffer, 0, compressedBytes);
                                }
                            }
                            fileCounter++;
                        }

                        int endStatus = compressStream.deflateEnd();
                        LogWriter.Write("Compress end, status message is " + endStatus + " (-3 = error, 0 = okay)", LogWriter.LogEventType.Event);
                        compressStream.free();
                        encryptingStream.FlushFinalBlock();
                    }
                }

                packedDataSHA1Stream.FlushFinalBlock();
                packedDataHashSum = packedDataSHA1.Hash;
                packedDataSHA1Stream.Dispose();

                LogWriter.Write("Calculating encrypted data hashsum", LogWriter.LogEventType.Event);
                Console.WriteLine("Reading all bytes from " + outFilePath);
                outFileAsBytes = File.ReadAllBytes(outFilePath);
                byte[] outFileWithoutHeader = new byte[outFileAsBytes.Length - (myAes.IV.Length + guidAsBytes.Length)];
                Console.WriteLine("Output length: " + outFileAsBytes.Length);
                Console.WriteLine("Output length without header: " + outFileWithoutHeader.Length);
                Array.Copy(outFileAsBytes, myAes.IV.Length + guidAsBytes.Length, outFileWithoutHeader, 0, outFileWithoutHeader.Length);
                Console.WriteLine("Out file as bytes: " + ConvertDataToString(outFileAsBytes));
                encryptedDataHashSum = cryptoHandler.CalculateHashSum(outFileAsBytes);
                Console.WriteLine("Hash for packed data: " + ConvertDataToString(packedDataHashSum) +
                    "\nHash for encrypted data: " + ConvertDataToString(encryptedDataHashSum));
            }
            catch (Exception e)
            {
                LogWriter.Write("There was a problem constructing the data package: " + e.Message, LogWriter.LogEventType.Error);
            }
        }

        private static async Task SendDataPackage()
        {
            LogWriter.Write("Moving onto chunk sending", LogWriter.LogEventType.Event);
            allowRemovalMessageSending = false;
            bool dataExistsOnServer;

            try
            {
                Task<bool> noveltyCheckTask = ConnectionHandler.CheckDeviceDataNoveltyAsync(
                    instanceGuid.ToString(),
                    serialNumber,
                    patientInfoId,
                    ConvertDataToString(encryptedDataHashSum, false));
                dataExistsOnServer = await noveltyCheckTask;
            }
            catch (Exception e)
            {
                LogWriter.Write("Could not verify whether data exists on the server or not: " + e.Message,
                    LogWriter.LogEventType.Error);
                throw;
            }

            LogWriter.Write("Novelty check done, data exists on the server: " + dataExistsOnServer, LogWriter.LogEventType.Event);
            try
            {
                Console.WriteLine("reading dataExistsOnServer: " + dataExistsOnServer);
            }
            catch (Exception)
            {
                Console.WriteLine("null ref");
                Debugger.Break();
            }

            if (!dataExistsOnServer)
            {
                taskBarNotifier.ShowBalloonTip("Transfering data to server...", 5000);
                while (true)
                {
                    try
                    {
                        Task chunkSendingTask = ConnectionHandler.SendDataAsChunksAsync(
                            outFileAsBytes,
                            cryptoHandler,
                            instanceGuid.ToString(),
                            serialNumber,
                            patientInfoId);
                        await chunkSendingTask;

                        if (await ConnectionHandler.VerifyOriginalDataIntegrityAsync(
                            instanceGuid.ToString(),
                            serialNumber,
                            patientInfoId,
                            ConvertDataToString(packedDataHashSum, false),
                            ConvertDataToString(encryptedDataHashSum, false)))
                        {
                            break;
                        }
                        else
                        {
                            continue;
                        }
                    }
                    catch (Exception e)
                    {
                        LogWriter.Write("The connection appears to have been cut off in the middle of sending chunks: " + e.Message
                            + "\n Chunk sending will be restarted after reconnecting.", LogWriter.LogEventType.Error);
                        throw;
                    }
                }
            }
            else
            {
                LogWriter.Write("Data already exists on the server!", LogWriter.LogEventType.Event);
            }
        }

        public static async Task HandleDeviceRemoval()
        {
            if (state == ProgramState.DataSent || state == ProgramState.DeviceInitialized)
            {
                if (state == ProgramState.DataSent)
                    LogWriter.Write("Device removed while doing the init message task, so cancel the task", LogWriter.LogEventType.Error);
                else if (state == ProgramState.DeviceInitialized)
                    LogWriter.Write("Device removed while listening to param messages, cancel param message listening", LogWriter.LogEventType.Error);

                ConnectionHandler.CancelCurrentSendingTask();
                ConnectionHandler.CancelCurrentReceptionTask();

                ChangeProgramState(ProgramState.Initialized);
                stateBeforeReconnecting = ProgramState.Initialized;

                DeviceHandler.ListenToMedicalDeviceInsertionAsync();
                Debugger.Break();

                if (allowRemovalMessageSending)
                {
                    Console.WriteLine("The removed USB Device was a medical device");
                    try
                    {
                        await ConnectionHandler.SendDeviceInsertionOrRemovalMessageAsync(instanceGuid.ToString(), serialNumber, false);
                    }
                    catch (Exception e)
                    {
                        LogWriter.Write("Could not resend removal message to the server: " + e.Message,
                            LogWriter.LogEventType.Error);
                    }
                }
                else
                {
                    removalMessageSendingQueued = true;
                }
            }
        }

        //TODO: Put in its own utility class. However currently, this would be the only method there.
        public static string ConvertDataToString(byte[] bytes, bool useSpacing = true, int lastIndex = 32)
        {
            int numOfBytes = (lastIndex == -1 || lastIndex > bytes.Length) ? bytes.Length : lastIndex;
            string space = useSpacing ? " " : "";
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < numOfBytes; i++)
            {
                builder.Append(bytes[i].ToString("x2") + space);
            }
            return builder.ToString();
        }
    }
}
