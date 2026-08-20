using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AL.Narrative.Nvs01.Contracts;

namespace AL.Narrative.Nvs01
{
    /// <summary>
    /// Fail-closed validation for the single catalog profile approved by NVS-01 G1.
    /// This type deliberately has no Unity dependency so the same contract can be
    /// exercised by editor validation, runtime loading, and later bridge tooling.
    /// </summary>
    public static class Nvs01CatalogValidator
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        internal static bool IsExactCurrentStateId(string value) =>
            CatalogSemantics.IsExpectedStateId(value);

        internal static bool IsExactCurrentDialogueId(string value) =>
            CatalogSemantics.IsExpectedDialogueId(value);

        internal static bool IsExactCurrentObjectiveId(
            int index,
            string value) =>
            CatalogSemantics.IsExpectedObjectiveId(index, value);

        internal static int ExactCurrentObjectiveCount =>
            CatalogSemantics.ExpectedObjectiveCount;

        internal static bool IsExactCurrentConsequenceId(string value) =>
            CatalogSemantics.IsExpectedConsequenceId(value);

        internal static bool IsExactCurrentEligibleRealmId(string value) =>
            CatalogSemantics.IsExpectedRealmId(value);

        public static Nvs01CatalogValidationResult ValidateCanonicalArtifact(byte[] bytes)
        {
            string json;
            string sha256;
            Nvs01CatalogDiagnostic diagnostic;
            if (!TryDecodeDocument(bytes, true, out json, out sha256, out diagnostic))
            {
                return Rejected(diagnostic);
            }

            return ValidateDecoded(json, bytes.Length, sha256);
        }

        // Semantic-fixture seam only. Runtime consumers must use
        // ValidateCanonicalArtifact so "verified" always includes source-byte identity.
        internal static Nvs01CatalogValidationResult ValidateDocument(byte[] bytes)
        {
            string json;
            string sha256;
            Nvs01CatalogDiagnostic diagnostic;
            if (!TryDecodeDocument(bytes, false, out json, out sha256, out diagnostic))
            {
                return Rejected(diagnostic);
            }

            return ValidateDecoded(json, bytes.Length, sha256);
        }

        public static bool TryCanonicalizeSource(
            byte[] sourceBytes,
            out byte[] canonicalBytes,
            out Nvs01CatalogDiagnostic diagnostic)
        {
            canonicalBytes = null;
            diagnostic = null;

            if (sourceBytes == null)
            {
                diagnostic = Diagnostic(
                    "CATALOG-MISSING",
                    "$",
                    "Catalog source bytes are required.",
                    "non-null UTF-8 bytes",
                    "null");
                return false;
            }

            if (sourceBytes.Length > Nvs01CatalogContract.MaximumByteLength * 2)
            {
                diagnostic = Diagnostic(
                    "CATALOG-MALFORMED",
                    "$",
                    "Catalog source exceeds the bounded input size.",
                    "at most " + (Nvs01CatalogContract.MaximumByteLength * 2).ToString(CultureInfo.InvariantCulture) + " bytes before line-ending normalization",
                    sourceBytes.Length.ToString(CultureInfo.InvariantCulture));
                return false;
            }

            if (HasUtf8Bom(sourceBytes))
            {
                diagnostic = Diagnostic(
                    "CATALOG-MALFORMED",
                    "$",
                    "A UTF-8 byte-order mark is prohibited.",
                    "UTF-8 without BOM",
                    "UTF-8 BOM");
                return false;
            }

            try
            {
                StrictUtf8.GetString(sourceBytes);
            }
            catch (DecoderFallbackException exception)
            {
                diagnostic = Diagnostic(
                    "CATALOG-MALFORMED",
                    "$",
                    "Catalog source is not strict UTF-8.",
                    "well-formed UTF-8",
                    exception.Message);
                return false;
            }

            var normalized = new List<byte>(sourceBytes.Length);
            for (var index = 0; index < sourceBytes.Length; index++)
            {
                var value = sourceBytes[index];
                if (value != 0x0d)
                {
                    normalized.Add(value);
                    continue;
                }

                if (index + 1 >= sourceBytes.Length || sourceBytes[index + 1] != 0x0a)
                {
                    diagnostic = Diagnostic(
                        "CATALOG-MALFORMED",
                        "$",
                        "A bare carriage return is prohibited.",
                        "LF or CRLF source line endings",
                        "bare CR at byte " + index.ToString(CultureInfo.InvariantCulture));
                    return false;
                }

                normalized.Add(0x0a);
                index++;
            }

            var candidate = normalized.ToArray();
            var result = ValidateCanonicalArtifact(candidate);
            if (!result.IsAccepted)
            {
                diagnostic = result.Diagnostics.Count == 0
                    ? Diagnostic("CATALOG-MALFORMED", "$", "Catalog source was rejected.", "approved canonical catalog", "rejected")
                    : result.Diagnostics[0];
                return false;
            }

            canonicalBytes = candidate;
            return true;
        }

        private static bool TryDecodeDocument(
            byte[] bytes,
            bool requireCanonicalIdentity,
            out string json,
            out string sha256,
            out Nvs01CatalogDiagnostic diagnostic)
        {
            json = null;
            sha256 = null;
            diagnostic = null;

            if (bytes == null)
            {
                diagnostic = Diagnostic("CATALOG-MISSING", "$", "Catalog bytes are required.", "non-null UTF-8 bytes", "null");
                return false;
            }

            if (bytes.Length > Nvs01CatalogContract.MaximumByteLength)
            {
                diagnostic = Diagnostic(
                    "CATALOG-MALFORMED",
                    "$",
                    "Catalog exceeds the approved uncompressed size bound.",
                    "at most " + Nvs01CatalogContract.MaximumByteLength.ToString(CultureInfo.InvariantCulture) + " bytes",
                    bytes.Length.ToString(CultureInfo.InvariantCulture));
                return false;
            }

            if (HasUtf8Bom(bytes))
            {
                diagnostic = Diagnostic("CATALOG-MALFORMED", "$", "A UTF-8 byte-order mark is prohibited.", "UTF-8 without BOM", "UTF-8 BOM");
                return false;
            }

            for (var index = 0; index < bytes.Length; index++)
            {
                if (bytes[index] == 0x0d)
                {
                    diagnostic = Diagnostic(
                        "CATALOG-MALFORMED",
                        "$",
                        "Canonical/document validation accepts LF line endings only.",
                        "LF with no CR bytes",
                        "CR at byte " + index.ToString(CultureInfo.InvariantCulture));
                    return false;
                }
            }

            if (bytes.Length == 0 || bytes[bytes.Length - 1] != 0x0a)
            {
                diagnostic = Diagnostic("CATALOG-MALFORMED", "$", "Catalog must end with a final LF.", "final byte 0x0A", bytes.Length == 0 ? "empty input" : "no final LF");
                return false;
            }

            try
            {
                json = StrictUtf8.GetString(bytes);
            }
            catch (DecoderFallbackException exception)
            {
                diagnostic = Diagnostic("CATALOG-MALFORMED", "$", "Catalog is not strict UTF-8.", "well-formed UTF-8", exception.Message);
                return false;
            }

            sha256 = ComputeSha256(bytes);
            if (!requireCanonicalIdentity)
            {
                return true;
            }

            if (bytes.Length != Nvs01CatalogContract.CanonicalByteLength)
            {
                diagnostic = Diagnostic(
                    "HASH-DRIFT",
                    "$",
                    "Runtime catalog byte length differs from the approved artifact.",
                    Nvs01CatalogContract.CanonicalByteLength.ToString(CultureInfo.InvariantCulture),
                    bytes.Length.ToString(CultureInfo.InvariantCulture));
                return false;
            }

            if (!string.Equals(sha256, Nvs01CatalogContract.CanonicalSha256, StringComparison.Ordinal))
            {
                diagnostic = Diagnostic(
                    "HASH-DRIFT",
                    "$",
                    "Runtime catalog hash differs from the approved artifact.",
                    Nvs01CatalogContract.CanonicalSha256,
                    sha256);
                return false;
            }

            return true;
        }

