using System;
using System.Text;
using System.Security.Cryptography;
using System.IO;

namespace NativeService
{
    class CryptographyHandler
    {
        //NOTE: This class is not currently used for encrypting, as we're doing encrypting and the packaging at the same time.
        //This class can be used for debugging though, as it's possible to do whole data encrypt/decrypt with the functions provided here
        //Hashsum functions are in use though.

        //private RSACryptoServiceProvider RSA;
        private Aes myAes;
        private ICryptoTransform encryptor;
        private ICryptoTransform decryptor;

        public CryptographyHandler()
        {
            //RSA = new RSACryptoServiceProvider();
            myAes = Aes.Create();
            //myAes.BlockSize = 128;
            //myAes.KeySize = 128;
            encryptor = myAes.CreateEncryptor(myAes.Key, myAes.IV);
            decryptor = myAes.CreateDecryptor(myAes.Key, myAes.IV);
            myAes.Padding = PaddingMode.PKCS7;
            Console.WriteLine("AES key to use: " + Program.ConvertDataToString(myAes.Key) + "\nIV: " + Program.ConvertDataToString(myAes.IV));
        }

        public byte[] Encrypt(byte[] dataToEncrypt)
        {
            byte[] encryptedData;

            // Create an encryptor to perform the stream transform
            
            // Create the streams used for encryption
            using (MemoryStream msEncrypt = new MemoryStream())
            {
                using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                {
                    //Write all data to the Crypto stream
                    csEncrypt.Write(dataToEncrypt, 0, dataToEncrypt.Length);
                    
                    encryptedData = msEncrypt.ToArray();
                }
            }

            return encryptedData;
        }

        public byte[] Decrypt(byte[] dataToDecrypt)
        {
            byte[] decryptionResult = new byte[dataToDecrypt.Length]; //This should probably be smaller...
            using (MemoryStream msDecrypt = new MemoryStream(dataToDecrypt))
            {
                using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Write))
                {
                    csDecrypt.Write(dataToDecrypt, 0, dataToDecrypt.Length);
                    decryptionResult = msDecrypt.ToArray();
                }
            }

            return decryptionResult;
        }

        /*
        public byte[] RSAEncrypt(byte[] dataToEncrypt, bool allowOAEPPadding)
        {
            //encrypt data
            byte[] encryptedData = null;
            try
            {
                RSA.ExportParameters(false); //only public key info needed
                encryptedData = RSA.Encrypt(dataToEncrypt, allowOAEPPadding);

            }
            catch (CryptographicException)
            {
                Console.WriteLine("Encrypting data failed.");
            }
            return encryptedData;
        }

        public byte[] RSADecrypt(byte[] dataToDecrypt, bool allowOAEPPadding)
        {
            //decrypt data
            byte[] decryptedData = null;
            try
            {
                RSA.ExportParameters(true);
                decryptedData = RSA.Decrypt(dataToDecrypt, allowOAEPPadding);
            }
            catch (CryptographicException)
            {
                Console.WriteLine("Decrypting data failed.");
            }
            return decryptedData;
        }
        */
        public byte[] CalculateHashSum(byte[] data)
        {
            using (SHA1 mySHA1 = new SHA1CryptoServiceProvider()) //Or SHA1Managed?
            {
                return mySHA1.ComputeHash(data);
            }
        }
        public byte[] CalculateHashSum(Stream dataStream)
        {
            using (SHA1 mySHA1 = new SHA1CryptoServiceProvider()) //Or SHA1Managed?
            {
                return mySHA1.ComputeHash(dataStream);
            }
        }
    }
}
