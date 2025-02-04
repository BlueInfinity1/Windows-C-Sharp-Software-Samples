![Screenshot](https://drive.google.com/uc?export=view&id=1RVQRCIcCPiRW8u6zmU67BBj1RbPIzFCT "Screenshot")

# C# Tray Application Samples

This repository includes sample C# classes for a **tray application** that listens for the connection of a Nox sleep diagnostic machine via USB. The Nox device records data for sleep apnea patients, and this service is responsible for detecting the device, checking for new data, and securely transferring it to a server.

## Features
- **Tray Application**: Runs in the **Windows system tray (notification area)**, providing a minimal interface while operating in the background.  
- **Device Monitoring**: Detects when a Nox device is connected to the system via USB by querying all USB devices attached to the computer and reading their Product Id - Vendor Id (PID-VID) values. The device is mounted at a specific drive location.
- **WebSocket Communication**: Establishes a WebSocket connection with a server for real-time data transfer.
- **Data Novelty Check**: Compares the hash of the data on the device with the hash stored on the server to determine if new data is available.
- **Data Chunking and Security**:
  - Splits new data into chunks (maximum 64 KB per chunk).
  - Compresses data using zlib.
  - Encrypts each chunk using AES block cipher.
  - Transfers encrypted data chunks securely to the server using Base64 encoding.
  - Transferred data integrity is verified by comparing hashes.
- **Logging**: Logs events and operations to rotating log files.

Note that some of these features are not implemented in the sample classes.

## Project Status

This project was initially intended for commercial use but was later cancelled, so I'm now displaying it as a demo project.

## Example Run

Here are some screenshots of the console messages during a single run of the application.

![Example Run](https://drive.google.com/uc?export=view&id=1301urQEyyqy9VzWBwzNcpAtbfTBglvh2 "Example Run")


