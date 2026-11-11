using Force.Crc32;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Utilities.Encoders;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Paddings;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;


public class CryptoUtils
{
    const int buffSize =  8192; //800000; // 2097152;

    public static string CRC32Hash(string inFile)
    {
        using (FileStream inStream = new FileStream(inFile, FileMode.Open))
        {
            Crc32Algorithm crc32 = new Crc32Algorithm();
            byte[] res = crc32.ComputeHash(inStream);
            string ccc = Hex.ToHexString(res).ToLower();
            return ccc.ToUpper();
        }
    }

    public static bool CompareCrc32(string cPacked, string cInFile)
    {
        string cUnpacked = CRC32Hash(cInFile);
        return (cPacked.ToLower().Equals(cUnpacked.ToLower()));
    }

    public async static Task<byte[]> GetByteArrayForHashing(string inFile)
    {
        using (MemoryStream ms = new MemoryStream())
        using (FileStream inStream = new FileStream(inFile, FileMode.Open, FileAccess.Read))
        {
            inStream.Seek(0, SeekOrigin.Begin);
            await inStream.CopyToAsync(ms, buffSize);
            return ms.ToArray();
        }
    }

    public static string MD5FileHash(string inFile)
    {
        using (FileStream inStream = new FileStream(inFile, FileMode.Open))
        {
            MD5Digest digest = new MD5Digest();
            inStream.Seek(0, SeekOrigin.Begin);
            byte[] input = new byte[buffSize];
            int bytesRead;
            int totBytes = 0;
            while ((bytesRead = inStream.Read(input, 0, buffSize)) != 0)
            {
                digest.BlockUpdate(input, 0, bytesRead);
                totBytes += bytesRead;
            }
            byte[] sum = new byte[digest.GetDigestSize()];
            digest.DoFinal(sum, 0);
            inStream.Dispose();

            return Hex.ToHexString(sum).ToLower();
        }
    }

    public static string Sha1FileHash(string inFile)
    {
        string result = string.Empty;
        using (FileStream inStream = new FileStream(inFile, FileMode.Open, FileAccess.Read))
        {
            Sha1Digest digest = new Sha1Digest();
            inStream.Seek(0, SeekOrigin.Begin);
            byte[] input = new byte[buffSize];
            int bytesRead;
            int totBytes = 0;
            while ((bytesRead = inStream.Read(input, 0, buffSize)) != 0)
            {
                digest.BlockUpdate(input, 0, bytesRead);
                totBytes += bytesRead;
            }
            byte[] sum = new byte[digest.GetDigestSize()];
            digest.DoFinal(sum, 0);
            inStream.Dispose();
            result = Hex.ToHexString(sum).ToLower();
        }

        return result;
    }

    public static string Sha224FileHash(string inFile)
    {
        string result = string.Empty;

        using (FileStream inStream = new FileStream(inFile, FileMode.Open, FileAccess.Read))
        {
            Sha224Digest digest = new Sha224Digest();
            inStream.Seek(0, SeekOrigin.Begin);
            byte[] input = new byte[buffSize];
            int bytesRead;
            int totBytes = 0;
            while ((bytesRead = inStream.Read(input, 0, buffSize)) != 0)
            {
                digest.BlockUpdate(input, 0, bytesRead);
                totBytes += bytesRead;
            }
            byte[] sum = new byte[digest.GetDigestSize()];
            digest.DoFinal(sum, 0);
            inStream.Dispose();
            result = Hex.ToHexString(sum).ToLower();
        }

        return result;
    }

    public static string Sha256FileHash(string inFile)
    {
        string result = string.Empty;

        using (FileStream inStream = new FileStream(inFile, FileMode.Open, FileAccess.Read))
        {
            Sha256Digest digest = new Sha256Digest();
            inStream.Seek(0, SeekOrigin.Begin);
            byte[] input = new byte[buffSize];
            int bytesRead;
            int totBytes = 0;
            while ((bytesRead = inStream.Read(input, 0, buffSize)) != 0)
            {
                digest.BlockUpdate(input, 0, bytesRead);
                totBytes += bytesRead;
            }
            byte[] sum = new byte[digest.GetDigestSize()];
            digest.DoFinal(sum, 0);
            inStream.Dispose();
            result = Hex.ToHexString(sum).ToLower();
        }

        return result;
    }

