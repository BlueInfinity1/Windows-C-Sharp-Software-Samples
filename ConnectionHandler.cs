using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
using System.IO;
using System.Diagnostics;

namespace NativeService
{
    class ConnectionHandler
    {
        private static ClientWebSocket ws = null;
        private static CancellationTokenSource sendingCanceller;
        private static CancellationTokenSource receptionCanceller;

        public static bool IsConnectionEstablished()
        {
            return ws?.State == WebSocketState.Open;
        }

        public static async Task OpenClientConnectionAsync(string serverUriString)
        {
            LogWriter.Write("Attempting to establish connection...", LogWriter.LogEventType.Event);
            Uri serverUri = new Uri(serverUriString);
            int retryCount = 0;

            while (true) // Infinite retry loop
            {
                if (ws != null)
                {
                    ws.Abort();
                    ws.Dispose();
                }

                ws = new ClientWebSocket();
                try
                {
                    await ws.ConnectAsync(serverUri, CancellationToken.None);
                    LogWriter.Write("Connection successful", LogWriter.LogEventType.Event);

                    sendingCanceller = new CancellationTokenSource();
                    receptionCanceller = new CancellationTokenSource();
                    return;
                }
                catch (WebSocketException e)
                {
                    retryCount++;
                    int delay = Math.Min(retryCount * 1000, 30000); // Exponential backoff, max 30 sec

                    LogWriter.Write($"Connection failed, retrying in {delay / 1000} sec... Error: {e.Message}", LogWriter.LogEventType.Error);
                    await Task.Delay(delay);

                    if (retryCount % 10 == 0)
                        LogWriter.Write($"Warning: {retryCount} consecutive connection failures. Server might be down.", LogWriter.LogEventType.Warning);
                }
            }
        }

        public static void AbortClientConnection()
        {
            if (ws == null)
            {
                LogWriter.Write("Attempted to abort, but WebSocket is already null.", LogWriter.LogEventType.Warning);
                return;
            }

            LogWriter.Write($"Aborting connection, WebSocket state is {ws.State}", LogWriter.LogEventType.Event);
            ws.Abort();
            ws.Dispose();
            ws = null;
            LogWriter.Write("Connection aborted and disposed.", LogWriter.LogEventType.Event);
        }

        public static async Task CloseClientConnectionAsync()
        {
            if (ws == null)
            {
                LogWriter.Write("Attempted to close, but WebSocket is already null.", LogWriter.LogEventType.Warning);
                return;
            }

            LogWriter.Write("Closing connection...", LogWriter.LogEventType.Event);
            DisposeCancellationTokenSources();

            try
            {
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing connection", CancellationToken.None);
                LogWriter.Write("Connection closed", LogWriter.LogEventType.Event);
            }
            catch (WebSocketException e)
            {
                LogWriter.Write($"WebSocket close error: {e.Message}", LogWriter.LogEventType.Error);
            }
            finally
            {
                ws.Dispose();
                ws = null;
            }
        }

        public static async Task RetryClientConnectionAsync(string serverUrl)
        {
            if (ws == null || ws?.State == WebSocketState.Open)
                return;

            DisposeCancellationTokenSources();
            LogWriter.Write("Attempting to reconnect after an aborted connection", LogWriter.LogEventType.Event);

            try
            {
                await OpenClientConnectionAsync(serverUrl);
            }
            catch (Exception e)
            {
                LogWriter.Write($"Could not reconnect: {e.Message}", LogWriter.LogEventType.Error);
                throw;
            }
        }

        public static async Task ResendPendingInsertionOrRemovalMessages(string instanceGuid, string serialNumber, bool isDeviceAttached)
        {
            try
            {
                await SendDeviceInsertionOrRemovalMessageAsync(instanceGuid, serialNumber, isDeviceAttached);
            }
            catch (Exception e)
            {
                LogWriter.Write("Could not send the pending insertion or removal message: " + e.Message, LogWriter.LogEventType.Error);
                throw;
            }
        }

        public static async Task SendDeviceInsertionOrRemovalMessageAsync(string instanceGuid, string serialNumber, bool isDeviceAttached)
        {
            string attachStatus = isDeviceAttached ? "attached" : "detached";

            string jsonMsg = $@"{{
                ""op"": ""device_status"",
                ""meta"": {{
                    ""instance"": ""{instanceGuid}"",
                    ""serialnumber"": ""{serialNumber}""
                }},
                ""data"": {{
                    ""status"": ""{attachStatus}""
                }}
            }}";

