using AmsiScanner.Common.Indexing;
using AmsiScanner.Common.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AmsiScanner.Common {
    public class SignatureFinderByTokenSettings {
        public int MinimumDeltaStepSize {
            get { return this._minimimDeltaStepSize; }
            private set {
                if (value < 1) {
                    throw new ArgumentException("The MinimumDeltaStepSize must be greater than 1.", "MinimumDeltaStepSize");
                }

                this._minimimDeltaStepSize = value;
            }
        }

        public bool Verbose {
            get;
            private set;
        } = true;

        public string Script {
            get;
            private set;
        }

        public Token[] Tokens {
            get;
            private set;
        }

        public IndexPredictor DeltaPredictor {
            get;
            set;
        } = new PreviousPredictor(0, 1);

        public IndexPredictor NablaPredictor {
            get;
            set;
        } = new PreviousPredictor(0, 1);

        public SignatureFinderByTokenSettings(string script, int minimumDeltaStepSize = 2, bool verbose = true) {
            this.Verbose = verbose;
            this.Script = script;
            this.MinimumDeltaStepSize = minimumDeltaStepSize;
            this.Tokens = AmsiUtility.Tokenize(script);
        }

        private int _minimimDeltaStepSize = 2;
    }

    public delegate void VerboseIterationByTokenCompleteEventHandler(int iteration, int nSteps, int stepSize, string deltaString, Token[] deltaToken);
    public delegate void VerboseMinimizationByTokenCompleteEventHandler(string reducedString, Token[] reducedToken);

    public class SignatureFinderByToken : BackgroundWorker {
        public event VerboseIterationByTokenCompleteEventHandler VerboseIterationComplete;
        public event VerboseMinimizationByTokenCompleteEventHandler VerboseMinimizationComplete;

        public string ReducedString {
            get {
                lock (this._deltaLock) {
                    return this._deltaString;
                }
            }

            private set {
                lock (this._deltaLock) {
                    this._deltaString = value;
                }
            }
        }

        public Token[] ReducedTokens {
            get {
                lock (this._deltaLock) {
                    return this._deltaTokens;
                }
            }

            private set {
                lock (this._deltaLock) {
                    this._deltaTokens = value;
                }
            }
        }

        public long AmsiCallCount {
            get {
                return Interlocked.Read(ref this._amsiCallCount);
            }
        }

        public SignatureFinderByToken(string script) {
            this._settings = new SignatureFinderByTokenSettings(script);
        }

        public SignatureFinderByToken(SignatureFinderByTokenSettings settings) {
            this._settings = settings;
        }

        protected override void Run(CancellationToken token) {
            using (AmsiSession session = new AmsiSession()) {
                string delta = this._settings.Script;
                Token[] tokens = this._settings.Tokens;
                this.ReducedString = delta;
                this.ReducedTokens = tokens;

                Interlocked.Increment(ref this._amsiCallCount);
                if (session.ScanString(delta) != AmsiResult.Detected) {
                    return;
                }

                int iteration = 0;
                int nSteps = 2;
                while (nSteps < tokens.Length * 2 && !token.IsCancellationRequested) {
                    this.ReducedString = delta;
                    this.ReducedTokens = tokens;

                    if (nSteps > tokens.Length) {
                        nSteps = tokens.Length;
                    }

                    int stepSize = (int)Math.Ceiling(tokens.Length / (double)nSteps);

                    //Raise the iteration event handler
                    this.RaiseVerboseIterationCompleteEvent(iteration, nSteps, stepSize, delta, tokens);

                    //Increment the iteration
                    iteration++;

                    Token[][] tokenArray = SignatureFinderByToken.GenerateDeltas(this._settings.Script, tokens, stepSize);
                    bool flag = false;
                    Token[][] tokenArray1 = tokenArray;
                    this._settings.DeltaPredictor.Reset(0, tokenArray1.Length - 1);
                    int num1 = this._settings.DeltaPredictor.Next();
                    int count = 0;
                    while (this._settings.DeltaPredictor.HasNext) {
                        this._amsiCallCount = session.AmsiCallCount;
                        //Only check for  cancellation every 10 iterations
                        if (count++ % 10 == 0) {
                            if (token.IsCancellationRequested) {
                                break;
                            }
                        }

                        Token[] tokenArray2 = tokenArray1[num1];
                        string str1 = SignatureFinderByToken.CombineTokenStrings(this._settings.Script, tokenArray2);
                        if (str1.Length < this._settings.MinimumDeltaStepSize || session.ScanString(str1) != AmsiResult.Detected) {
                            num1 = this._settings.DeltaPredictor.Next();
                        } else {
                            flag = true;
                            nSteps = 2;
                            delta = str1;
                            tokens = tokenArray2;
                            break;
                        }
                    }
                    if (flag) {
                        continue;
                    }
                    if (nSteps != 2) {
                        tokenArray1 = SignatureFinderByToken.GenerateNablas(this._settings.Script, tokenArray);
                        this._settings.NablaPredictor.Reset(0, tokenArray1.Length - 1);
                        num1 = this._settings.NablaPredictor.Next();
                        count = 0;
                        while (this._settings.NablaPredictor.HasNext) {
                            this._amsiCallCount = session.AmsiCallCount;

                            //Only check for  cancellation every 10 iterations
                            if (count++ % 10 == 0) {
                                if (token.IsCancellationRequested) {
                                    break;
                                }
                            }

                            Token[] tokenArray3 = tokenArray1[num1];
                            string str2 = SignatureFinderByToken.CombineTokenStrings(this._settings.Script, tokenArray3);
                            if (session.ScanString(str2) != AmsiResult.Detected) {
                                num1 = this._settings.NablaPredictor.Next();
                            } else {
                                flag = true;
                                nSteps--;
                                delta = str2;
                                tokens = tokenArray3;
                                break;
                            }
                        }
                        if (flag) {
                            continue;
                        }
                        nSteps *= 2;
                    } else {
                        nSteps *= 2;
                    }
                }
                this._amsiCallCount = session.AmsiCallCount;
            }
        }

        protected override void OnStopping() {
            this.RaiseMinimizationCompleteEvent(this._deltaString, this._deltaTokens);
        }

        private static string CombineTokenStrings(string original, Token[] tokens) {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < tokens.Length; i++) {
                Token token = tokens[i];
                builder.Append(original, token.Start, token.Length);
            }
            return builder.ToString();
        }

        private static Token[][] GenerateDeltas(string original, Token[] delta, int step) {
            List<Token[]> deltas = new List<Token[]>();
            for (int i = 0; i < (int)delta.Length; i += step) {
                List<Token> tokens = new List<Token>();
                int num = Math.Min(step, (int)delta.Length - i);
                for (int j = 0; j < num; j++) {
                    tokens.Add(delta[i + j]);
                }
                deltas.Add(tokens.ToArray<Token>());
            }
            return deltas.ToArray<Token[]>();
        }

        private static Token[][] GenerateNablas(string original, Token[][] deltas) {
            List<Token[]> nablas = new List<Token[]>();
            for (int i = 0; i < (int)deltas.Length; i++) {
                List<Token> tokens = new List<Token>();
                for (int j = 0; j < (int)deltas.Length; j++) {
                    if (i != j) {
                        for (int k = 0; k < (int)deltas[j].Length; k++) {
                            tokens.Add(deltas[j][k]);
                        }
                    }
                }
                nablas.Add(tokens.ToArray<Token>());
            }
            return nablas.ToArray<Token[]>();
        }

        private static string[] GenerateNablas(string[] deltas) {
            List<string> nablas = new List<string>();
            for (int i = 0; i < (int)deltas.Length; i++) {
                StringBuilder builder = new StringBuilder();
                for (int j = 0; j < (int)deltas.Length; j++) {
                    if (i != j) {
                        builder.Append(deltas[j]);
                    }
                }
                nablas.Add(builder.ToString());
            }
            return nablas.ToArray<string>();
        }

        private void RaiseVerboseIterationCompleteEvent(int iteration, int nSteps, int stepSize, string deltaString, Token[] deltaToken) {
            try {
                if (this.VerboseIterationComplete != null) {
                    this.VerboseIterationComplete(iteration, nSteps, stepSize, deltaString, deltaToken);
                }
            } catch { }
        }

        private void RaiseMinimizationCompleteEvent(string reducedString, Token[] reducedToken) {
            try {
                if (this.VerboseMinimizationComplete != null) {
                    this.VerboseMinimizationComplete(reducedString, reducedToken);
                }
            } catch { }
        }

        private SignatureFinderByTokenSettings _settings;

        private string _deltaString;
        private Token[] _deltaTokens;
        private object _deltaLock = new object();
        private long _amsiCallCount;
    }
}
