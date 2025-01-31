using System;
using System.Text;
using System.Security.Cryptography;
using System.IO;

namespace NativeService
{
    class CryptographyHandler : IDisposable
    {
        private readonly Aes myAes;
        private readonly ICryptoTransform encryptor;
        private readonly ICryptoTransform decryptor;

        public byte[] Key => myAes.Key; // Expose AES Key
        public byte[] IV => myAes.IV;   // Expose AES IV

        public CryptographyHandler()
        {
            myAes = Aes.Create();
            myAes.Padding = PaddingMode.PKCS7;

            encryptor = myAes.CreateEncryptor();
            decryptor = myAes.CreateDecryptor();

            Console.WriteLine($"AES Key: {BitConverter.ToString(myAes.Key)}");
            Console.WriteLine($"AES IV: {BitConverter.ToString(myAes.IV)}");
        }

        public byte[] Encrypt(byte[] dataToEncrypt)
        {
            using (MemoryStream msEncrypt = new MemoryStream())
            {
                using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                {
                    csEncrypt.Write(dataToEncrypt, 0, dataToEncrypt.Length);
                    csEncrypt.FlushFinalBlock(); // Ensure complete encryption
                }
                return msEncrypt.ToArray();
            }
        }

        public byte[] Decrypt(byte[] dataToDecrypt)
        {
            using (MemoryStream msDecrypt = new MemoryStream(dataToDecrypt))
            {
                using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                {
                    using (MemoryStream msOutput = new MemoryStream())
                    {
                        csDecrypt.CopyTo(msOutput);
                        return msOutput.ToArray();
                    }
                }
            }
        }

        public byte[] CalculateHashSum(byte[] data)
        {
            using (SHA256 sha256 = SHA256.Create()) // Use SHA256 instead of SHA1
            {
                return sha256.ComputeHash(data);
            }
        }

        public byte[] CalculateHashSum(Stream dataStream)
        {
            using (SHA256 sha256 = SHA256.Create()) // Use SHA256
            {
                return sha256.ComputeHash(dataStream);
            }
        }

        public void Dispose()
        {
            encryptor?.Dispose();
            decryptor?.Dispose();
            myAes?.Dispose();
        }
    }
}