            Console.WriteLine($"Conn handler: Device removal/insertion message: {jsonMsg}");

            try
            {
                await SendDataAsync(jsonMsg);
                string serverResponse = await ReceiveDataAsync(65536);
                LogWriter.Write($"Received response for device {attachStatus}: {serverResponse}", LogWriter.LogEventType.Event);
            }
            catch (Exception e)
            {
                LogWriter.Write($"Unable to send device insertion/removal message: {e.Message}", LogWriter.LogEventType.Error);
                throw;
            }

            // Mark that this operation is not in the queue anymore
            if (isDeviceAttached)
                Program.insertionMessageSendingQueued = false;
            else
                Program.removalMessageSendingQueued = false;
        }

        public static async Task SendRecordingInitializationMessageAsync(string instanceGuid, string serialNumber)
        {
            string jsonMsg = $@"{{
                ""op"": ""initialize_recording"",
                ""meta"": {{
                    ""instance"": ""{instanceGuid}"",
                    ""serialnumber"": ""{serialNumber}""
                }}
            }}";

            Console.WriteLine($"Conn handler: Recording initialization message: {jsonMsg}");

            try
            {
                await SendDataAsync(jsonMsg);
                string serverResponse = await ReceiveDataAsync(65536);
                LogWriter.Write($"Received response to recording initialization: {serverResponse}", LogWriter.LogEventType.Event);
            }
            catch (Exception e)
            {
                LogWriter.Write($"Recording Init Message Task failed: {e}", LogWriter.LogEventType.Error);
                throw;
            }
        }

        public static async Task ListenToDeviceParametrizationMessagesAsync(MedicalDevice medDev)
        {
            LogWriter.Write("Begin listening to parametrization messages", LogWriter.LogEventType.Event);

            try
            {
                string serverResponse = await ReceiveDataAsync(65536);

                if (!string.IsNullOrEmpty(serverResponse))
                {
                    string responsePayload = JsonHandler.GetTokenValue(serverResponse, "data.payload");

                    try
                    {
                        byte[] payloadBytes = Convert.FromBase64String(responsePayload);
                        string clockOrSchedulingCommand = Encoding.ASCII.GetString(payloadBytes);

                        LogWriter.Write($"Set device clock with command: {clockOrSchedulingCommand}", LogWriter.LogEventType.Event);
                        medDev.SetDeviceClockAndScheduling(clockOrSchedulingCommand);
                    }
                    catch (FormatException fe)
                    {
                        LogWriter.Write($"Invalid Base64 payload received: {fe.Message}", LogWriter.LogEventType.Error);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                LogWriter.Write("Parametrization message listening was canceled.", LogWriter.LogEventType.Warning);
                throw;
            }
            catch (WebSocketException)
            {
                LogWriter.Write("WebSocket connection problem while listening for parametrization messages", LogWriter.LogEventType.Error);
                throw;
            }
            catch (Exception e)
            {
                LogWriter.Write($"Unexpected error in parametrization: {e.Message}", LogWriter.LogEventType.Error);
                throw;
            }
        }

        public static async Task<bool> CheckDeviceDataNoveltyAsync(string instanceGuid, string serialNumber, string dataGuid, string dataHash)
        {
            string jsonMsg = $@"{{
                ""op"": ""recording_exists"",
                ""meta"": {{
                    ""instance"": ""{instanceGuid}"",
                    ""serialnumber"": ""{serialNumber}""
                }},
                ""data"": {{
                    ""uuid"": ""{dataGuid}"",
                    ""hash"": ""{dataHash}""
                }}
            }}";

            Console.WriteLine($"Conn handler: Sending data novelty check message: {jsonMsg}");

            try
            {
                await SendDataAsync(jsonMsg);
                Console.WriteLine("Waiting for server response...");
                string serverResponse = await ReceiveDataAsync(65536);
                Console.WriteLine($"Received server response: {serverResponse}");
                return serverResponse.Contains("uploaded");
            }
            catch (Exception e)
            {
                LogWriter.Write($"Novelty query failed: {e.Message}", LogWriter.LogEventType.Error);
                throw;
            }
        }

        public static async Task<bool> VerifyOriginalDataIntegrityAsync(string instanceGuid, string serialNumber, string dataGuid, string encryptedDataHash, string originalDataHash)
        {
            string jsonMsg = $@"{{
                ""op"": ""recording_verify"",
                ""meta"": {{
                    ""instance"": ""{instanceGuid}"",
                    ""serialnumber"": ""{serialNumber}""
                }},
                ""data"": {{
                    ""uuid"": ""{dataGuid}"",
                    ""hash1"": ""{encryptedDataHash}"",
                    ""hash2"": ""{originalDataHash}""
                }}
            }}";

            Console.WriteLine($"Conn handler: Sending data integrity check: {jsonMsg}");

            try
            {
                await SendDataAsync(jsonMsg);
                string serverResponse = await ReceiveDataAsync(65536);

                if (serverResponse.Contains("error"))
                {
                    LogWriter.Write("Data package hash sums do not match. Restarting the sending task...", LogWriter.LogEventType.Error);
                    return false;
                }

                LogWriter.Write("Data package hash sums match! The package has been successfully sent!", LogWriter.LogEventType.Event);
                return true;
            }
            catch (Exception e)
            {
                LogWriter.Write($"Data integrity check failed: {e.Message}", LogWriter.LogEventType.Error);
                throw;
            }
        }

        public static async Task SendDataAsChunksAsync(byte[] dataToSend, CryptographyHandler cryptoHandler, string instanceGuid, string serialNumber, string dataGuid)
        {
            uint chunkSize = 65536; // 64 KB per chunk
            ChunkGenerator gen = new ChunkGenerator();
            gen.InitChunking(dataToSend, chunkSize);

            LogWriter.Write("Start chunking", LogWriter.LogEventType.Event);

            int totalBytes = dataToSend.Length;
            uint chunkCount = 0;
            byte[] chunk = gen.GetNextChunk();

            try
            {
                while (chunk != null)
                {
                    Console.WriteLine($"First bytes of chunk: {Program.ConvertDataToString(chunk, false)}");

                    await SendSingleChunkAsync(
                        chunk,
                        instanceGuid,
                        serialNumber,
                        dataGuid,
                        chunkSize * chunkCount,
                        chunk.Length,
                        totalBytes,
                        Program.ConvertDataToString(cryptoHandler.CalculateHashSum(chunk), false));

                    if (await ChunkSendingSuccessfulAsync())
                    {
                        chunkCount++;
                        chunk = gen.GetNextChunk();

                        int percentageSent = (int)Math.Round((chunkCount * chunkSize / (double)totalBytes) * 100);
                        Console.WriteLine($"Progress: {percentageSent}% ({chunkCount * chunkSize}/{totalBytes} bytes)");
                    }
                    else
                    {
                        LogWriter.Write("Server received a flawed chunk! Retrying...", LogWriter.LogEventType.Error);
                    }
                }
            }
            catch (Exception e)
            {
                if (e is WebSocketException || e is InvalidOperationException)
                {
                    LogWriter.Write($"Chunk sending loop could not finish: {e.Message}", LogWriter.LogEventType.Error);
                    CancelCurrentSendingTask();
                    ResetCancellationTokenSources();
                    throw;
                }

                LogWriter.Write($"Unexpected exception in chunk sending: {e.Message}", LogWriter.LogEventType.Error);
            }
        }

        public static async Task SendSingleChunkAsync(byte[] chunk, string instanceGuid, string serialNumber, string dataGuid, uint chunkOffset, int chunkLength, int totalDataLength, string chunkHash)
        {
            string base64EncodedChunk = Convert.ToBase64String(chunk); // Encode chunk as Base64

            string jsonMsg = $@"{{
            ""op"": ""recording_send_chunk"",
            ""meta"": {{
                ""instance"": ""{instanceGuid}"",
                ""serialnumber"": ""{serialNumber}""
            }},
            ""data"": {{
                ""uuid"": ""{dataGuid}"",
                ""offset"": {chunkOffset},
                ""length"": {chunkLength},
                ""total size"": {totalDataLength},
                ""payload"": ""{base64EncodedChunk}"",
                ""hash"": ""{chunkHash}""
            }}
        }}";

            string preview = jsonMsg.Length > 100 ? jsonMsg.Substring(0, 100) : jsonMsg;
            Console.WriteLine($"Conn handler: Sending chunk - {preview}... (truncated)");

            try
            {
                await SendDataAsync(jsonMsg);
            }
            catch (Exception e)
            {
                LogWriter.Write($"Failed to send chunk: {e.Message}", LogWriter.LogEventType.Error);
                throw;
            }
        }

        public static async Task<bool> ChunkSendingSuccessfulAsync()
        {
            string serverResponse = await ReceiveDataAsync(65536);

            try
            {
                string status = JsonHandler.GetTokenValue(serverResponse, "status");
                return status != "error";
            }
            catch (Exception e)
            {
                LogWriter.Write($"Failed to parse chunk response: {e.Message}", LogWriter.LogEventType.Error);
                return false;
            }
        }

        public static async Task SendDataAsync(string myJsonMessage)
        {
            CancellationToken sendingCancelToken = sendingCanceller.Token;

            if (myJsonMessage.Length < 1000)
                LogWriter.Write("Sending the following message: " + myJsonMessage, LogWriter.LogEventType.Event, true);
            else
                LogWriter.Write(myJsonMessage.Substring(0, 1000), LogWriter.LogEventType.Event);

            LogWriter.Write("Websocket state before sending data asynchronously: " + ws.State, LogWriter.LogEventType.Event);

            ArraySegment<byte> bytesToSendSegment = new ArraySegment<byte>(Encoding.ASCII.GetBytes(myJsonMessage));
            try
            {
                await ws.SendAsync(bytesToSendSegment, WebSocketMessageType.Text, true, sendingCancelToken);
            }
            catch (WebSocketException e)
            {
                LogWriter.Write("Sending operation failed due to connection issues: " + e.Message, LogWriter.LogEventType.Error);
            }
            catch (OperationCanceledException)
            {
                LogWriter.Write("Sending operation canceled", LogWriter.LogEventType.Event);
            }

            LogWriter.Write("Websocket state after sending data asynchronously: " + ws.State, LogWriter.LogEventType.Event);
        }

        public static void CancelCurrentSendingTask()
        {
            Console.WriteLine("Sending cancel requested");
            sendingCanceller.Cancel();
        }

        public static void CancelCurrentReceptionTask()
        {
            Console.WriteLine("Reception cancel requested");
            receptionCanceller.Cancel();
        }

        private static void DisposeCancellationTokenSources()
        {
            sendingCanceller.Dispose();
            receptionCanceller.Dispose();
        }

        private static void ResetCancellationTokenSources()
        {
            DisposeCancellationTokenSources();
            sendingCanceller = new CancellationTokenSource();
            receptionCanceller = new CancellationTokenSource();
        }

        public static async Task<string> ReceiveDataAsync(uint buffersize)
        {
            CancellationToken receptionCancelToken = receptionCanceller.Token;
            ArraySegment<byte> bytesToReceiveSegment = new ArraySegment<byte>(new byte[buffersize]);
            WebSocketReceiveResult wsResult;
            string receivedMessage = null;

            try
            {
                while (true)
                {
                    LogWriter.Write("Websocket state before receiving data asynchronously: " + ws.State, LogWriter.LogEventType.Event);

                    using (MemoryStream ms = new MemoryStream())
                    {
                        do
                        {
                            wsResult = await ws.ReceiveAsync(bytesToReceiveSegment, receptionCancelToken);
                            ms.Write(bytesToReceiveSegment.Array, bytesToReceiveSegment.Offset, wsResult.Count);
                        }
                        while (!wsResult.EndOfMessage);

                        ms.Seek(0, SeekOrigin.Begin);

                        if (wsResult.MessageType == WebSocketMessageType.Text)
                        {
                            using (StreamReader reader = new StreamReader(ms, Encoding.UTF8))
                            {
                                receivedMessage = reader.ReadToEnd();
                                if (receivedMessage.Length < 1000)
                                    LogWriter.Write("Received the following message after construction: " + receivedMessage, LogWriter.LogEventType.Event, true);
                                else
                                    LogWriter.Write(receivedMessage.Substring(0, 1000), LogWriter.LogEventType.Event);
                            }
                        }
                    }

                    LogWriter.Write("Websocket state after receiving data asynchronously: " + ws.State, LogWriter.LogEventType.Event);

                    if (!string.IsNullOrEmpty(receivedMessage))
                        break;
                }

                return receivedMessage;
            }
            catch (Exception e)
            {
                Console.WriteLine("ReceiveAsync: " + e.Message);
                if (e is OperationCanceledException)
                {
                    LogWriter.Write("Reception operation has been canceled: " + e.Message, LogWriter.LogEventType.Event);
                }
                else if (e is WebSocketException)
                {
                    LogWriter.Write("Reception operation failed due to connection issues: " + e.Message, LogWriter.LogEventType.Error);
                }
                else if (e is InvalidOperationException)
                {
                    LogWriter.Write("Reception operation failed due to invalid operation: " + e.Message, LogWriter.LogEventType.Error);
                }
                else
                {
                    LogWriter.Write("Exception is something else (should not happen)", LogWriter.LogEventType.Error);
                }
                throw;
            }
        }
    }
}