    public static string Sha384FileHash(string inFile)
    {
        string result = string.Empty;

        using (FileStream inStream = new FileStream(inFile, FileMode.Open, FileAccess.Read))
        {
            Sha384Digest digest = new Sha384Digest();
            inStream.Seek(0, SeekOrigin.Begin);
            byte[] input = new byte[buffSize];
            int bytesRead;
            int totBytes = 0;
            while ((bytesRead = inStream.Read(input, 0, buffSize)) != 0)
            {
                digest.BlockUpdate(input, 0, bytesRead);
                totBytes += bytesRead;
            }
            byte[] sum = new byte[digest.GetDigestSize()];
            digest.DoFinal(sum, 0);
            inStream.Dispose();
            result = Hex.ToHexString(sum).ToLower();
        }

        return result;
    }
    public static string Sha512FileHash(string inFile)
    {
        string result = string.Empty;

        using (FileStream inStream = new FileStream(inFile, FileMode.Open, FileAccess.Read))
        {
            Sha512Digest digest = new Sha512Digest();
            inStream.Seek(0, SeekOrigin.Begin);
            byte[] input = new byte[buffSize];
            int bytesRead;
            int totBytes = 0;
            while ((bytesRead = inStream.Read(input, 0, buffSize)) != 0)
            {
                digest.BlockUpdate(input, 0, bytesRead);
                totBytes += bytesRead;
            }
            byte[] sum = new byte[digest.GetDigestSize()];
            digest.DoFinal(sum, 0);
            inStream.Dispose();
            result = Hex.ToHexString(sum).ToLower();
        }

        return result;
    }

    public static string TigerFileHash(string inFile)
    {
        string result = string.Empty;

        using (FileStream inStream = new FileStream(inFile, FileMode.Open, FileAccess.Read))
        {
            TigerDigest digest = new TigerDigest();
            inStream.Seek(0, SeekOrigin.Begin);
            byte[] input = new byte[buffSize];
            int bytesRead;
            int totBytes = 0;
            while ((bytesRead = inStream.Read(input, 0, buffSize)) != 0)
            {
                digest.BlockUpdate(input, 0, bytesRead);
                totBytes += bytesRead;
            }
            byte[] sum = new byte[digest.GetDigestSize()];
            digest.DoFinal(sum, 0);
            inStream.Dispose();
            result = Hex.ToHexString(sum).ToLower();
        }

        return result;
    }

    public static string WhirlpoolFileHash(string inFile)
    {
        string result = string.Empty;

        using (FileStream inStream = new FileStream(inFile, FileMode.Open, FileAccess.Read))
        {
            WhirlpoolDigest digest = new WhirlpoolDigest();
            inStream.Seek(0, SeekOrigin.Begin);
            byte[] input = new byte[buffSize];
            int bytesRead;
            int totBytes = 0;
            while ((bytesRead = inStream.Read(input, 0, buffSize)) != 0)
            {
                digest.BlockUpdate(input, 0, bytesRead);
                totBytes += bytesRead;
            }
            byte[] sum = new byte[digest.GetDigestSize()];
            digest.DoFinal(sum, 0);
            inStream.Dispose();
            result = Hex.ToHexString(sum).ToLower();
        }

        return result;
    }

    public static string Sha3FileHash(string inFile, int aSize)
    {
        string result = string.Empty;

        using (FileStream inStream = new FileStream(inFile, FileMode.Open, FileAccess.Read))
        {
            Sha3Digest digest = new Sha3Digest(aSize);
            inStream.Seek(0, SeekOrigin.Begin);
            byte[] input = new byte[buffSize];
            int bytesRead;
            int totBytes = 0;
            while ((bytesRead = inStream.Read(input, 0, buffSize)) != 0)
            {
                digest.BlockUpdate(input, 0, bytesRead);
                totBytes += bytesRead;
            }
            byte[] sum = new byte[digest.GetDigestSize()];
            digest.DoFinal(sum, 0);
            inStream.Dispose();
            result = Hex.ToHexString(sum).ToLower();
        }

        return result;
    }

