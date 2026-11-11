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
using SevenZipExtractor;

public class FileUtils
{
    public static string GuessFileFormat(byte[] signature)
    {
        var formatSignatures = new Dictionary<string, string>
        {
            { "504B0304", "zip" },
            { "504B0506", "zip-empty" },
            { "504B0708", "zip-span" },
            { "526172211A0700", "rar" },
            { "526172211A070100", "rar5" },
            { "377ABCAF271C", "7z" },
            { "4D534346", "cab" },
            { "49536328", "cab-installshield" },
            { "5A4F4F", "zoo" },
            { "4D5357494D000000D000000000", "wim" },
            { "636F6E6E6563746978", "vhd" },
            { "7668647866696C65", "vhdx" },
            { "D0CF11E0A1B11AE1", "msi" },
            { "4B444D", "wmdk" },
            { "23204469736B2044657363726970746F", "vmdk-split"},
            { "3C3C3C204F7261636C6520564D205669727475616C426F78204469736B20496D616765203E3E3E", "vdi" }, // 39 bytes
            { "4344303031", "iso" },
            { "25504446", "arc" },
            { "60EA", "arj" },
            { "78617221", "xar"},
            { "EDABEEDB", "rpm" },
            { "213C617263683E0A", "deb" },
            { "6B6F6C79","dmg" },
            { "53514C69746520666F726D6174203300", "sqlite3" },
            { "1F9D", "z-lzw" },
            { "1FA0", "z-lzh" },
            { "2D686C302D", "lzh0" },
            { "2D686C352D", "lzh5" },
            { "425A68", "bz2" },
            { "4C5A4950", "lz" },
            { "303730373037", "cpio" },
            { "1F8B","gz" },
            { "FD377A585A00","xz" },
            { "7573746172003030", "tar"},
            { "7573746172202000", "tar"},
            { "49545346", "chm" },
            { "7F454C46", "elf" }
            // Add more signature-format pairs as needed
        };

        string signatureHex = BitConverter.ToString(signature).Replace("-", "").ToUpper();
        foreach (var kvp in formatSignatures)
        {
            if (signatureHex.StartsWith(kvp.Key))
            {
                return kvp.Value;
            }
        }
        return "unknown";
    }

    public static bool IsLinux()
    {
        return System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
    }
    
    public static string GetSevenZipLibs(){
        string LibPath = string.Empty;
        if (IsLinux())
        {
            // Console.WriteLine("We are in linux");
            LibPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Libs", "linux", "7z.so");
        }
        else
        {
            // Console.WriteLine("This is WIndows");
            if (Environment.Is64BitProcess)
                LibPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Libs", "64bit", "7z.dll");
            else
                LibPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Libs", "32bit", "7z.dll");
        }
        return LibPath;
    }
    public static int GetSevenZipFormat(Stream inStream){
        byte[] signatureBytes = new byte[40];
        int bytesRead = inStream.Read(signatureBytes, 0, signatureBytes.Length);
        int szFormat = 0;
        // Compare the signature bytes with known signatures for different formats
        string fileFormat = GuessFileFormat(signatureBytes);
        Console.WriteLine(fileFormat);
        switch(fileFormat){
            case "zip-empty":
            case "zip-span":
            case "zip": {
                szFormat = (int)SevenZipFormat.Zip;
                break;
            }
            // case "rar": {
            //     szFormat = SevenZipExtractor.SevenZipFormat.Rar;
            //     break;
            // }
            // case "rar5": {
            //     szFormat = SevenZipExtractor.SevenZipFormat.Rar5;
            //     break;
            // }
            // case "7z": {
            //     szFormat = SevenZipExtractor.SevenZipFormat.SevenZip;
            //     break;
            // }
            // case "cab": {
            //     szFormat = SevenZipExtractor.SevenZipFormat.Cab;
            //     break;
            // }
            // case "wim": {
            //     szFormat = SevenZipExtractor.SevenZipFormat.Wim;
            //     break;
            // }
            // case "vhd": {
            //     szFormat = SevenZipExtractor.SevenZipFormat.Vhd;
            //     break;
            // }
            // case "msi": {
            //     szFormat = SevenZipExtractor.SevenZipFormat.Msi;
            //     break;
            // }
            // case "iso": {
            //     szFormat = SevenZipExtractor.SevenZipFormat.Iso;
            //     break;
            // }
            // case "chm": {
            //     szFormat = SevenZipExtractor.SevenZipFormat.Chm;
            //     break;
            // }
            // case "arj": {
            //     szFormat = SevenZipExtractor.SevenZipFormat.Arj;
            //     break;
            // }
            // case "xar": {
            //     szFormat = SevenZipExtractor.SevenZipFormat.Xar;
            //     break;
            // }
            // case "rpm": {
            //     szFormat = SevenZipExtractor.SevenZipFormat.Rpm;
            //     break;
            // }
            // case "deb": {
            //     szFormat = SevenZipExtractor.SevenZipFormat.Deb;
            //     break;
            // }
            // case "dmg": {
            //     szFormat = SevenZipExtractor.SevenZipFormat.Dmg;
            //     break;
            // }
            // case "tar": {
            //     szFormat = SevenZipExtractor.SevenZipFormat.Tar;
            //     break;
            // }
            // case "cpio": {
            //     szFormat = SevenZipExtractor.SevenZipFormat.Cpio;
            //     break;
            // }
            // case "xz": {
            //     szFormat = SevenZipExtractor.SevenZipFormat.XZ;
            //     break;
            // }
            // case "bz2": {
            //     szFormat = SevenZipExtractor.SevenZipFormat.BZip2;
            //     break;
            // }
            // case "elf": {
            //     szFormat = SevenZipExtractor.SevenZipFormat.Elf;
            //     break;
            // }
        }
        return szFormat;
    }
}