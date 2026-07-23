using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace AL.Data.Catalogs
{
    internal sealed class StrictJsonException : Exception
    {
        internal StrictJsonException(string code, string path, int position, string message)
            : base(message ?? string.Empty)
        {
            Code = code ?? string.Empty;
            Path = path ?? "$";
            Position = position < 0 ? 0 : position;
        }

        internal string Code { get; }
        internal string Path { get; }
        internal int Position { get; }
    }

    internal abstract class StrictJsonValue
    {
        protected StrictJsonValue(GameDataValueKind kind)
        {
            Kind = kind;
        }

        internal GameDataValueKind Kind { get; }
    }

    internal sealed class StrictJsonProperty
    {
        internal StrictJsonProperty(string name, StrictJsonValue value)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        internal string Name { get; }
        internal StrictJsonValue Value { get; }
    }

    internal sealed class StrictJsonObject : StrictJsonValue
    {
        private readonly IReadOnlyDictionary<string, StrictJsonValue> propertiesByName;

        internal StrictJsonObject(IList<StrictJsonProperty> properties)
            : base(GameDataValueKind.Object)
        {
            if (properties == null) throw new ArgumentNullException(nameof(properties));

            var ordered = new StrictJsonProperty[properties.Count];
            var index = new Dictionary<string, StrictJsonValue>(properties.Count, StringComparer.Ordinal);
            for (var propertyIndex = 0; propertyIndex < properties.Count; propertyIndex++)
            {
                var property = properties[propertyIndex];
                if (property == null) throw new ArgumentException("JSON properties cannot contain null.", nameof(properties));
                ordered[propertyIndex] = property;
                index.Add(property.Name, property.Value);
            }

            Properties = Array.AsReadOnly(ordered);
            propertiesByName = new ReadOnlyDictionary<string, StrictJsonValue>(index);
        }

        internal IReadOnlyList<StrictJsonProperty> Properties { get; }

        internal bool TryGet(string name, out StrictJsonValue value)
        {
            return propertiesByName.TryGetValue(name ?? string.Empty, out value);
        }
    }

    internal sealed class StrictJsonArray : StrictJsonValue
    {
        internal StrictJsonArray(IList<StrictJsonValue> items)
            : base(GameDataValueKind.Array)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));

            var copy = new StrictJsonValue[items.Count];
            for (var itemIndex = 0; itemIndex < items.Count; itemIndex++)
            {
                copy[itemIndex] = items[itemIndex] ??
                    throw new ArgumentException("JSON arrays cannot contain null references.", nameof(items));
            }

            Items = Array.AsReadOnly(copy);
        }

        internal IReadOnlyList<StrictJsonValue> Items { get; }
    }

    internal sealed class StrictJsonString : StrictJsonValue
    {
        internal StrictJsonString(string value)
            : base(GameDataValueKind.String)
        {
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        internal string Value { get; }
    }

    internal sealed class StrictJsonNumber : StrictJsonValue
    {
        internal StrictJsonNumber(
            string rawValue,
            double value,
            bool hasFiniteDoubleValue,
            bool hasNonZeroSignificand)
            : base(GameDataValueKind.Number)
        {
            RawValue = rawValue ?? throw new ArgumentNullException(nameof(rawValue));
            Value = value;
            HasFiniteDoubleValue = hasFiniteDoubleValue;
            HasNonZeroSignificand = hasNonZeroSignificand;
        }

        internal string RawValue { get; }
        internal double Value { get; }
        internal bool HasFiniteDoubleValue { get; }
        internal bool HasNonZeroSignificand { get; }
    }

    internal sealed class StrictJsonBoolean : StrictJsonValue
    {
        internal StrictJsonBoolean(bool value)
            : base(GameDataValueKind.Boolean)
        {
            Value = value;
        }

        internal bool Value { get; }
    }

    internal sealed class StrictJsonNull : StrictJsonValue
    {
        internal static readonly StrictJsonNull Instance = new StrictJsonNull();

        private StrictJsonNull()
            : base(GameDataValueKind.Null)
        {
        }
    }

    internal static class StrictJsonDocument
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        internal static StrictJsonValue Parse(byte[] bytes, int maximumBytes)
        {
            if (maximumBytes <= 0)
            {
                throw Error("LIMIT_INVALID", "$", 0, "The JSON byte limit must be positive.");
            }

            if (bytes == null)
            {
                throw Error("INPUT_NULL", "$", 0, "JSON input bytes are required.");
            }

            if (bytes.Length == 0)
            {
                throw Error("INPUT_EMPTY", "$", 0, "JSON input cannot be empty.");
            }

            if (bytes.Length > maximumBytes)
            {
                throw Error("INPUT_TOO_LARGE", "$", 0, "JSON input exceeds the configured byte limit.");
            }

            if (bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf)
            {
                throw Error("UTF8_BOM", "$", 0, "UTF-8 byte-order marks are not supported.");
            }

            string source;
            try
            {
                source = StrictUtf8.GetString(bytes);
            }
            catch (DecoderFallbackException exception)
            {
                var position = exception.Index < 0 ? 0 : exception.Index;
                throw Error("UTF8_INVALID", "$", position, "JSON input is not well-formed UTF-8.");
            }

            return new Parser(source).Parse();
        }

        private static StrictJsonException Error(string code, string path, int position, string message)
        {
            return new StrictJsonException(code, path, position, message);
        }

        private sealed class Parser
        {
            private readonly string source;
            private int index;
            private int nodeCount;

            internal Parser(string source)
            {
                this.source = source ?? throw new ArgumentNullException(nameof(source));
            }

            internal StrictJsonValue Parse()
            {
                SkipWhitespace();
                if (index >= source.Length)
                {
                    Fail("INPUT_EMPTY", "$", "JSON input must contain one root value.");
                }

                var value = ParseValue("$", 0);
                SkipWhitespace();
                if (index != source.Length)
                {
                    Fail("TRAILING_CONTENT", "$", "Unexpected content follows the root JSON value.");
                }

                return value;
            }

            private StrictJsonValue ParseValue(string path, int depth)
            {
                if (depth > GameDataCatalogContract.MaximumJsonDepth)
                {
                    Fail("DEPTH_LIMIT", path, "JSON nesting exceeds the supported depth.");
                }

                nodeCount++;
                if (nodeCount > GameDataCatalogContract.MaximumJsonNodes)
                {
                    Fail("NODE_LIMIT", path, "JSON value count exceeds the supported limit.");
                }

                SkipWhitespace();
                if (index >= source.Length)
                {
                    Fail("UNEXPECTED_END", path, "Unexpected end of JSON input.");
                }

                switch (source[index])
                {
                    case '{':
                        return ParseObject(path, depth);
                    case '[':
                        return ParseArray(path, depth);
                    case '"':
                        return new StrictJsonString(ParseString(path));
                    case 't':
                        ParseLiteral(path, "true");
                        return new StrictJsonBoolean(true);
                    case 'f':
                        ParseLiteral(path, "false");
                        return new StrictJsonBoolean(false);
                    case 'n':
                        ParseLiteral(path, "null");
                        return StrictJsonNull.Instance;
                    default:
                        if (source[index] == '-' || IsDigit(source[index]))
                        {
                            return ParseNumber(path);
                        }

                        Fail("TOKEN_INVALID", path, "The next character cannot begin a JSON value.");
                        return null;
                }
            }

            private StrictJsonObject ParseObject(string path, int depth)
            {
                Require(path, '{');
                SkipWhitespace();

                var properties = new List<StrictJsonProperty>();
                var names = new HashSet<string>(StringComparer.Ordinal);
                if (Consume('}'))
                {
                    return new StrictJsonObject(properties);
                }

                while (true)
                {
                    if (properties.Count >= GameDataCatalogContract.MaximumPropertiesPerObject)
                    {
                        Fail("PROPERTY_LIMIT", path, "JSON object property count exceeds the supported limit.");
                    }

                    if (index >= source.Length)
                    {
                        Fail("UNEXPECTED_END", path, "Unexpected end while reading a JSON object.");
                    }

                    if (source[index] != '"')
                    {
                        Fail("PROPERTY_NAME", path, "A JSON object property name must be a string.");
                    }

                    var name = ParseString(path);
                    var propertyPath = AppendProperty(path, name);
                    if (!names.Add(name))
                    {
                        Fail("PROPERTY_DUPLICATE", propertyPath, "A JSON object contains a duplicate property name.");
                    }

                    SkipWhitespace();
                    Require(propertyPath, ':');
                    var value = ParseValue(propertyPath, depth + 1);
                    properties.Add(new StrictJsonProperty(name, value));

                    SkipWhitespace();
                    if (Consume('}'))
                    {
                        return new StrictJsonObject(properties);
                    }

                    Require(path, ',');
                    SkipWhitespace();
                }
            }

            private StrictJsonArray ParseArray(string path, int depth)
            {
                Require(path, '[');
                SkipWhitespace();

                var items = new List<StrictJsonValue>();
                if (Consume(']'))
                {
                    return new StrictJsonArray(items);
                }

                while (true)
                {
                    if (items.Count >= GameDataCatalogContract.MaximumItemsPerArray)
                    {
                        Fail("ARRAY_LIMIT", path, "JSON array item count exceeds the supported limit.");
                    }

                    var itemPath = path + "[" + items.Count.ToString(CultureInfo.InvariantCulture) + "]";
                    items.Add(ParseValue(itemPath, depth + 1));

                    SkipWhitespace();
                    if (Consume(']'))
                    {
                        return new StrictJsonArray(items);
                    }

                    Require(path, ',');
                    SkipWhitespace();
                }
            }

            private string ParseString(string path)
            {
                Require(path, '"');
                var builder = new StringBuilder();
                while (index < source.Length)
                {
                    var characterPosition = index;
                    var character = source[index++];
                    if (character == '"')
                    {
                        ValidateSurrogates(builder, path, characterPosition);
                        return builder.ToString();
                    }

                    if (character < 0x20)
                    {
                        FailAt("STRING_CONTROL", path, characterPosition, "JSON strings cannot contain unescaped control characters.");
                    }

                    if (character != '\\')
                    {
                        AppendCharacter(builder, character, path, characterPosition);
                        continue;
                    }

                    if (index >= source.Length)
                    {
                        Fail("ESCAPE_INCOMPLETE", path, "A JSON escape sequence is incomplete.");
                    }

                    var escapePosition = index - 1;
                    var escaped = source[index++];
                    switch (escaped)
                    {
                        case '"': AppendCharacter(builder, '"', path, escapePosition); break;
                        case '\\': AppendCharacter(builder, '\\', path, escapePosition); break;
                        case '/': AppendCharacter(builder, '/', path, escapePosition); break;
                        case 'b': AppendCharacter(builder, '\b', path, escapePosition); break;
                        case 'f': AppendCharacter(builder, '\f', path, escapePosition); break;
                        case 'n': AppendCharacter(builder, '\n', path, escapePosition); break;
                        case 'r': AppendCharacter(builder, '\r', path, escapePosition); break;
                        case 't': AppendCharacter(builder, '\t', path, escapePosition); break;
                        case 'u': AppendCharacter(builder, ParseUnicodeEscape(path), path, escapePosition); break;
                        default:
                            FailAt("ESCAPE_INVALID", path, escapePosition, "JSON string contains an unsupported escape sequence.");
                            break;
                    }
                }

                Fail("STRING_UNTERMINATED", path, "JSON string is not terminated.");
                return null;
            }

            private char ParseUnicodeEscape(string path)
            {
                var escapeStart = index;
                if (index + 4 > source.Length)
                {
                    Fail("UNICODE_ESCAPE", path, "A Unicode escape must contain four hexadecimal digits.");
                }

                var value = 0;
                for (var offset = 0; offset < 4; offset++)
                {
                    var digit = HexValue(source[index++]);
                    if (digit < 0)
                    {
                        FailAt("UNICODE_ESCAPE", path, escapeStart + offset, "A Unicode escape contains a non-hexadecimal digit.");
                    }

                    value = (value << 4) | digit;
                }

                return (char)value;
            }

            private StrictJsonNumber ParseNumber(string path)
            {
                var start = index;
                Consume('-');

                if (index >= source.Length)
                {
                    Fail("NUMBER_INVALID", path, "A JSON number is incomplete.");
                }

                if (Consume('0'))
                {
                    if (index < source.Length && IsDigit(source[index]))
                    {
                        Fail("NUMBER_INVALID", path, "A JSON number cannot contain a leading zero.");
                    }
                }
                else
                {
                    if (index >= source.Length || source[index] < '1' || source[index] > '9')
                    {
                        Fail("NUMBER_INVALID", path, "A JSON number requires an integer component.");
                    }

                    while (index < source.Length && IsDigit(source[index])) index++;
                }

                if (Consume('.'))
                {
                    if (index >= source.Length || !IsDigit(source[index]))
                    {
                        Fail("NUMBER_INVALID", path, "A JSON number fraction requires at least one digit.");
                    }

                    while (index < source.Length && IsDigit(source[index])) index++;
                }

                if (index < source.Length && (source[index] == 'e' || source[index] == 'E'))
                {
                    index++;
                    if (index < source.Length && (source[index] == '+' || source[index] == '-')) index++;
                    if (index >= source.Length || !IsDigit(source[index]))
                    {
                        Fail("NUMBER_INVALID", path, "A JSON number exponent requires at least one digit.");
                    }

                    while (index < source.Length && IsDigit(source[index])) index++;
                }

                var rawValue = source.Substring(start, index - start);
                double value;
                var hasFiniteDoubleValue =
                    double.TryParse(
                        rawValue,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out value) &&
                    !double.IsNaN(value) &&
                    !double.IsInfinity(value);

                var hasNonZeroSignificand = false;
                for (var tokenIndex = 0; tokenIndex < rawValue.Length; tokenIndex++)
                {
                    var character = rawValue[tokenIndex];
                    if (character == 'e' || character == 'E')
                    {
                        break;
                    }

                    if (character >= '1' && character <= '9')
                    {
                        hasNonZeroSignificand = true;
                        break;
                    }
                }

                // JSON itself does not impose an IEEE-754 range. Keep the exact token
                // so domain validators can degrade a known field or preserve an
                // unknown future field without rejecting the entire document.
                return new StrictJsonNumber(
                    rawValue,
                    hasFiniteDoubleValue ? value : 0d,
                    hasFiniteDoubleValue,
                    hasNonZeroSignificand);
            }

            private void ParseLiteral(string path, string literal)
            {
                if (index + literal.Length > source.Length ||
                    !string.Equals(source.Substring(index, literal.Length), literal, StringComparison.Ordinal))
                {
                    Fail("LITERAL_INVALID", path, "JSON literal is invalid.");
                }

                index += literal.Length;
            }

            private void AppendCharacter(StringBuilder builder, char value, string path, int position)
            {
                if (builder.Length >= GameDataCatalogContract.MaximumStringLength)
                {
                    FailAt("STRING_LIMIT", path, position, "JSON string length exceeds the supported limit.");
                }

                builder.Append(value);
            }

            private void ValidateSurrogates(StringBuilder builder, string path, int endPosition)
            {
                for (var characterIndex = 0; characterIndex < builder.Length; characterIndex++)
                {
                    var value = builder[characterIndex];
                    if (char.IsHighSurrogate(value))
                    {
                        if (characterIndex + 1 >= builder.Length || !char.IsLowSurrogate(builder[characterIndex + 1]))
                        {
                            FailAt("SURROGATE_INVALID", path, endPosition, "JSON string contains an unpaired high surrogate.");
                        }

                        characterIndex++;
                    }
                    else if (char.IsLowSurrogate(value))
                    {
                        FailAt("SURROGATE_INVALID", path, endPosition, "JSON string contains an unpaired low surrogate.");
                    }
                }
            }

            private void SkipWhitespace()
            {
                while (index < source.Length)
                {
                    var value = source[index];
                    if (value != ' ' && value != '\t' && value != '\r' && value != '\n') return;
                    index++;
                }
            }

            private bool Consume(char expected)
            {
                if (index >= source.Length || source[index] != expected) return false;
                index++;
                return true;
            }

            private void Require(string path, char expected)
            {
                if (!Consume(expected))
                {
                    Fail("TOKEN_EXPECTED", path, "JSON syntax is missing a required delimiter.");
                }
            }

            private void Fail(string code, string path, string message)
            {
                throw Error(code, path, index, message);
            }

            private void FailAt(string code, string path, int position, string message)
            {
                throw Error(code, path, position, message);
            }

            private static string AppendProperty(string path, string propertyName)
            {
                if (IsSafePathSegment(propertyName))
                {
                    return path + "." + propertyName;
                }

                return path + ".<property>";
            }

            private static bool IsSafePathSegment(string value)
            {
                if (string.IsNullOrEmpty(value) || value.Length > 64) return false;
                for (var characterIndex = 0; characterIndex < value.Length; characterIndex++)
                {
                    var character = value[characterIndex];
                    var isLetter = (character >= 'a' && character <= 'z') ||
                                   (character >= 'A' && character <= 'Z');
                    var isDigit = character >= '0' && character <= '9';
                    if (!isLetter && !isDigit && character != '_' && character != '-') return false;
                }

                return true;
            }

            private static int HexValue(char value)
            {
                if (value >= '0' && value <= '9') return value - '0';
                if (value >= 'a' && value <= 'f') return value - 'a' + 10;
                if (value >= 'A' && value <= 'F') return value - 'A' + 10;
                return -1;
            }

            private static bool IsDigit(char value)
            {
                return value >= '0' && value <= '9';
            }
        }
    }
}