        private static Nvs01CatalogValidationResult ValidateDecoded(string json, int byteLength, string sha256)
        {
            try
            {
                var root = new JsonParser(json).Parse();
                var catalog = CatalogReader.Read(root);
                CatalogSemantics.Validate(catalog);
                return new Nvs01CatalogValidationResult(
                    Nvs01CatalogValidationStatus.Accepted,
                    new Nvs01VerifiedCatalog(catalog, byteLength, sha256),
                    new Nvs01CatalogDiagnostic[0]);
            }
            catch (CatalogValidationException exception)
            {
                return Rejected(exception.Diagnostic);
            }
            catch (JsonParseException exception)
            {
                return Rejected(Diagnostic(
                    "CATALOG-MALFORMED",
                    exception.Path,
                    exception.Message,
                    "strict JSON",
                    "character " + exception.Position.ToString(CultureInfo.InvariantCulture)));
            }
            catch (Exception exception)
            {
                return Rejected(Diagnostic(
                    "CATALOG-MALFORMED",
                    "$",
                    "Catalog validation failed without publishing a partial catalog.",
                    "approved catalog",
                    exception.GetType().Name + ": " + exception.Message));
            }
        }

        private static Nvs01CatalogValidationResult Rejected(Nvs01CatalogDiagnostic diagnostic)
        {
            return new Nvs01CatalogValidationResult(
                Nvs01CatalogValidationStatus.Rejected,
                null,
                new[] { diagnostic });
        }

        private static Nvs01CatalogDiagnostic Diagnostic(string code, string path, string message, string expected, string actual)
        {
            return new Nvs01CatalogDiagnostic(code, path, message, expected, actual);
        }