    public static string RipeMDFileHash(string inFile, int aSize)
    {
        string result = string.Empty;

        using (FileStream inStream = new FileStream(inFile, FileMode.Open, FileAccess.Read))
        {
            GeneralDigest digest = null;
            switch (aSize)
            {
                case 128:
                    {
                        digest = new RipeMD128Digest();
                        break;
                    }
                case 160:
                    {
                        digest = new RipeMD160Digest();
                        break;
                    }
                case 256:
                    {
                        digest = new RipeMD256Digest();
                        break;
                    }
                case 320:
                    {
                        digest = new RipeMD320Digest();
                        break;
                    }
            }
            inStream.Seek(0, SeekOrigin.Begin);
            byte[] input = new byte[buffSize];
            int bytesRead;
            int totBytes = 0;
            while ((bytesRead = inStream.Read(input, 0, buffSize)) != 0)
            {
                digest.BlockUpdate(input, 0, bytesRead);
                totBytes += bytesRead;
            }
            byte[] sum = new byte[digest.GetDigestSize()];
            digest.DoFinal(sum, 0);
            inStream.Dispose();
            result = Hex.ToHexString(sum).ToLower();
        }

        return result;
    }


    #region String Encryption/Decryption

    /* public async static Task<string> GetHashedPasswordForDb()
    {
        string aAccountName = await UserUtils.GetUserSystemAccountName();
        string aHashAccount = UserUtils.GetShake256HaskForString(aAccountName);
        return aHashAccount;
    }

    public async static Task<string> EncryptString(string plainInput)
    {
        string aAccountName = await UserUtils.GetUserSystemAccountName();
        string aHashAccount = UserUtils.GetShake256HaskForString(aAccountName);

        byte[] iv = new byte[16];
        byte[] array;
        using (Aes aes = Aes.Create())
        {
            aes.Key = Encoding.UTF8.GetBytes(aHashAccount);
            aes.IV = iv;
            ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (CryptoStream cryptoStream = new CryptoStream((Stream)memoryStream, encryptor, CryptoStreamMode.Write))
                {
                    using (StreamWriter streamWriter = new StreamWriter((Stream)cryptoStream))
                    {
                        streamWriter.Write(plainInput);
                    }

                    array = memoryStream.ToArray();
                }
            }
        }
        return Convert.ToBase64String(array);
    }

    public async static Task<string> DecryptString(string cipherText)
    {
        string aAccountName = await UserUtils.GetUserSystemAccountName();
        string aHashAccount = UserUtils.GetShake256HaskForString(aAccountName);

        byte[] iv = new byte[16];
        byte[] buffer = Convert.FromBase64String(cipherText);
        using (Aes aes = Aes.Create())
        {
            aes.Key = Encoding.UTF8.GetBytes(aHashAccount);
            aes.IV = iv;
            ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using (MemoryStream memoryStream = new MemoryStream(buffer))
            {
                using (CryptoStream cryptoStream = new CryptoStream((Stream)memoryStream, decryptor, CryptoStreamMode.Read))
                {
                    using (StreamReader streamReader = new StreamReader((Stream)cryptoStream))
                    {
                        return streamReader.ReadToEnd();
                    }
                }
            }
        }
    }
*/


    #endregion

    /* public async static Task<string> GetHardwareId(bool Base64Hash)
    {

        var token = HardwareIdentification.GetPackageSpecificToken(null);
        var hardwareId = token.Id;
        var dataReader = Windows.Storage.Streams.DataReader.FromBuffer(hardwareId);

        var array = new byte[hardwareId.Length];
        dataReader.ReadBytes(array);

        var sb = new StringBuilder();

        for (var i = 0; i < array.Length; i++)
            sb.Append(array[i].ToString());
        if (!Base64Hash)
            return sb.ToString();
        else
            return Convert.ToBase64String(Encoding.ASCII.GetBytes(sb.ToString()));
    } */
}

