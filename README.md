# C# Background Service Samples

This repository includes sample C# classes for a **background service** that listens for the connection of a Nox sleep diagnostic machine via USB. The Nox device records data for sleep apnea patients, and this service is responsible for detecting the device, checking for new data, and securely transferring it to a server.

## Features

- **Device Monitoring**: Detects when a Nox device is connected to the system via USB. The device is mounted at a specific drive location (check the code for details).
- **WebSocket Communication**: Establishes a WebSocket connection with a server for real-time data transfer.
- **Data Novelty Check**: Compares the hash of the data on the device with the hash stored on the server to determine if new data is available.
- **Data Chunking and Security**:
  - Splits new data into chunks (maximum 64 KB per chunk).
  - Compresses data using zlib (check implementation to confirm if compression occurs before or after encryption).
  - Encrypts each chunk using AES block cipher.
  - Transfers encrypted data chunks securely to the server.
- **Logging**: Logs events and operations to rotating log files.

Note that some of these features are not implemented in the sample classes.

## Project Status

This project was initially intended for commercial use but was later cancelled, so I'm now displaying it as a demo project.