        private static bool HasUtf8Bom(byte[] bytes)
        {
            return bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf;
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using (var algorithm = SHA256.Create())
            {
                var hash = algorithm.ComputeHash(bytes);
                var builder = new StringBuilder(hash.Length * 2);
                for (var index = 0; index < hash.Length; index++)
                {
                    builder.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }

        private sealed class CatalogValidationException : Exception
        {
            public CatalogValidationException(Nvs01CatalogDiagnostic diagnostic)
                : base(diagnostic.Message)
            {
                Diagnostic = diagnostic;
            }

            public Nvs01CatalogDiagnostic Diagnostic { get; }
        }

        private sealed class JsonParseException : Exception
        {
            public JsonParseException(string path, int position, string message)
                : base(message)
            {
                Path = path;
                Position = position;
            }

            public string Path { get; }
            public int Position { get; }
        }

        private abstract class JsonValue
        {
        }

        private sealed class JsonObject : JsonValue
        {
            public JsonObject(List<JsonProperty> properties)
            {
                Properties = properties;
            }

            public List<JsonProperty> Properties { get; }
        }

        private sealed class JsonProperty
        {
            public JsonProperty(string name, JsonValue value)
            {
                Name = name;
                Value = value;
            }

            public string Name { get; }
            public JsonValue Value { get; }
        }

        private sealed class JsonArray : JsonValue
        {
            public JsonArray(List<JsonValue> items)
            {
                Items = items;
            }

            public List<JsonValue> Items { get; }
        }

        private sealed class JsonString : JsonValue
        {
            public JsonString(string value)
            {
                Value = value;
            }

            public string Value { get; }
        }

        private sealed class JsonNumber : JsonValue
        {
            public JsonNumber(string lexeme)
            {
                Lexeme = lexeme;
            }

            public string Lexeme { get; }
        }

        private sealed class JsonBoolean : JsonValue
        {
            public JsonBoolean(bool value)
            {
                Value = value;
            }

            public bool Value { get; }
        }

        private sealed class JsonNull : JsonValue
        {
            public static readonly JsonNull Instance = new JsonNull();

            private JsonNull()
            {
            }
        }

        private sealed class JsonParser
        {
            private const int MaximumDepth = 64;
            private const int MaximumNodes = 4096;
            private readonly string source;
            private int index;
            private int nodeCount;

            public JsonParser(string source)
            {
                this.source = source ?? throw new ArgumentNullException(nameof(source));
            }

            public JsonValue Parse()
            {
                SkipWhitespace();
                var value = ParseValue("$", 0);
                SkipWhitespace();
                if (index != source.Length)
                {
                    Fail("$", "Unexpected content follows the root value.");
                }

                return value;
            }

            private JsonValue ParseValue(string path, int depth)
            {
                if (depth > MaximumDepth)
                {
                    Fail(path, "JSON nesting exceeds the supported depth.");
                }

                nodeCount++;
                if (nodeCount > MaximumNodes)
                {
                    Fail(path, "JSON value count exceeds the bounded parser limit.");
                }

                SkipWhitespace();
                if (index >= source.Length)
                {
                    Fail(path, "Unexpected end of JSON.");
                }

                switch (source[index])
                {
                    case '{':
                        return ParseObject(path, depth + 1);
                    case '[':
                        return ParseArray(path, depth + 1);
                    case '"':
                        return new JsonString(ParseString(path));
                    case 't':
                        ParseLiteral(path, "true");
                        return new JsonBoolean(true);
                    case 'f':
                        ParseLiteral(path, "false");
                        return new JsonBoolean(false);
                    case 'n':
                        ParseLiteral(path, "null");
                        return JsonNull.Instance;
                    default:
                        if (source[index] == '-' || IsDigit(source[index]))
                        {
                            return new JsonNumber(ParseNumber(path));
                        }

                        Fail(path, "Unexpected token.");
                        return null;
                }
            }

            private JsonObject ParseObject(string path, int depth)
            {
                index++;
                SkipWhitespace();
                var properties = new List<JsonProperty>();
                if (Consume('}'))
                {
                    return new JsonObject(properties);
                }

                while (true)
                {
                    if (index >= source.Length || source[index] != '"')
                    {
                        Fail(path, "Object property name must be a JSON string.");
                    }

                    var name = ParseString(path);
                    SkipWhitespace();
                    Require(path, ':');
                    var propertyPath = AppendProperty(path, name);
                    var value = ParseValue(propertyPath, depth);
                    properties.Add(new JsonProperty(name, value));
                    SkipWhitespace();
                    if (Consume('}'))
                    {
                        return new JsonObject(properties);
                    }

                    Require(path, ',');
                    SkipWhitespace();
                }
            }

            private JsonArray ParseArray(string path, int depth)
            {
                index++;
                SkipWhitespace();
                var items = new List<JsonValue>();
                if (Consume(']'))
                {
                    return new JsonArray(items);
                }

                while (true)
                {
                    items.Add(ParseValue(path + "[" + items.Count.ToString(CultureInfo.InvariantCulture) + "]", depth));
                    SkipWhitespace();
                    if (Consume(']'))
                    {
                        return new JsonArray(items);
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
                    var value = source[index++];
                    if (value == '"')
                    {
                        ValidateSurrogates(path, builder);
                        return builder.ToString();
                    }

                    if (value < 0x20)
                    {
                        Fail(path, "Unescaped control character in JSON string.");
                    }

                    if (value != '\\')
                    {
                        builder.Append(value);
                        continue;
                    }

                    if (index >= source.Length)
                    {
                        Fail(path, "Incomplete JSON escape sequence.");
                    }

                    switch (source[index++])
                    {
                        case '"': builder.Append('"'); break;
                        case '\\': builder.Append('\\'); break;
                        case '/': builder.Append('/'); break;
                        case 'b': builder.Append('\b'); break;
                        case 'f': builder.Append('\f'); break;
                        case 'n': builder.Append('\n'); break;
                        case 'r': builder.Append('\r'); break;
                        case 't': builder.Append('\t'); break;
                        case 'u': builder.Append(ParseUnicodeEscape(path)); break;
                        default:
                            Fail(path, "Unsupported JSON escape sequence.");
                            break;
                    }
                }

                Fail(path, "Unterminated JSON string.");
                return null;
            }

            private char ParseUnicodeEscape(string path)
            {
                if (index + 4 > source.Length)
                {
                    Fail(path, "Incomplete Unicode escape sequence.");
                }

                var value = 0;
                for (var offset = 0; offset < 4; offset++)
                {
                    var digit = HexValue(source[index++]);
                    if (digit < 0)
                    {
                        Fail(path, "Invalid Unicode escape sequence.");
                    }

                    value = (value << 4) | digit;
                }

                return (char)value;
            }

            private string ParseNumber(string path)
            {
                var start = index;
                Consume('-');
                if (index >= source.Length)
                {
                    Fail(path, "Incomplete JSON number.");
                }

                if (Consume('0'))
                {
                    if (index < source.Length && IsDigit(source[index]))
                    {
                        Fail(path, "Leading zero is not valid in a JSON number.");
                    }
                }
                else
                {
                    if (index >= source.Length || source[index] < '1' || source[index] > '9')
                    {
                        Fail(path, "Invalid JSON number.");
                    }

                    while (index < source.Length && IsDigit(source[index])) index++;
                }

                if (Consume('.'))
                {
                    if (index >= source.Length || !IsDigit(source[index]))
                    {
                        Fail(path, "Fraction requires at least one digit.");
                    }

                    while (index < source.Length && IsDigit(source[index])) index++;
                }

                if (index < source.Length && (source[index] == 'e' || source[index] == 'E'))
                {
                    index++;
                    if (index < source.Length && (source[index] == '+' || source[index] == '-')) index++;
                    if (index >= source.Length || !IsDigit(source[index]))
                    {
                        Fail(path, "Exponent requires at least one digit.");
                    }

                    while (index < source.Length && IsDigit(source[index])) index++;
                }

                return source.Substring(start, index - start);
            }

            private void ParseLiteral(string path, string literal)
            {
                if (index + literal.Length > source.Length ||
                    !string.Equals(source.Substring(index, literal.Length), literal, StringComparison.Ordinal))
                {
                    Fail(path, "Invalid JSON literal.");
                }

                index += literal.Length;
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

            private bool Consume(char value)
            {
                if (index >= source.Length || source[index] != value) return false;
                index++;
                return true;
            }

            private void Require(string path, char value)
            {
                if (!Consume(value))
                {
                    Fail(path, "Expected '" + value + "'.");
                }
            }

            private void Fail(string path, string message)
            {
                throw new JsonParseException(path, index, message);
            }

            private static void ValidateSurrogates(string path, StringBuilder builder)
            {
                for (var index = 0; index < builder.Length; index++)
                {
                    var value = builder[index];
                    if (char.IsHighSurrogate(value))
                    {
                        if (index + 1 >= builder.Length || !char.IsLowSurrogate(builder[index + 1]))
                        {
                            throw new JsonParseException(path, 0, "Unpaired high surrogate in JSON string.");
                        }

                        index++;
                    }
                    else if (char.IsLowSurrogate(value))
                    {
                        throw new JsonParseException(path, 0, "Unpaired low surrogate in JSON string.");
                    }
                }
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

        private static class CatalogReader
        {
            private static readonly string[] RootRequired =
            {
                "schemaVersion", "packetVersion", "milestoneId", "questId", "titleKey", "descriptionKey",
                "approval", "placement", "speaker", "states", "objectives", "dialogue", "transitions",
                "externalCapabilities", "consequences", "abandonment", "localization"
            };

            public static Nvs01Catalog Read(JsonValue value)
            {
                var root = Object(value, "$", RootRequired, Empty);
                var schemaVersion = Int32(root.Get("schemaVersion"), "$.schemaVersion");
                var packetVersion = String(root.Get("packetVersion"), "$.packetVersion");
                var milestoneId = String(root.Get("milestoneId"), "$.milestoneId");
                var questId = String(root.Get("questId"), "$.questId");
                var titleKey = String(root.Get("titleKey"), "$.titleKey");
                var descriptionKey = String(root.Get("descriptionKey"), "$.descriptionKey");
                var approval = ReadApproval(root.Get("approval"), "$.approval");
                var placement = ReadPlacement(root.Get("placement"), "$.placement");
                var speaker = ReadSpeaker(root.Get("speaker"), "$.speaker");
                var states = ReadArray(root.Get("states"), "$.states", ReadState);
                var objectives = ReadArray(root.Get("objectives"), "$.objectives", ReadObjective);
                var dialogue = ReadArray(root.Get("dialogue"), "$.dialogue", ReadDialogue);
                var transitions = ReadArray(root.Get("transitions"), "$.transitions", ReadTransition);
                var externalCapabilities = ReadArray(root.Get("externalCapabilities"), "$.externalCapabilities", ReadExternalCapability);
                var consequences = ReadArray(root.Get("consequences"), "$.consequences", ReadConsequence);
                var abandonment = ReadAbandonment(root.Get("abandonment"), "$.abandonment");
                var localization = ReadLocalization(root.Get("localization"), "$.localization");

                ValidateUnique(states, item => item.Id, "$.states");
                ValidateUnique(objectives, item => item.Id, "$.objectives");
                ValidateUnique(dialogue, item => item.Id, "$.dialogue");
                ValidateUnique(externalCapabilities, item => item.Id, "$.externalCapabilities");
                ValidateUnique(consequences, item => item.Id, "$.consequences");
                ValidateUniqueTransitions(transitions);

                return new Nvs01Catalog(
                    schemaVersion,
                    packetVersion,
                    milestoneId,
                    questId,
                    titleKey,
                    descriptionKey,
                    approval,
                    placement,
                    speaker,
                    states,
                    objectives,
                    dialogue,
                    transitions,
                    externalCapabilities,
                    consequences,
                    abandonment,
                    localization);
            }

            private static Nvs01Approval ReadApproval(JsonValue value, string path)
            {
                var fields = Object(value, path, new[] { "issue", "commentId", "decisions" }, Empty);
                return new Nvs01Approval(
                    Int32(fields.Get("issue"), path + ".issue"),
                    Int64(fields.Get("commentId"), path + ".commentId"),
                    ReadStringArray(fields.Get("decisions"), path + ".decisions"));
            }

            private static Nvs01Placement ReadPlacement(JsonValue value, string path)
            {
                var fields = Object(
                    value,
                    path,
                    new[] { "contextId", "eligibleRealmIds", "prerequisite", "offerAction", "autoAccept", "completionUnlockId", "completionDestination" },
                    Empty);
                return new Nvs01Placement(
                    String(fields.Get("contextId"), path + ".contextId"),
                    ReadStringArray(fields.Get("eligibleRealmIds"), path + ".eligibleRealmIds"),
                    String(fields.Get("prerequisite"), path + ".prerequisite"),
                    String(fields.Get("offerAction"), path + ".offerAction"),
                    Boolean(fields.Get("autoAccept"), path + ".autoAccept"),
                    String(fields.Get("completionUnlockId"), path + ".completionUnlockId"),
                    String(fields.Get("completionDestination"), path + ".completionDestination"));
            }

            private static Nvs01Speaker ReadSpeaker(JsonValue value, string path)
            {
                var fields = Object(value, path, new[] { "id", "nameKey", "roleKey" }, Empty);
                return new Nvs01Speaker(
                    String(fields.Get("id"), path + ".id"),
                    String(fields.Get("nameKey"), path + ".nameKey"),
                    String(fields.Get("roleKey"), path + ".roleKey"));
            }

            private static Nvs01State ReadState(JsonValue value, string path)
            {
                var fields = Object(value, path, new[] { "id", "resume", "terminal" }, new[] { "transient" });
                return new Nvs01State(
                    String(fields.Get("id"), path + ".id"),
                    String(fields.Get("resume"), path + ".resume"),
                    Boolean(fields.Get("terminal"), path + ".terminal"),
                    fields.Has("transient") && Boolean(fields.Get("transient"), path + ".transient"));
            }

            private static Nvs01Objective ReadObjective(JsonValue value, string path)
            {
                var fields = Object(value, path, new[] { "id", "textKey", "activatesIn", "completesOn" }, Empty);
                return new Nvs01Objective(
                    String(fields.Get("id"), path + ".id"),
                    String(fields.Get("textKey"), path + ".textKey"),
                    String(fields.Get("activatesIn"), path + ".activatesIn"),
                    String(fields.Get("completesOn"), path + ".completesOn"));
            }

            private static Nvs01DialogueNode ReadDialogue(JsonValue value, string path)
            {
                var fields = Object(value, path, new[] { "id", "speakerId", "textKey", "choices" }, new[] { "semanticAction" });
                return new Nvs01DialogueNode(
                    String(fields.Get("id"), path + ".id"),
                    String(fields.Get("speakerId"), path + ".speakerId"),
                    String(fields.Get("textKey"), path + ".textKey"),
                    fields.Has("semanticAction") ? String(fields.Get("semanticAction"), path + ".semanticAction") : null,
                    ReadArray(fields.Get("choices"), path + ".choices", ReadChoice));
            }

            private static Nvs01DialogueChoice ReadChoice(JsonValue value, string path)
            {
                var fields = Object(value, path, new[] { "key" }, new[] { "target", "semanticAction" });
                var target = fields.Has("target") ? String(fields.Get("target"), path + ".target") : null;
                var semanticAction = fields.Has("semanticAction") ? String(fields.Get("semanticAction"), path + ".semanticAction") : null;
                if (target == null && semanticAction == null)
                {
                    Throw(
                        "CATALOG-MALFORMED",
                        path,
                        "A dialogue choice must declare a target or semanticAction.",
                        "target and/or semanticAction",
                        "neither");
                }

                return new Nvs01DialogueChoice(
                    String(fields.Get("key"), path + ".key"),
                    target,
                    semanticAction);
            }

            private static Nvs01Transition ReadTransition(JsonValue value, string path)
            {
                var fields = Object(value, path, new[] { "from", "event", "to" }, new[] { "objective", "dialogue" });
                return new Nvs01Transition(
                    String(fields.Get("from"), path + ".from"),
                    String(fields.Get("event"), path + ".event"),
                    String(fields.Get("to"), path + ".to"),
                    fields.Has("objective") ? String(fields.Get("objective"), path + ".objective") : null,
                    fields.Has("dialogue") ? String(fields.Get("dialogue"), path + ".dialogue") : null);
            }

            private static Nvs01ExternalCapability ReadExternalCapability(JsonValue value, string path)
            {
                var fields = Object(value, path, new[] { "id", "status" }, Empty);
                return new Nvs01ExternalCapability(
                    String(fields.Get("id"), path + ".id"),
                    String(fields.Get("status"), path + ".status"));
            }

            private static Nvs01Consequence ReadConsequence(JsonValue value, string path)
            {
                var fields = Object(value, path, new[] { "id", "target", "trigger", "repeatability" }, new[] { "retained", "amount" });
                return new Nvs01Consequence(
                    String(fields.Get("id"), path + ".id"),
                    String(fields.Get("target"), path + ".target"),
                    String(fields.Get("trigger"), path + ".trigger"),
                    String(fields.Get("repeatability"), path + ".repeatability"),
                    fields.Has("retained") ? (bool?)Boolean(fields.Get("retained"), path + ".retained") : null,
                    fields.Has("amount") ? (long?)Int64(fields.Get("amount"), path + ".amount") : null);
            }

            private static Nvs01Abandonment ReadAbandonment(JsonValue value, string path)
            {
                var fields = Object(
                    value,
                    path,
                    new[] { "allowedOutsideActiveEncounter", "resultState", "clearsActiveProgress", "clearsUnearnedConsequences", "retainsEarnedConsequences" },
                    Empty);
                return new Nvs01Abandonment(
                    Boolean(fields.Get("allowedOutsideActiveEncounter"), path + ".allowedOutsideActiveEncounter"),
                    String(fields.Get("resultState"), path + ".resultState"),
                    Boolean(fields.Get("clearsActiveProgress"), path + ".clearsActiveProgress"),
                    Boolean(fields.Get("clearsUnearnedConsequences"), path + ".clearsUnearnedConsequences"),
                    Boolean(fields.Get("retainsEarnedConsequences"), path + ".retainsEarnedConsequences"));
            }

            private static Dictionary<string, string> ReadLocalization(JsonValue value, string path)
            {
                var source = value as JsonObject;
                if (source == null)
                {
                    TypeFailure(path, "object", value);
                }

                var result = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var property in source.Properties)
                {
                    var propertyPath = AppendProperty(path, property.Name);
                    if (string.IsNullOrWhiteSpace(property.Name))
                    {
                        Throw("CATALOG-MALFORMED", propertyPath, "Localization keys must be nonblank.", "nonblank key", property.Name ?? "null");
                    }

                    if (result.ContainsKey(property.Name))
                    {
                        Throw("CATALOG-MALFORMED", propertyPath, "Duplicate localization key.", "unique ordinal key", property.Name);
                    }

                    result.Add(property.Name, String(property.Value, propertyPath));
                }

                return result;
            }

            private static IList<string> ReadStringArray(JsonValue value, string path)
            {
                var source = value as JsonArray;
                if (source == null)
                {
                    TypeFailure(path, "array", value);
                }

                var result = new List<string>(source.Items.Count);
                for (var index = 0; index < source.Items.Count; index++)
                {
                    result.Add(String(source.Items[index], path + "[" + index.ToString(CultureInfo.InvariantCulture) + "]"));
                }

                return result;
            }

            private static List<T> ReadArray<T>(JsonValue value, string path, Func<JsonValue, string, T> reader) where T : class
            {
                var source = value as JsonArray;
                if (source == null)
                {
                    TypeFailure(path, "array", value);
                }

                var result = new List<T>(source.Items.Count);
                for (var index = 0; index < source.Items.Count; index++)
                {
                    result.Add(reader(source.Items[index], path + "[" + index.ToString(CultureInfo.InvariantCulture) + "]"));
                }

                return result;
            }

            private static PropertyBag Object(JsonValue value, string path, string[] required, string[] optional)
            {
                var source = value as JsonObject;
                if (source == null)
                {
                    TypeFailure(path, "object", value);
                }

                var allowed = new HashSet<string>(required, StringComparer.Ordinal);
                allowed.UnionWith(optional);
                var values = new Dictionary<string, JsonValue>(StringComparer.Ordinal);
                foreach (var property in source.Properties)
                {
                    var propertyPath = AppendProperty(path, property.Name);
                    if (values.ContainsKey(property.Name))
                    {
                        Throw(
                            "CATALOG-MALFORMED",
                            propertyPath,
                            "Duplicate JSON property.",
                            "one ordinal property",
                            property.Name);
                    }

                    if (!allowed.Contains(property.Name))
                    {
                        Throw(
                            "CATALOG-MALFORMED",
                            propertyPath,
                            "Unknown or wrong-case schema property.",
                            string.Join(", ", allowed),
                            property.Name);
                    }

                    values.Add(property.Name, property.Value);
                }

                for (var index = 0; index < required.Length; index++)
                {
                    if (!values.ContainsKey(required[index]))
                    {
                        Throw(
                            "CATALOG-MALFORMED",
                            AppendProperty(path, required[index]),
                            "Required schema property is missing.",
                            required[index],
                            "missing");
                    }
                }

                return new PropertyBag(values);
            }

            private static string String(JsonValue value, string path)
            {
                var typed = value as JsonString;
                if (typed == null)
                {
                    TypeFailure(path, "nonblank string", value);
                }

                if (string.IsNullOrWhiteSpace(typed.Value))
                {
                    Throw("CATALOG-MALFORMED", path, "Schema strings must be nonblank.", "nonblank string", typed.Value ?? "null");
                }

                return typed.Value;
            }

            private static bool Boolean(JsonValue value, string path)
            {
                var typed = value as JsonBoolean;
                if (typed == null)
                {
                    TypeFailure(path, "boolean", value);
                }

                return typed.Value;
            }

            private static int Int32(JsonValue value, string path)
            {
                var parsed = Int64(value, path);
                if (parsed < int.MinValue || parsed > int.MaxValue)
                {
                    Throw("CATALOG-MALFORMED", path, "Integer is outside the Int32 range.", "Int32", parsed.ToString(CultureInfo.InvariantCulture));
                }

                return (int)parsed;
            }

            private static long Int64(JsonValue value, string path)
            {
                var typed = value as JsonNumber;
                if (typed == null)
                {
                    TypeFailure(path, "integer", value);
                }

                if (typed.Lexeme.IndexOf('.') >= 0 || typed.Lexeme.IndexOf('e') >= 0 || typed.Lexeme.IndexOf('E') >= 0)
                {
                    Throw("CATALOG-MALFORMED", path, "Floating-point values are not valid integers.", "base-10 integer", typed.Lexeme);
                }

                long parsed;
                if (!long.TryParse(typed.Lexeme, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out parsed))
                {
                    Throw("CATALOG-MALFORMED", path, "Integer is outside the Int64 range.", "Int64", typed.Lexeme);
                }

                return parsed;
            }

            private static void ValidateUnique<T>(IList<T> items, Func<T, string> key, string path)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (var index = 0; index < items.Count; index++)
                {
                    var id = key(items[index]);
                    if (!seen.Add(id))
                    {
                        Throw(
                            "ID-DUPLICATE",
                            path + "[" + index.ToString(CultureInfo.InvariantCulture) + "].id",
                            "Category IDs must be unique.",
                            "unique ordinal ID",
                            id);
                    }
                }
            }

            private static void ValidateUniqueTransitions(IList<Nvs01Transition> transitions)
            {
                var seen = new HashSet<Nvs01TransitionKey>();
                for (var index = 0; index < transitions.Count; index++)
                {
                    var key = new Nvs01TransitionKey(transitions[index].From, transitions[index].EventId);
                    if (!seen.Add(key))
                    {
                        Throw(
                            "ID-DUPLICATE",
                            "$.transitions[" + index.ToString(CultureInfo.InvariantCulture) + "]",
                            "State/event transition keys must be unique.",
                            "unique from/event pair",
                            transitions[index].From + "/" + transitions[index].EventId);
                    }
                }
            }

            private static void TypeFailure(string path, string expected, JsonValue actual)
            {
                Throw("CATALOG-MALFORMED", path, "JSON value has the wrong schema type.", expected, TypeName(actual));
            }

            private static string TypeName(JsonValue value)
            {
                if (value is JsonObject) return "object";
                if (value is JsonArray) return "array";
                if (value is JsonString) return "string";
                if (value is JsonNumber) return "number";
                if (value is JsonBoolean) return "boolean";
                if (value is JsonNull) return "null";
                return value == null ? "null reference" : value.GetType().Name;
            }

            private static readonly string[] Empty = new string[0];

            private sealed class PropertyBag
            {
                private readonly Dictionary<string, JsonValue> values;

                public PropertyBag(Dictionary<string, JsonValue> values)
                {
                    this.values = values;
                }

                public bool Has(string name)
                {
                    return values.ContainsKey(name);
                }

                public JsonValue Get(string name)
                {
                    return values[name];
                }
            }
        }

        private static class CatalogSemantics
        {
            private static readonly string[] ExpectedRealmIds =
            {
                "crownlands", "stonehold", "eldergrove", "umbral"
            };

            private static readonly string[] ExpectedStateIds =
            {
                "OFFERED", "TALK_TO_VALERIUS", "INVESTIGATE_SKY_CASTLE", "FAILED", "REPORT_TO_VALERIUS", "COMPLETED"
            };

            private static readonly string[] ExpectedObjectiveIds =
            {
                "OBJ_OMEN_1_TALK", "OBJ_OMEN_1_ARENA", "OBJ_OMEN_1_REPORT"
            };

            private static readonly string[] ExpectedDialogueIds =
            {
                "DLG_OMEN_1_OFFER", "DLG_OMEN_1_START", "DLG_OMEN_1_LORE", "DLG_OMEN_1_GO",
                "DLG_OMEN_1_ARENA_START", "DLG_OMEN_1_FAILURE", "DLG_OMEN_1_REPORT", "DLG_OMEN_1_REPORT_CONCLUSION"
            };

            private static readonly string[] ExpectedCapabilityIds =
            {
                "LOCATION_SKY_CASTLE_MARKER", "ACTION_DEPLOY_CHAMPION", "HOOK_SKY_CASTLE_ARENA",
                "EVENT_SKY_CASTLE_ARENA_SUCCESS", "EVENT_SKY_CASTLE_ARENA_FAILURE",
                "EVENT_SKY_CASTLE_ARENA_CANCELLED", "EVENT_SKY_CASTLE_ARENA_UNAVAILABLE",
                "ARTIFACT_CELESTIAL_TEAR", "CH1_REALM_INTRO"
            };

            private static readonly string[] ExpectedConsequenceIds =
            {
                "ACQUIRE_CELESTIAL_TEAR", "GRANT_GOLD_500", "GRANT_VALERIUS_AFFINITY_5", "COMPLETE_OMEN_1", "UNLOCK_REALM_CHAPTER_1"
            };

            private static readonly string[] ExpectedLocalizationKeys =
            {
                "quest.omen1.title", "quest.omen1.description", "npc.valerius.name", "npc.valerius.role.veil_watch_liaison",
                "objective.omen1.talk", "objective.omen1.arena", "objective.omen1.report",
                "dialogue.omen1.offer", "dialogue.omen1.start", "dialogue.omen1.lore", "dialogue.omen1.go",
                "dialogue.omen1.arena_start", "dialogue.omen1.failure", "dialogue.omen1.report",
                "dialogue.omen1.report_conclusion", "choice.omen1.accept", "choice.omen1.decline",
                "choice.omen1.investigate", "choice.omen1.ask_more", "choice.omen1.depart", "choice.omen1.deploy",
                "choice.omen1.retry", "choice.omen1.present_tear", "choice.omen1.continue",
                "artifact.celestial_tear.name", "artifact.celestial_tear.lore", "reward.omen1.gold",
                "reward.omen1.valerius_affinity"
            };

            internal static int ExpectedObjectiveCount =>
                ExpectedObjectiveIds.Length;

            internal static bool IsExpectedStateId(string value) =>
                Contains(ExpectedStateIds, value);

            internal static bool IsExpectedDialogueId(string value) =>
                Contains(ExpectedDialogueIds, value);

            internal static bool IsExpectedObjectiveId(
                int index,
                string value) =>
                index >= 0 &&
                index < ExpectedObjectiveIds.Length &&
                Equal(ExpectedObjectiveIds[index], value);

            internal static bool IsExpectedConsequenceId(string value) =>
                Contains(ExpectedConsequenceIds, value);

            internal static bool IsExpectedRealmId(string value) =>
                Contains(ExpectedRealmIds, value);

            private static bool Contains(
                IEnumerable<string> expected,
                string value)
            {
                foreach (string item in expected)
                {
                    if (Equal(item, value)) return true;
                }

                return false;
            }

            public static void Validate(Nvs01Catalog catalog)
            {
                ValidateSupportedIdentity(catalog);
                ValidateReferences(catalog);
                ValidateStateGraph(catalog);
                ValidateConsequences(catalog);
                ValidateCounts(catalog);
                ValidateExactProfile(catalog);
            }

            private static void ValidateSupportedIdentity(Nvs01Catalog catalog)
            {
                if (catalog.SchemaVersion != Nvs01CatalogContract.SchemaVersion)
                {
                    Throw(
                        "VERSION-UNSUPPORTED",
                        "$.schemaVersion",
                        "Catalog schema version is unsupported.",
                        Nvs01CatalogContract.SchemaVersion.ToString(CultureInfo.InvariantCulture),
                        catalog.SchemaVersion.ToString(CultureInfo.InvariantCulture));
                }

                if (!Equal(catalog.PacketVersion, Nvs01CatalogContract.PacketVersion))
                {
                    Throw(
                        "VERSION-UNSUPPORTED",
                        "$.packetVersion",
                        "Catalog content version is unsupported.",
                        Nvs01CatalogContract.PacketVersion,
                        catalog.PacketVersion);
                }

                Exact(catalog.MilestoneId, Nvs01CatalogContract.MilestoneId, "$.milestoneId");
                Exact(catalog.QuestId, Nvs01CatalogContract.QuestId, "$.questId");
            }

            private static void ValidateCounts(Nvs01Catalog catalog)
            {
                Count(catalog.States.Count, 6, "$.states");
                Count(catalog.Objectives.Count, 3, "$.objectives");
                Count(catalog.Dialogue.Count, 8, "$.dialogue");
                Count(catalog.Transitions.Count, 8, "$.transitions");
                Count(catalog.ExternalCapabilities.Count, 9, "$.externalCapabilities");
                Count(catalog.Consequences.Count, 5, "$.consequences");
                Count(catalog.Localization.Count, 28, "$.localization");
            }

            private static void ValidateReferences(Nvs01Catalog catalog)
            {
                RequireLocalization(catalog, catalog.TitleKey, "$.titleKey");
                RequireLocalization(catalog, catalog.DescriptionKey, "$.descriptionKey");
                RequireLocalization(catalog, catalog.Speaker.NameKey, "$.speaker.nameKey");
                RequireLocalization(catalog, catalog.Speaker.RoleKey, "$.speaker.roleKey");

                var declaredEvents = new HashSet<string>(StringComparer.Ordinal)
                {
                    catalog.Placement.OfferAction
                };
                foreach (var capability in catalog.ExternalCapabilities)
                {
                    declaredEvents.Add(capability.Id);
                }

                foreach (var objective in catalog.Objectives)
                {
                    Nvs01State ignoredState;
                    if (!catalog.TryGetState(objective.ActivatesIn, out ignoredState))
                    {
                        MissingReference("$.objectives." + objective.Id + ".activatesIn", "state", objective.ActivatesIn);
                    }

                    RequireLocalization(catalog, objective.TextKey, "$.objectives." + objective.Id + ".textKey");
                    declaredEvents.Add(objective.CompletesOn);
                }

                foreach (var node in catalog.Dialogue)
                {
                    if (!Equal(node.SpeakerId, catalog.Speaker.Id))
                    {
                        MissingReference("$.dialogue." + node.Id + ".speakerId", "speaker", node.SpeakerId);
                    }

                    RequireLocalization(catalog, node.TextKey, "$.dialogue." + node.Id + ".textKey");
                    if (node.SemanticAction != null) declaredEvents.Add(node.SemanticAction);
                    var choiceKeys = new HashSet<string>(StringComparer.Ordinal);
                    for (var choiceIndex = 0; choiceIndex < node.Choices.Count; choiceIndex++)
                    {
                        var choice = node.Choices[choiceIndex];
                        var choicePath = "$.dialogue." + node.Id + ".choices[" + choiceIndex.ToString(CultureInfo.InvariantCulture) + "]";
                        if (!choiceKeys.Add(choice.Key))
                        {
                            Throw("ID-DUPLICATE", choicePath + ".key", "Choice localization keys must be unique within a node.", "unique ordinal key", choice.Key);
                        }

                        RequireLocalization(catalog, choice.Key, choicePath + ".key");
                        if (choice.Target != null && !Equal(choice.Target, "end"))
                        {
                            Nvs01DialogueNode ignoredNode;
                            if (!catalog.TryGetDialogue(choice.Target, out ignoredNode))
                            {
                                MissingReference(choicePath + ".target", "dialogue node or end", choice.Target);
                            }
                        }

                        if (choice.SemanticAction != null) declaredEvents.Add(choice.SemanticAction);
                    }
                }

                foreach (var node in catalog.Dialogue)
                {
                    declaredEvents.Add(node.Id);
                }

                foreach (var transition in catalog.Transitions)
                {
                    Nvs01State ignoredState;
                    if (!catalog.TryGetState(transition.From, out ignoredState))
                    {
                        MissingReference("$.transitions." + transition.From + ".from", "state", transition.From);
                    }

                    if (!catalog.TryGetState(transition.To, out ignoredState))
                    {
                        MissingReference("$.transitions." + transition.From + ".to", "state", transition.To);
                    }

                    if (!declaredEvents.Contains(transition.EventId))
                    {
                        Throw(
                            "TRANSITION-INVALID",
                            "$.transitions." + transition.From + ".event",
                            "Transition event is not declared by the packet.",
                            "declared objective/action/capability/dialogue event",
                            transition.EventId);
                    }

                    if (transition.Objective != null)
                    {
                        Nvs01Objective ignoredObjective;
                        if (!catalog.TryGetObjective(transition.Objective, out ignoredObjective))
                        {
                            MissingReference("$.transitions." + transition.From + ".objective", "objective", transition.Objective);
                        }
                    }

                    if (transition.Dialogue != null)
                    {
                        Nvs01DialogueNode ignoredNode;
                        if (!catalog.TryGetDialogue(transition.Dialogue, out ignoredNode))
                        {
                            MissingReference("$.transitions." + transition.From + ".dialogue", "dialogue node", transition.Dialogue);
                        }
                    }
                }

                foreach (var objective in catalog.Objectives)
                {
                    var found = false;
                    foreach (var transition in catalog.Transitions)
                    {
                        if (Equal(transition.EventId, objective.CompletesOn))
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        MissingReference("$.objectives." + objective.Id + ".completesOn", "transition event", objective.CompletesOn);
                    }
                }

                Nvs01State resultState;
                if (!catalog.TryGetState(catalog.Abandonment.ResultState, out resultState))
                {
                    MissingReference("$.abandonment.resultState", "state", catalog.Abandonment.ResultState);
                }
            }

            private static void ValidateStateGraph(Nvs01Catalog catalog)
            {
                Nvs01State offered;
                if (!catalog.TryGetState("OFFERED", out offered))
                {
                    MissingReference("$.states", "OFFERED state", "missing");
                }

                var reached = new HashSet<string>(StringComparer.Ordinal) { "OFFERED" };
                var queue = new Queue<string>();
                queue.Enqueue("OFFERED");
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    foreach (var transition in catalog.Transitions)
                    {
                        if (Equal(transition.From, current) && reached.Add(transition.To))
                        {
                            queue.Enqueue(transition.To);
                        }
                    }
                }

                foreach (var state in catalog.States)
                {
                    if (!reached.Contains(state.Id))
                    {
                        Throw(
                            "STATE-UNREACHABLE",
                            "$.states." + state.Id,
                            "Every state must be reachable from OFFERED.",
                            "reachable state",
                            state.Id);
                    }

                    var shouldBeTerminal = Equal(state.Id, "COMPLETED");
                    if (state.Terminal != shouldBeTerminal)
                    {
                        Throw(
                            "TRANSITION-INVALID",
                            "$.states." + state.Id + ".terminal",
                            "COMPLETED alone must be terminal.",
                            shouldBeTerminal.ToString(),
                            state.Terminal.ToString());
                    }

                    var shouldBeTransient = Equal(state.Id, "FAILED");
                    if (state.Transient != shouldBeTransient)
                    {
                        Throw(
                            "TRANSITION-INVALID",
                            "$.states." + state.Id + ".transient",
                            "FAILED alone must be transient.",
                            shouldBeTransient.ToString(),
                            state.Transient.ToString());
                    }
                }

                foreach (var transition in catalog.Transitions)
                {
                    Nvs01State from;
                    if (catalog.TryGetState(transition.From, out from) && from.Terminal)
                    {
                        Throw(
                            "TRANSITION-INVALID",
                            "$.transitions." + transition.From,
                            "Terminal states cannot have outgoing transitions.",
                            "no outgoing transition",
                            transition.EventId);
                    }
                }
            }

            private static void ValidateConsequences(Nvs01Catalog catalog)
            {
                var validTriggers = new HashSet<string>(StringComparer.Ordinal);
                foreach (var transition in catalog.Transitions) validTriggers.Add(transition.EventId);

                var validTargets = new HashSet<string>(StringComparer.Ordinal)
                {
                    catalog.QuestId,
                    catalog.Speaker.Id,
                    "RESOURCE_GOLD"
                };
                foreach (var capability in catalog.ExternalCapabilities) validTargets.Add(capability.Id);

                foreach (var consequence in catalog.Consequences)
                {
                    var path = "$.consequences." + consequence.Id;
                    if (!validTriggers.Contains(consequence.Trigger))
                    {
                        Throw(
                            "REFERENCE-MISSING",
                            path + ".trigger",
                            "Consequence trigger must name one declared transition event.",
                            "declared transition event",
                            consequence.Trigger);
                    }

                    if (!validTargets.Contains(consequence.Target))
                    {
                        Throw(
                            "REFERENCE-MISSING",
                            path + ".target",
                            "Consequence target is unavailable in the supported packet profile.",
                            "quest, speaker, resource, or declared capability",
                            consequence.Target);
                    }

                    if (!Equal(consequence.Repeatability, "once"))
                    {
                        Throw(
                            "TRANSITION-INVALID",
                            path + ".repeatability",
                            "NVS-01 consequences must be exactly-once.",
                            "once",
                            consequence.Repeatability);
                    }

                    if (consequence.Amount.HasValue && consequence.Amount.Value <= 0)
                    {
                        Throw(
                            "TRANSITION-INVALID",
                            path + ".amount",
                            "Consequence amount must be positive.",
                            "positive Int64",
                            consequence.Amount.Value.ToString(CultureInfo.InvariantCulture));
                    }
                }
            }

            private static void ValidateExactProfile(Nvs01Catalog catalog)
            {
                Exact(catalog.TitleKey, "quest.omen1.title", "$.titleKey");
                Exact(catalog.DescriptionKey, "quest.omen1.description", "$.descriptionKey");

                Exact(catalog.Approval.Issue, 138, "$.approval.issue");
                Exact(catalog.Approval.CommentId, 4966062298L, "$.approval.commentId");
                Count(catalog.Approval.Decisions.Count, 16, "$.approval.decisions");
                for (var index = 0; index < 16; index++)
                {
                    Exact(catalog.Approval.Decisions[index], "D" + (index + 1).ToString(CultureInfo.InvariantCulture), "$.approval.decisions[" + index.ToString(CultureInfo.InvariantCulture) + "]");
                }

                Exact(catalog.Placement.ContextId, "POST_REALM_PROLOGUE", "$.placement.contextId");
                ExactSequence(catalog.Placement.EligibleRealmIds, new[] { "crownlands", "stonehold", "eldergrove", "umbral" }, "$.placement.eligibleRealmIds");
                Exact(catalog.Placement.Prerequisite, "ONE_COMMITTED_PLAYABLE_REALM", "$.placement.prerequisite");
                Exact(catalog.Placement.OfferAction, "SELECT_VALERIUS", "$.placement.offerAction");
                Exact(catalog.Placement.AutoAccept, false, "$.placement.autoAccept");
                Exact(catalog.Placement.CompletionUnlockId, "CH1_REALM_INTRO", "$.placement.completionUnlockId");
                Exact(catalog.Placement.CompletionDestination, "CH1_REALM_INTRO", "$.placement.completionDestination");

                Exact(catalog.Speaker.Id, "NPC_VALERIUS", "$.speaker.id");
                Exact(catalog.Speaker.NameKey, "npc.valerius.name", "$.speaker.nameKey");
                Exact(catalog.Speaker.RoleKey, "npc.valerius.role.veil_watch_liaison", "$.speaker.roleKey");

                ExactRecordIds(catalog.States, item => item.Id, ExpectedStateIds, "$.states");
                ExactRecordIds(catalog.Objectives, item => item.Id, ExpectedObjectiveIds, "$.objectives");
                ExactRecordIds(catalog.Dialogue, item => item.Id, ExpectedDialogueIds, "$.dialogue");
                ExactRecordIds(catalog.ExternalCapabilities, item => item.Id, ExpectedCapabilityIds, "$.externalCapabilities");
                ExactRecordIds(catalog.Consequences, item => item.Id, ExpectedConsequenceIds, "$.consequences");

                ValidateExactObjectives(catalog);
                ValidateExactDialogue(catalog);
                ValidateExactTransitions(catalog);
                ValidateExactCapabilities(catalog);
                ValidateExactConsequences(catalog);

                Exact(catalog.Abandonment.AllowedOutsideActiveEncounter, true, "$.abandonment.allowedOutsideActiveEncounter");
                Exact(catalog.Abandonment.ResultState, "OFFERED", "$.abandonment.resultState");
                Exact(catalog.Abandonment.ClearsActiveProgress, true, "$.abandonment.clearsActiveProgress");
                Exact(catalog.Abandonment.ClearsUnearnedConsequences, true, "$.abandonment.clearsUnearnedConsequences");
                Exact(catalog.Abandonment.RetainsEarnedConsequences, true, "$.abandonment.retainsEarnedConsequences");

                foreach (var key in ExpectedLocalizationKeys)
                {
                    RequireLocalization(catalog, key, "$.localization." + key);
                }
            }

            private static void ValidateExactObjectives(Nvs01Catalog catalog)
            {
                var expected = new[]
                {
                    new[] { "OBJ_OMEN_1_TALK", "objective.omen1.talk", "OFFERED", "QUEST_ACCEPTED" },
                    new[] { "OBJ_OMEN_1_ARENA", "objective.omen1.arena", "INVESTIGATE_SKY_CASTLE", "EVENT_SKY_CASTLE_ARENA_SUCCESS" },
                    new[] { "OBJ_OMEN_1_REPORT", "objective.omen1.report", "REPORT_TO_VALERIUS", "DLG_OMEN_1_REPORT_CONCLUSION" }
                };
                for (var index = 0; index < catalog.Objectives.Count; index++)
                {
                    var item = catalog.Objectives[index];
                    var path = "$.objectives[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                    Exact(item.Id, expected[index][0], path + ".id");
                    Exact(item.TextKey, expected[index][1], path + ".textKey");
                    Exact(item.ActivatesIn, expected[index][2], path + ".activatesIn");
                    Exact(item.CompletesOn, expected[index][3], path + ".completesOn");
                }
            }

            private static void ValidateExactDialogue(Nvs01Catalog catalog)
            {
                var textKeys = new[]
                {
                    "dialogue.omen1.offer", "dialogue.omen1.start", "dialogue.omen1.lore", "dialogue.omen1.go",
                    "dialogue.omen1.arena_start", "dialogue.omen1.failure", "dialogue.omen1.report", "dialogue.omen1.report_conclusion"
                };
                var semanticActions = new[] { null, null, null, null, "REQUEST_SKY_CASTLE_ARENA", null, null, null };
                var choices = new[]
                {
                    new[] { "choice.omen1.accept|DLG_OMEN_1_START|", "choice.omen1.decline|end|" },
                    new[] { "choice.omen1.investigate|DLG_OMEN_1_GO|", "choice.omen1.ask_more|DLG_OMEN_1_LORE|" },
                    new[] { "choice.omen1.depart|DLG_OMEN_1_GO|" },
                    new[] { "choice.omen1.deploy|DLG_OMEN_1_ARENA_START|" },
                    new string[0],
                    new[] { "choice.omen1.retry||RETRY_SKY_CASTLE_ARENA" },
                    new[] { "choice.omen1.present_tear|DLG_OMEN_1_REPORT_CONCLUSION|" },
                    new[] { "choice.omen1.continue|end|" }
                };

                for (var index = 0; index < catalog.Dialogue.Count; index++)
                {
                    var item = catalog.Dialogue[index];
                    var path = "$.dialogue[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                    Exact(item.SpeakerId, "NPC_VALERIUS", path + ".speakerId");
                    Exact(item.TextKey, textKeys[index], path + ".textKey");
                    ExactNullable(item.SemanticAction, semanticActions[index], path + ".semanticAction");
                    Count(item.Choices.Count, choices[index].Length, path + ".choices");
                    for (var choiceIndex = 0; choiceIndex < item.Choices.Count; choiceIndex++)
                    {
                        var choice = item.Choices[choiceIndex];
                        var actual = choice.Key + "|" + (choice.Target ?? string.Empty) + "|" + (choice.SemanticAction ?? string.Empty);
                        Exact(actual, choices[index][choiceIndex], path + ".choices[" + choiceIndex.ToString(CultureInfo.InvariantCulture) + "]");
                    }
                }
            }

            private static void ValidateExactTransitions(Nvs01Catalog catalog)
            {
                var expected = new[]
                {
                    "OFFERED|QUEST_ACCEPTED|TALK_TO_VALERIUS|OBJ_OMEN_1_TALK|",
                    "TALK_TO_VALERIUS|REQUEST_SKY_CASTLE_ARENA|INVESTIGATE_SKY_CASTLE|OBJ_OMEN_1_ARENA|",
                    "INVESTIGATE_SKY_CASTLE|EVENT_SKY_CASTLE_ARENA_FAILURE|FAILED||DLG_OMEN_1_FAILURE",
                    "FAILED|RETRY_SKY_CASTLE_ARENA|INVESTIGATE_SKY_CASTLE|OBJ_OMEN_1_ARENA|",
                    "INVESTIGATE_SKY_CASTLE|EVENT_SKY_CASTLE_ARENA_CANCELLED|INVESTIGATE_SKY_CASTLE|OBJ_OMEN_1_ARENA|",
                    "INVESTIGATE_SKY_CASTLE|EVENT_SKY_CASTLE_ARENA_SUCCESS|REPORT_TO_VALERIUS|OBJ_OMEN_1_REPORT|",
                    "REPORT_TO_VALERIUS|SELECT_VALERIUS|REPORT_TO_VALERIUS||DLG_OMEN_1_REPORT",
                    "REPORT_TO_VALERIUS|DLG_OMEN_1_REPORT_CONCLUSION|COMPLETED||"
                };
                for (var index = 0; index < catalog.Transitions.Count; index++)
                {
                    var item = catalog.Transitions[index];
                    var actual = item.From + "|" + item.EventId + "|" + item.To + "|" + (item.Objective ?? string.Empty) + "|" + (item.Dialogue ?? string.Empty);
                    Exact(actual, expected[index], "$.transitions[" + index.ToString(CultureInfo.InvariantCulture) + "]");
                }
            }

            private static void ValidateExactCapabilities(Nvs01Catalog catalog)
            {
                for (var index = 0; index < catalog.ExternalCapabilities.Count; index++)
                {
                    Exact(catalog.ExternalCapabilities[index].Status, "requested", "$.externalCapabilities[" + index.ToString(CultureInfo.InvariantCulture) + "].status");
                }
            }

            private static void ValidateExactConsequences(Nvs01Catalog catalog)
            {
                var expectedTargets = new[] { "ARTIFACT_CELESTIAL_TEAR", "RESOURCE_GOLD", "NPC_VALERIUS", "OMEN_1", "CH1_REALM_INTRO" };
                var expectedTriggers = new[]
                {
                    "EVENT_SKY_CASTLE_ARENA_SUCCESS", "DLG_OMEN_1_REPORT_CONCLUSION", "DLG_OMEN_1_REPORT_CONCLUSION",
                    "DLG_OMEN_1_REPORT_CONCLUSION", "DLG_OMEN_1_REPORT_CONCLUSION"
                };
                var expectedRetained = new bool?[] { true, null, null, null, null };
                var expectedAmounts = new long?[] { null, 500L, 5L, null, null };
                for (var index = 0; index < catalog.Consequences.Count; index++)
                {
                    var item = catalog.Consequences[index];
                    var path = "$.consequences[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                    Exact(item.Target, expectedTargets[index], path + ".target");
                    Exact(item.Trigger, expectedTriggers[index], path + ".trigger");
                    Exact(item.Repeatability, "once", path + ".repeatability");
                    ExactNullable(item.Retained, expectedRetained[index], path + ".retained");
                    ExactNullable(item.Amount, expectedAmounts[index], path + ".amount");
                }
            }

            private static void RequireLocalization(Nvs01Catalog catalog, string key, string path)
            {
                string value;
                if (!catalog.TryGetLocalization(key, out value) || string.IsNullOrWhiteSpace(value))
                {
                    Throw(
                        "REFERENCE-MISSING",
                        path,
                        "Player-facing localization key does not resolve to nonblank text.",
                        "present nonblank localization entry",
                        key);
                }
            }

            private static void MissingReference(string path, string expectedType, string actual)
            {
                Throw("REFERENCE-MISSING", path, "Catalog reference does not resolve.", expectedType, actual);
            }

            private static void Count(int actual, int expected, string path)
            {
                if (actual != expected)
                {
                    Throw(
                        "CATALOG-MALFORMED",
                        path,
                        "Catalog count differs from the exact supported NVS-01 profile.",
                        expected.ToString(CultureInfo.InvariantCulture),
                        actual.ToString(CultureInfo.InvariantCulture));
                }
            }

            private static void ExactRecordIds<T>(IReadOnlyList<T> actual, Func<T, string> selector, string[] expected, string path)
            {
                for (var index = 0; index < expected.Length; index++)
                {
                    Exact(selector(actual[index]), expected[index], path + "[" + index.ToString(CultureInfo.InvariantCulture) + "].id");
                }
            }

            private static void ExactSequence(IReadOnlyList<string> actual, string[] expected, string path)
            {
                Count(actual.Count, expected.Length, path);
                for (var index = 0; index < expected.Length; index++)
                {
                    Exact(actual[index], expected[index], path + "[" + index.ToString(CultureInfo.InvariantCulture) + "]");
                }
            }

            private static void Exact(string actual, string expected, string path)
            {
                if (!Equal(actual, expected))
                {
                    Throw("CATALOG-MALFORMED", path, "Value differs from the exact supported NVS-01 profile.", expected, actual ?? "null");
                }
            }

            private static void Exact(long actual, long expected, string path)
            {
                if (actual != expected)
                {
                    Throw("CATALOG-MALFORMED", path, "Value differs from the exact supported NVS-01 profile.", expected.ToString(CultureInfo.InvariantCulture), actual.ToString(CultureInfo.InvariantCulture));
                }
            }

            private static void Exact(bool actual, bool expected, string path)
            {
                if (actual != expected)
                {
                    Throw("CATALOG-MALFORMED", path, "Value differs from the exact supported NVS-01 profile.", expected.ToString(), actual.ToString());
                }
            }

            private static void ExactNullable(string actual, string expected, string path)
            {
                if (!Equal(actual, expected))
                {
                    Throw("CATALOG-MALFORMED", path, "Optional value differs from the exact supported NVS-01 profile.", expected ?? "absent", actual ?? "absent");
                }
            }

            private static void ExactNullable(bool? actual, bool? expected, string path)
            {
                if (actual != expected)
                {
                    Throw("CATALOG-MALFORMED", path, "Optional value differs from the exact supported NVS-01 profile.", expected.HasValue ? expected.Value.ToString() : "absent", actual.HasValue ? actual.Value.ToString() : "absent");
                }
            }

            private static void ExactNullable(long? actual, long? expected, string path)
            {
                if (actual != expected)
                {
                    Throw(
                        "CATALOG-MALFORMED",
                        path,
                        "Optional value differs from the exact supported NVS-01 profile.",
                        expected.HasValue ? expected.Value.ToString(CultureInfo.InvariantCulture) : "absent",
                        actual.HasValue ? actual.Value.ToString(CultureInfo.InvariantCulture) : "absent");
                }
            }

            private static bool Equal(string left, string right)
            {
                return string.Equals(left, right, StringComparison.Ordinal);
            }
        }

        private static void Throw(string code, string path, string message, string expected, string actual)
        {
            throw new CatalogValidationException(Diagnostic(code, path, message, expected, actual));
        }

        private static string AppendProperty(string path, string property)
        {
            return path + "." + property;
        }
    }
}
