using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;

namespace AmsiScanner.Common {
    public class Token {
        public int Length {
            get;
        }

        public int Start {
            get;
        }

        public string Contents {
            get;
        }

        public string TokenType {
            get;
        }

        public Token(int start, int length, string tokenType, string contents) {
            this.Start = start;
            this.Length = length;
            this.TokenType = tokenType;
            this.Contents = contents;
        }

        public static Token[] Tokenize(string script) {
            List<Token> results = new List<Token>();
            Collection<PSParseError> errors = null;
            PSToken[] tokens = PSParser.Tokenize(script, out errors).ToArray();
            int previous = 0;
            for (int i = 0; i < tokens.Length; i++) {
                PSToken current = tokens[i];

                if (current.Start > previous) {
                    int length = current.Start - previous;
                    results.Add(new Token(previous, length, "Whitespace", script.Substring(previous, length)));
                }

                results.Add(new Token(current.Start, current.Length, current.Type.ToString(), current.Content));

                previous = current.Start + current.Length;
            }

            return results.ToArray();
        }
    }
}
