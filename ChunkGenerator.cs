using System;

namespace NativeService
{
    class ChunkGenerator
    {
        private uint dataPointer; // Tracks the current position in the source data
        private byte[] sourceData;
        private uint chunkSize;

        public ChunkGenerator() { }

        public void InitChunking(byte[] sourceData, uint chunkSize) // Initializes chunking with the given data
        {
            dataPointer = 0;
            this.sourceData = sourceData;
            this.chunkSize = chunkSize;
        }

        public byte[] GetNextChunk() // Returns the next chunk or null if all data is processed
        {
            if (dataPointer < sourceData.Length)
            {
                uint currentChunkSize = (uint)Math.Min(chunkSize, sourceData.Length - dataPointer);

                // Copy chunkSize bytes or the remaining data
                byte[] currentDataChunk = new byte[currentChunkSize];
                Array.Copy(sourceData, dataPointer, currentDataChunk, 0, currentChunkSize);

                dataPointer += currentChunkSize; // Ensure pointer moves correctly for last chunk

                return currentDataChunk;
            }
            return null;
        }
    }
}
