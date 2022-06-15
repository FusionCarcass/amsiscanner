using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public AmsiSession() : this(string.Format("{0}_{1}_{2}", "PowerShell", "C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe", Environment.OSVersion.Version.ToString())) { }

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

        public AmsiResult ScanTextFile(FileInfo path) {
            string script = Utility.ReadAllText(path.FullName);
            return this.ScanString(script);
        }

        public AmsiResult ScanBinaryFile(FileInfo path) {
            byte[] data = Utility.ReadAllBytes(path.FullName);
            return this.ScanData(data);
        }

        public AmsiResult ScanString(string script) {
            if (this._cacheNotDetected.Contains(script)) {
                return AmsiResult.NotDetected;
            }

            int amsiResult = AmsiUtility.AMSI_RESULT_CLEAN;

            string str = Guid.NewGuid().ToString();
            Interlocked.Increment(ref this._amsiCalls);
            int resultCode = NativeMethods.AmsiScanString(this._context, script, str, this._session, out amsiResult);
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
                this._cacheNotDetected.Add(script);
                return AmsiResult.NotDetected;
            }
        }

        public AmsiResult ScanData(Byte[] data) {
            int amsiResult = AmsiUtility.AMSI_RESULT_CLEAN;

            string str = Guid.NewGuid().ToString();
            Interlocked.Increment(ref this._amsiCalls);
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

        private IntPtr _context = IntPtr.Zero;
        private IntPtr _session = IntPtr.Zero;
        private HashSet<string> _cacheNotDetected;
        private long _amsiCalls = 0;
    }
}
