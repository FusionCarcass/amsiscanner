using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AmsiScanner.Common {
    public class AmsiSession : IDisposable {
        public long AmsiCallCount {
            get {
                return Interlocked.Read(ref this._amsiCalls);
            }
        }

        public AmsiSession() : this(string.Format("{0}_{1}_{2}", "PowerShell", "C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe", Utility.MainModule.FileVersionInfo.ProductVersion)) { }

        public AmsiSession(string application, bool correlated = false, bool cache = true) {
            int resultCode = NativeMethods.AmsiInitialize(application, out this._context);
            if (resultCode != 0) {
                throw new Exception(string.Format("Call to AmsiInitialize failed with return code {0}.", resultCode));
            }

            if (correlated) {
                resultCode = NativeMethods.AmsiOpenSession(this._context, out this._session);
                if (resultCode != 0) {
                    this.Dispose();
                    throw new Exception(string.Format("Call to AmsiOpenSession failed with return code {0}.", resultCode));
                }
            }

            if (cache) {
                this._cacheNotDetected = new HashSet<string>();
            }
        }

        public AmsiResult ScanFile(FileInfo path) {
            byte[] contents = Utility.ReadAllBytes(path.FullName);
            string text;
            if (Utility.TryGetText(contents, out text)) {
                return this.ScanString(text);
            } else {
                return this.ScanData(contents);
            }
        }

        public AmsiResult ScanTextFile(FileInfo path) {
            string script = Utility.ReadAllText(path.FullName);
            return this.ScanString(script);
        }

        public AmsiResult ScanBinaryFile(FileInfo path) {
            byte[] data = Utility.ReadAllBytes(path.FullName);
            return this.ScanData(data);
        }

        public AmsiResult ScanString(string text) {
            if (this._cacheNotDetected.Contains(text)) {
                return AmsiResult.NotDetected;
            }

            string str = Guid.NewGuid().ToString();
            Interlocked.Increment(ref this._amsiCalls);

            int amsiResult;
            int resultCode = NativeMethods.AmsiScanString(this._context, text, str, this._session, out amsiResult);
            if (resultCode != 0) {
                throw new Exception(string.Format("Call to AmsiScanString failed with return code {0}.", resultCode));
            }

            if (amsiResult >= AmsiUtility.AMSI_RESULT_DETECTED) {
                return AmsiResult.Detected; ;
            } else if (amsiResult >= AmsiUtility.AMSI_RESULT_BLOCKED_BY_ADMIN_START && amsiResult <= AmsiUtility.AMSI_RESULT_BLOCKED_BY_ADMIN_END) {
                return AmsiResult.BlockedByAdmin;
            } else if (amsiResult == AmsiUtility.AMSI_RESULT_CLEAN) {
                return AmsiResult.Clean;
            } else {
                this._cacheNotDetected.Add(text);
                return AmsiResult.NotDetected;
            }
        }

        public AmsiResult ScanData(Byte[] data) {
            string str = Guid.NewGuid().ToString();
            Interlocked.Increment(ref this._amsiCalls);
            int amsiResult;
            int resultCode = NativeMethods.AmsiScanBuffer(this._context, data, (uint)data.Length, str, this._session, out amsiResult);
            if (resultCode != 0) {
                throw new Exception(string.Format("Call to AmsiScanString failed with return code {0}.", resultCode));
            }

            if (amsiResult >= AmsiUtility.AMSI_RESULT_DETECTED) {
                return AmsiResult.Detected; ;
            } else if (amsiResult >= AmsiUtility.AMSI_RESULT_BLOCKED_BY_ADMIN_START && amsiResult <= AmsiUtility.AMSI_RESULT_BLOCKED_BY_ADMIN_END) {
                return AmsiResult.BlockedByAdmin;
            } else if (amsiResult == AmsiUtility.AMSI_RESULT_CLEAN) {
                return AmsiResult.Clean;
            } else {
                return AmsiResult.NotDetected;
            }
        }

        public static bool IsDetected(int result) {
            return result >= AmsiUtility.AMSI_RESULT_DETECTED;
        }

        public void Dispose() {
            if (this._context != IntPtr.Zero) {
                NativeMethods.AmsiUninitialize(this._context);
            }
        }

        private readonly IntPtr _context = IntPtr.Zero;
        private readonly IntPtr _session = IntPtr.Zero;
        private readonly HashSet<string> _cacheNotDetected;
        private long _amsiCalls = 0;
    }
}
