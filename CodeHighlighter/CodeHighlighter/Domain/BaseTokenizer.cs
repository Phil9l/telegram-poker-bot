﻿using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CodeHighlighter.Domain
{
    public abstract class BaseTokenizer
    {
        public abstract string Name { get; }
        public abstract List<string> Extensions { get; }
        public abstract List<TokenMapping> Tokens { get; }
        public abstract HashSet<string> Builtins { get; }
        public abstract HashSet<string> Keywords { get; }
        public abstract string Variable { get; }
        public abstract bool CheckVariable { get; }
        public abstract string CorrectName { get; }

        private bool CheckBuiltin(string token)
        {
            return Builtins.Contains(token);
        }

        private bool CheckKeyword(string token)
        {
            return Keywords.Contains(token);
        }

        private ParsedData ParseLine(string line)
        {
            var result = new ParsedData();
            result.Found = false;
            var nearestIndex = -1;
            foreach (var tokenMapping in Tokens)
            {
                var v = Regex.Match(line, tokenMapping.RegularExpression);
                if (!v.Success)
                    continue;
                if (nearestIndex == -1 || v.Index < nearestIndex)
                {
                    nearestIndex = v.Index;
                    result = new ParsedData(v, tokenMapping.Type);
                }
            }
            return result;
        }

        public IEnumerable<Token> HandleLine(string line, int lineIndex)
        {
            var offset = 0;
            while (line.Length > 0)
            {
                var buf = ParseLine(line);
                var found = buf.Found;
                var regexToken = buf.RegularExpression;
                var tokenType = buf.Type;

                if (tokenType == TokenTypes.Name && CheckBuiltin(regexToken.Groups[0].Value))
                    tokenType = TokenTypes.Builtin;
                if (tokenType == TokenTypes.Name && CheckKeyword(regexToken.Groups[0].Value))
                    tokenType = TokenTypes.Keyword;
                if (!found)
                    break;
                var start = new Position(lineIndex, offset + regexToken.Index);
                var end = new Position(lineIndex, offset + regexToken.Index + regexToken.Length);

                var content = regexToken.Groups[0].Value;
                if (tokenType == TokenTypes.Name)
                {
                    if (!Regex.Match(content, CorrectName).Success)
                        tokenType = TokenTypes.IncorrectName;
                    if (CheckVariable && Regex.Match(content, Variable).Success)
                        tokenType = TokenTypes.Variable;
                }
                yield return new Token(tokenType, content, start, end);
                var regexEnd = regexToken.Index + regexToken.Length;
                offset += regexEnd;
                line = line.Substring(regexEnd);
            }
        }

        public IEnumerable<Token> Tokenize(IEnumerable<string> lines)
        {
            var indent = 0;
            var currentIndent = 0;
            var index = 0;

            foreach (var line in lines)
            {
                index++;
                foreach (var token in HandleLine(line, index))
                {
                    switch (token.Type)
                    {
                        case TokenTypes.Indent:
                            currentIndent++;
                            if (currentIndent > indent)
                            {
                                indent = currentIndent;
                                token.Type = TokenTypes.NewIndent;
                            }
                            break;
                        case TokenTypes.LineBreak:
                            currentIndent = 0;
                            break;
                        default:
                            while (indent > currentIndent)
                            {
                                indent--;
                                yield return new Token(TokenTypes.Dedent, "", token.Start, token.Start);
                            }
                            break;
                    }
                    yield return token;
                }
            }
        }
    }
}