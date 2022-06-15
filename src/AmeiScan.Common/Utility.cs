﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace AmsiScanner.Common {
    public static class Utility {
        public const string DEFAULT_PASSWORD = "5e16e53245c147a8acd1b3e38de0135d";

        public static byte[] SALT;

        public readonly static byte[] MARKER;

        public static int ConsoleWidth {
            get {
                if (Console.IsOutputRedirected) {
                    return 256;
                } else {
                    return Console.WindowWidth;
                }
            }
        }

        static Utility() {
            Utility.SALT = new byte[] { 251, 51, 164, 251, 59, 131, 182, 228 };
            Utility.MARKER = new byte[] { 89, 37, 32, 212, 143, 199, 216, 38, 176, 236, 164, 32, 184, 202, 182 };
        }

        public static byte[] Decrypt(byte[] ciphertextbytes, string password = Utility.DEFAULT_PASSWORD) {
            byte[] numArray;
            Rfc2898DeriveBytes rfc2898DeriveByte = new Rfc2898DeriveBytes(password, Utility.SALT);
            using (AesCryptoServiceProvider aesCryptoServiceProvider = new AesCryptoServiceProvider()) {
                aesCryptoServiceProvider.KeySize = 256;
                aesCryptoServiceProvider.BlockSize = 128;
                aesCryptoServiceProvider.Key = rfc2898DeriveByte.GetBytes(aesCryptoServiceProvider.KeySize / 8);
                byte[] numArray1 = new byte[16];
                Array.Copy(ciphertextbytes, numArray1, (int)numArray1.Length);
                aesCryptoServiceProvider.IV = numArray1;
                ICryptoTransform cryptoTransform = aesCryptoServiceProvider.CreateDecryptor(aesCryptoServiceProvider.Key, aesCryptoServiceProvider.IV);
                using (MemoryStream memoryStream = new MemoryStream()) {
                    using (MemoryStream memoryStream1 = new MemoryStream(ciphertextbytes)) {
                        byte[] numArray2 = new byte[4096];
                        memoryStream1.Read(numArray2, 0, 16);
                        using (CryptoStream cryptoStream = new CryptoStream(memoryStream1, cryptoTransform, CryptoStreamMode.Read)) {
                            int num = 0;
                            while (true) {
                                int num1 = cryptoStream.Read(numArray2, 0, (int)numArray2.Length);
                                num = num1;
                                if (num1 <= 0) {
                                    break;
                                }
                                memoryStream.Write(numArray2, 0, num);
                            }
                        }
                        byte[] array = memoryStream.ToArray();
                        byte[] numArray3 = new byte[(int)Utility.MARKER.Length];
                        Array.Copy((Array)array, numArray3, (int)numArray3.Length);
                        if (!numArray3.SequenceEqual<byte>(Utility.MARKER)) {
                            throw new Exception("File is not using a supported encryption format.");
                        }
                        byte[] numArray4 = new byte[(int)array.Length - (int)numArray3.Length];
                        Array.Copy((Array)array, (int)numArray3.Length, numArray4, 0, (int)numArray4.Length);
                        numArray = numArray4;
                    }
                }
            }
            return numArray;
        }

        public static byte[] Encrypt(byte[] plaintextbytes, string password = Utility.DEFAULT_PASSWORD) {
            byte[] array;
            Rfc2898DeriveBytes rfc2898DeriveByte = new Rfc2898DeriveBytes(password, Utility.SALT);
            using (AesCryptoServiceProvider aesCryptoServiceProvider = new AesCryptoServiceProvider()) {
                aesCryptoServiceProvider.KeySize = 256;
                aesCryptoServiceProvider.BlockSize = 128;
                aesCryptoServiceProvider.Key = rfc2898DeriveByte.GetBytes(aesCryptoServiceProvider.KeySize / 8);
                aesCryptoServiceProvider.GenerateIV();
                ICryptoTransform cryptoTransform = aesCryptoServiceProvider.CreateEncryptor(aesCryptoServiceProvider.Key, aesCryptoServiceProvider.IV);
                using (MemoryStream memoryStream = new MemoryStream()) {
                    memoryStream.Write(aesCryptoServiceProvider.IV, 0, (int)aesCryptoServiceProvider.IV.Length);
                    using (CryptoStream cryptoStream = new CryptoStream(memoryStream, cryptoTransform, CryptoStreamMode.Write)) {
                        cryptoStream.Write(Utility.MARKER, 0, (int)Utility.MARKER.Length);
                        cryptoStream.Write(plaintextbytes, 0, (int)plaintextbytes.Length);
                        cryptoStream.FlushFinalBlock();
                    }
                    array = memoryStream.ToArray();
                }
            }
            return array;
        }

        public static string EncryptBase64(byte[] plaintextbytes, string password = Utility.DEFAULT_PASSWORD) {
            return Convert.ToBase64String(Utility.Encrypt(plaintextbytes, password));
        }

        public static string EncryptBase64(string plaintext, string password = Utility.DEFAULT_PASSWORD) {
            return Utility.EncryptBase64(Encoding.UTF8.GetBytes(plaintext), password);
        }

        public static string DecryptBase64(string base64, string password = Utility.DEFAULT_PASSWORD) {
            return Encoding.UTF8.GetString(Utility.Decrypt(Convert.FromBase64String(base64), password));
        }

        public static string ReadAllText(string path) {
            path = Path.GetFullPath(path);
            if (!File.Exists(path)) {
                throw new FileNotFoundException(path);
            }
            string str = null;
            if (Utility.TryReadZipFile(path, out str)) {
                return str;
            }
            if (Utility.TryReadEncryptedFile(path, out str)) {
                return str;
            }
            return File.ReadAllText(path);
        }

        public static bool TryReadEncryptedFile(string path, out string text, string password = Utility.DEFAULT_PASSWORD) {
            try {
                using (MemoryStream memoryStream = new MemoryStream(Utility.Decrypt(File.ReadAllBytes(path), password))) {
                    using (StreamReader streamReader = new StreamReader(memoryStream, true)) {
                        text = streamReader.ReadToEnd();
                        return true;
                    }
                }
            } catch {
                text = null;
                return false;
            }
        }

        public static bool TryReadZipFile(string path, out string text) {
            try {
                ZipArchive zipArchive = ZipFile.OpenRead(path);

                if (zipArchive.Entries.Count > 1) {
                    text = null;
                    return false;
                }

                using (Stream stream = zipArchive.Entries[0].Open()) {
                    using (StreamReader streamReader = new StreamReader(stream)) {
                        text = streamReader.ReadToEnd();
                        return true;
                    }
                }
            } catch {
                text = null;
                return false;
            }
        }

        public static byte[] ReadAllBytes(string path) {
            path = Path.GetFullPath(path);
            if (!File.Exists(path)) {
                throw new FileNotFoundException(path);
            }
            
            byte[] unpacked = null;
            if (Utility.TryReadZipFileBytes(path, out unpacked)) {
                return unpacked;
            }

            byte[] contents = File.ReadAllBytes(path);
            if (Utility.TryReadEncryptedFileBytes(contents, out unpacked)) {
                return unpacked;
            }

            return contents;
        }

        public static bool TryReadEncryptedFileBytes(byte[] data, out byte[] unpacked) {
            try {
                using (MemoryStream memoryStream = new MemoryStream(Utility.Decrypt(data, Utility.DEFAULT_PASSWORD))) {
                    unpacked = memoryStream.ToArray();
                }
                return true;
            } catch {
                unpacked = null;
                return false;
            }
        }

        public static bool TryReadZipFileBytes(string path, out byte[] unpacked) {
            try {
                ZipArchive zipArchive = ZipFile.OpenRead(path);

                //Don't currently support more than one file in the zip file
                if (zipArchive.Entries.Count > 1) {
                    unpacked = null;
                    return false;
                }

                using (Stream stream = zipArchive.Entries[0].Open()) {
                    using (MemoryStream ms = new MemoryStream()) {
                        stream.CopyTo(ms);
                        unpacked = ms.ToArray();
                        return true;
                    }
                }

            } catch {
                unpacked = null;
                return false;
            }
        }

        public static bool TryGetText(byte[] data, out string text) {
            using (MemoryStream ms = new MemoryStream(data)) {
                using (StreamReader stream = new StreamReader(ms)) {
                    string temp = stream.ReadToEnd();
                    for (int i = 0; i < Math.Min(512, temp.Length); i++) {
                        if (Utility.IsControlCharacter(temp[i])) {
                            text = default(String);
                            return false;
                        }
                    }
                    text = temp;
                    return true;
                }
            }
        }

        private static bool IsControlCharacter(char c) {
            return char.IsControl(c) && c != '\r' && c != '\n' && c != '\t';
        }
    }
}