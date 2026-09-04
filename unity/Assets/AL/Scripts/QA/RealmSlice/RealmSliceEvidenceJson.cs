using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AL.QA.RealmSlice
{
    public static class RealmSliceEvidenceJson
    {
        public static object Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var reader = new Reader(text);
            object value = reader.ParseValue();
            reader.SkipWhitespace();
            if (!reader.AtEnd)
                throw new FormatException("AL-RSQ-JSON-TRAILING");
            return value;
        }

        public static Dictionary<string, object> ParseObject(string text)
        {
            object value = Parse(text);
            if (value is Dictionary<string, object> map) return map;
            throw new FormatException("AL-RSQ-JSON-OBJECT-REQUIRED");
        }

        public static byte[] CanonicalBytes(object value)
        {
            var builder = new StringBuilder();
            WriteCanonical(builder, value);
            builder.Append('\n');
            return Encoding.UTF8.GetBytes(builder.ToString());
        }

        public static string Sha256Hex(byte[] payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] digest = sha256.ComputeHash(payload);
                var text = new StringBuilder(digest.Length * 2);
                foreach (byte value in digest)
                    text.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                return text.ToString();
            }
        }

        public static string CanonicalSha256(object value)
        {
            return Sha256Hex(CanonicalBytes(value));
        }

        public static bool TryGet(Dictionary<string, object> map, string key, out object value)
        {
            if (map == null || key == null)
            {
                value = null;
                return false;
            }

            return map.TryGetValue(key, out value);
        }

        public static string AsString(object value)
        {
            return value as string;
        }

        public static bool AsBool(object value, bool fallback = false)
        {
            return value is bool flag ? flag : fallback;
        }

        public static IReadOnlyList<object> AsList(object value)
        {
            return value as IReadOnlyList<object>;
        }

        public static Dictionary<string, object> AsObject(object value)
        {
            return value as Dictionary<string, object>;
        }

        private static void WriteCanonical(StringBuilder builder, object value)
        {
            if (value == null)
            {
                builder.Append("null");
                return;
            }

            if (value is bool flag)
            {
                builder.Append(flag ? "true" : "false");
                return;
            }

            if (value is string text)
            {
                WriteString(builder, text);
                return;
            }

            if (value is long whole)
            {
                builder.Append(whole.ToString(CultureInfo.InvariantCulture));
                return;
            }

            if (value is int small)
            {
                builder.Append(small.ToString(CultureInfo.InvariantCulture));
                return;
            }

            if (value is double real)
            {
                builder.Append(real.ToString("R", CultureInfo.InvariantCulture));
                return;
            }

            if (value is IReadOnlyList<object> list)
            {
                builder.Append('[');
                for (int index = 0; index < list.Count; index++)
                {
                    if (index > 0) builder.Append(',');
                    WriteCanonical(builder, list[index]);
                }

                builder.Append(']');
                return;
            }

            if (value is Dictionary<string, object> map)
            {
                var keys = new List<string>(map.Keys);
                keys.Sort(StringComparer.Ordinal);
                builder.Append('{');
                for (int index = 0; index < keys.Count; index++)
                {
                    if (index > 0) builder.Append(',');
                    WriteString(builder, keys[index]);
                    builder.Append(':');
                    WriteCanonical(builder, map[keys[index]]);
                }

                builder.Append('}');
                return;
            }

            throw new InvalidOperationException("AL-RSQ-JSON-UNSUPPORTED:" + value.GetType().Name);
        }

        private static void WriteString(StringBuilder builder, string text)
        {
            builder.Append('"');
            foreach (char character in text)
            {
                switch (character)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\b':
                        builder.Append("\\b");
                        break;
                    case '\f':
                        builder.Append("\\f");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (character < 0x20)
                        {
                            builder.Append("\\u");
                            builder.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(character);
                        }

                        break;
                }
            }

            builder.Append('"');
        }

        private sealed class Reader
        {
            private readonly string _text;
            private int _index;

            public Reader(string text)
            {
                _text = text;
            }

            public bool AtEnd => _index >= _text.Length;

            public object ParseValue()
            {
                SkipWhitespace();
                if (AtEnd) throw new FormatException("AL-RSQ-JSON-EMPTY");
                char current = _text[_index];
                if (current == '{') return ParseObject();
                if (current == '[') return ParseArray();
                if (current == '"') return ParseString();
                if (current == 't') return ParseLiteral("true", true);
                if (current == 'f') return ParseLiteral("false", false);
                if (current == 'n') return ParseLiteral("null", null);
                if (current == '-' || (current >= '0' && current <= '9')) return ParseNumber();
                throw new FormatException("AL-RSQ-JSON-TOKEN");
            }

            public void SkipWhitespace()
            {
                while (_index < _text.Length)
                {
                    char current = _text[_index];
                    if (current != ' ' && current != '\t' && current != '\n' && current != '\r')
                        return;
                    _index++;
                }
            }

            private Dictionary<string, object> ParseObject()
            {
                _index++;
                var map = new Dictionary<string, object>(StringComparer.Ordinal);
                SkipWhitespace();
                if (TryConsume('}')) return map;
                while (true)
                {
                    SkipWhitespace();
                    if (AtEnd || _text[_index] != '"')
                        throw new FormatException("AL-RSQ-JSON-KEY");
                    string key = ParseString();
                    SkipWhitespace();
                    if (!TryConsume(':')) throw new FormatException("AL-RSQ-JSON-COLON");
                    if (map.ContainsKey(key)) throw new FormatException("AL-RSQ-JSON-DUPLICATE:" + key);
                    map.Add(key, ParseValue());
                    SkipWhitespace();
                    if (TryConsume('}')) return map;
                    if (!TryConsume(',')) throw new FormatException("AL-RSQ-JSON-COMMA");
                }
            }

            private List<object> ParseArray()
            {
                _index++;
                var list = new List<object>();
                SkipWhitespace();
                if (TryConsume(']')) return list;
                while (true)
                {
                    list.Add(ParseValue());
                    SkipWhitespace();
                    if (TryConsume(']')) return list;
                    if (!TryConsume(',')) throw new FormatException("AL-RSQ-JSON-COMMA");
                }
            }

            private string ParseString()
            {
                _index++;
                var builder = new StringBuilder();
                while (!AtEnd)
                {
                    char current = _text[_index++];
                    if (current == '"') return builder.ToString();
                    if (current != '\\')
                    {
                        builder.Append(current);
                        continue;
                    }

                    if (AtEnd) throw new FormatException("AL-RSQ-JSON-ESCAPE");
                    char escape = _text[_index++];
                    switch (escape)
                    {
                        case '"':
                        case '\\':
                        case '/':
                            builder.Append(escape);
                            break;
                        case 'b':
                            builder.Append('\b');
                            break;
                        case 'f':
                            builder.Append('\f');
                            break;
                        case 'n':
                            builder.Append('\n');
                            break;
                        case 'r':
                            builder.Append('\r');
                            break;
                        case 't':
                            builder.Append('\t');
                            break;
                        case 'u':
                            if (_index + 4 > _text.Length)
                                throw new FormatException("AL-RSQ-JSON-UNICODE");
                            string hex = _text.Substring(_index, 4);
                            _index += 4;
                            builder.Append((char)int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            break;
                        default:
                            throw new FormatException("AL-RSQ-JSON-ESCAPE");
                    }
                }

                throw new FormatException("AL-RSQ-JSON-UNTERMINATED");
            }

            private object ParseNumber()
            {
                int start = _index;
                if (_text[_index] == '-') _index++;
                if (AtEnd || _text[_index] < '0' || _text[_index] > '9')
                    throw new FormatException("AL-RSQ-JSON-NUMBER");
                if (_text[_index] == '0')
                {
                    _index++;
                }
                else
                {
                    while (!AtEnd && _text[_index] >= '0' && _text[_index] <= '9') _index++;
                }

                bool real = false;
                if (!AtEnd && _text[_index] == '.')
                {
                    real = true;
                    _index++;
                    if (AtEnd || _text[_index] < '0' || _text[_index] > '9')
                        throw new FormatException("AL-RSQ-JSON-NUMBER");
                    while (!AtEnd && _text[_index] >= '0' && _text[_index] <= '9') _index++;
                }

                if (!AtEnd && (_text[_index] == 'e' || _text[_index] == 'E'))
                {
                    real = true;
                    _index++;
                    if (!AtEnd && (_text[_index] == '+' || _text[_index] == '-')) _index++;
                    if (AtEnd || _text[_index] < '0' || _text[_index] > '9')
                        throw new FormatException("AL-RSQ-JSON-NUMBER");
                    while (!AtEnd && _text[_index] >= '0' && _text[_index] <= '9') _index++;
                }

                string token = _text.Substring(start, _index - start);
                if (real)
                    return double.Parse(token, NumberStyles.Float, CultureInfo.InvariantCulture);
                return long.Parse(token, NumberStyles.Integer, CultureInfo.InvariantCulture);
            }

            private object ParseLiteral(string token, object value)
            {
                if (_index + token.Length > _text.Length ||
                    _text.Substring(_index, token.Length) != token)
                    throw new FormatException("AL-RSQ-JSON-LITERAL");
                _index += token.Length;
                return value;
            }

            private bool TryConsume(char expected)
            {
                if (AtEnd || _text[_index] != expected) return false;
                _index++;
                return true;
            }
        }
    }
}
