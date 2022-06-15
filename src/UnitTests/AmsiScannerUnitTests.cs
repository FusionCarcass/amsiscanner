using AmsiScanner;
using AmsiScanner.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using UnitTests.Utilities;

namespace UnitTests {
    [TestClass]
    public class AmsiScannerUnitTests {
        [TestMethod]
        public void Encrypt() {
            using (TemporaryFile src = new TemporaryFile()) {
                using (TemporaryFile dst = new TemporaryFile()) {
                    Program.HandleEncrypt(src.FileInfo, dst.FileInfo.FullName);
                    Assert.IsTrue(dst.FileInfo.Exists);
                    Assert.IsTrue(dst.FileInfo.Length >= src.FileInfo.Length);
                    Assert.IsFalse(src.CompareTo(dst) == 0);
                    byte[] decrypted = Utility.Decrypt(dst.CurrentContents);
                    Assert.AreEqual(decrypted.Length, src.FileInfo.Length);
                    Assert.IsTrue(src.OriginalContents.SequenceEqual(decrypted));
                }
            }
        }

        [TestMethod]
        public void Decrypt() {
            using (TemporaryFile src = new TemporaryFile()) {
                using (TemporaryFile dst = new TemporaryFile()) {
                    byte[] plaintext = Generate.ByteArray();
                    byte[] encrypted = Utility.Encrypt(plaintext);
                    File.WriteAllBytes(src.FileInfo.FullName, encrypted);
                    Program.HandleDecrypt(src.FileInfo, dst.FileInfo.FullName);
                    Assert.IsTrue(dst.CurrentContents.SequenceEqual(plaintext));
                }
            }
        }

        [TestMethod]
        public void Scan() {
            Program.HandleScanCommand(new FileInfo(TestPaths.Sample0));
        }

        [TestMethod]
        public void SigfindByChar() {
            Program.HandleSigFindPerChar(new FileInfo(TestPaths.Sample2));
        }

        [TestMethod]
        public void SigfindByToken() {
            Program.HandleSigFindPerToken(new FileInfo(TestPaths.Sample2));
        }
    }
}
