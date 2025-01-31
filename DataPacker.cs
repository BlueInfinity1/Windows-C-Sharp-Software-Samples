using System.IO.Compression;
using System.IO;
using System.Collections.Generic;

namespace NativeService
{
    //NOTE: This class is currently unused due to packing and encrypting work happening in the same class

    class DataPacker //methods for dealing with byte[] data, e.g. building packages and compression
    {
        public static byte[] CompressData(byte[] dataToCompress)
        {
            using (MemoryStream outputStream = new MemoryStream())
            {
                using (DeflateStream compressionStream = new DeflateStream(outputStream, CompressionLevel.Optimal))
                {
                    compressionStream.Write(dataToCompress, 0, dataToCompress.Length);
                }
                return outputStream.ToArray();
            }
        }

        public static byte[] CompressData(List<FileInfo> filesToCompress)
        {
            /*using (MemoryStream outputStream = new MemoryStream())
            {
                using (DeflateStream compressionStream = new DeflateStream(outputStream, CompressionLevel.Optimal))
                {
                    compressionStream.Write(dataToCompress, 0, dataToCompress.Length);
                }
                return outputStream.ToArray();
            }*/
            return null;
        }

        public static byte[] DecompressData(byte[] dataToDecompress)
        {
            return null; //TODO if needed
        }

        public static byte[] BuildMultiFileBytePackage(List<FileInfo> files)
        {
            //TODO: Construct a byte array by appending each file in files to the byte array
            return null;
        }
    }
}
