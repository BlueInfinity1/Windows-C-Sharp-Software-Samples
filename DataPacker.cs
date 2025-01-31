using System.IO.Compression;
using System.IO;
using System.Collections.Generic;

namespace NativeService
{
    class DataPacker // Methods for dealing with byte[] data (compression, packaging)
    {
        public static byte[] CompressData(byte[] dataToCompress)
        {
            using (MemoryStream outputStream = new MemoryStream())
            {
                using (DeflateStream compressionStream = new DeflateStream(outputStream, CompressionLevel.Optimal, true))
                {
                    compressionStream.Write(dataToCompress, 0, dataToCompress.Length);
                    compressionStream.Flush(); 
                }
                return outputStream.ToArray();
            }
        }

        public static byte[] DecompressData(byte[] dataToDecompress)
        {
            using (MemoryStream inputStream = new MemoryStream(dataToDecompress))
            {
                using (MemoryStream outputStream = new MemoryStream())
                {
                    using (DeflateStream decompressionStream = new DeflateStream(inputStream, CompressionMode.Decompress))
                    {
                        decompressionStream.CopyTo(outputStream);
                    }
                    return outputStream.ToArray();
                }
            }
        }

        public static byte[] CompressFiles(List<FileInfo> filesToCompress)
        {
            using (MemoryStream outputStream = new MemoryStream())
            {
                using (GZipStream compressionStream = new GZipStream(outputStream, CompressionLevel.Optimal, true))
                {
                    foreach (var file in filesToCompress)
                    {
                        if (!file.Exists) continue;

                        byte[] fileBytes = File.ReadAllBytes(file.FullName);
                        compressionStream.Write(fileBytes, 0, fileBytes.Length);
                    }
                    compressionStream.Flush();
                }
                return outputStream.ToArray();
            }
        }

        public static byte[] BuildMultiFileBytePackage(List<FileInfo> files)
        {
            using (MemoryStream packageStream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(packageStream))
                {
                    foreach (var file in files)
                    {
                        if (!file.Exists) continue;

                        byte[] fileBytes = File.ReadAllBytes(file.FullName);
                        writer.Write(fileBytes.Length); // Write file size
                        writer.Write(fileBytes); // Write file data
                    }
                }
                return packageStream.ToArray();
            }
        }
    }
}
