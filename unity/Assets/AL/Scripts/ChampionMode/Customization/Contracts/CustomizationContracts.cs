using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace AL.ChampionMode.Customization.Contracts
{
    public static class CustomizationTechnicalLimits
    {
        public const int MaximumIdLength = 64;
        public const int MaximumContentKeyLength = 160;
        public const int MaximumOptions = 256;
        public const int MaximumPresets = 64;
        public const int MaximumAliases = 128;
        public const int MaximumCapabilities = 256;
        public const int MaximumPlaceholderOptions = 6;
        public const int RequiredVectorComponents = 3;
        public const int SupportedRawStateSchemaVersion = 1;
        public const string ExpectedCatalogId = "character_customization";
        public const string ExpectedFamilyId = "champion_customization";
    }

    public static class CustomizationFamilies
    {
        public const string BodyPresets = "body_presets";
        public const string HairStyles = "hair_styles";
        public const string ArmorStyles = "armor_styles";
        public const string PrimaryColors = "primary_colors";
        public const string HairColors = "hair_colors";
        public const string SkinColors = "skin_colors";
        public const string EyeColors = "eye_colors";
        public const string AccentColors = "accent_colors";
        public const string FaceMarks = "face_marks";
        public const string WeaponStyles = "weapon_styles";
        public const string OffhandStyles = "offhand_styles";

        public static readonly IReadOnlyList<string> Required =
            Array.AsReadOnly(new[]
            {
                BodyPresets,
                HairStyles,
                ArmorStyles,
                PrimaryColors,
                HairColors,
                SkinColors,
                EyeColors,
                AccentColors,
                FaceMarks,
                WeaponStyles,
                OffhandStyles
            });
    }

    [Flags]
    public enum CustomizationField
    {
        None = 0,
        BodyPreset = 1 << 0,
        HairStyle = 1 << 1,
        ArmorStyle = 1 << 2,
        FaceMark = 1 << 3,
        WeaponStyle = 1 << 4,
        OffhandStyle = 1 << 5,
        PrimaryColor = 1 << 6,
        HairColor = 1 << 7,
        SkinColor = 1 << 8,
        EyeColor = 1 << 9,
        AccentColor = 1 << 10,
        CapeEnabled = 1 << 11,
        HelmetEnabled = 1 << 12,
        OptionFields = BodyPreset | HairStyle | ArmorStyle | FaceMark |
                       WeaponStyle | OffhandStyle,
        ColorFields = PrimaryColor | HairColor | SkinColor | EyeColor |
                      AccentColor,
        FlagFields = CapeEnabled | HelmetEnabled,
        All = OptionFields | ColorFields | FlagFields
    }

    public enum CustomizationDiagnosticSeverity
    {
        Error = 0,
        Warning = 1
    }

    public enum CustomizationCatalogAvailability
    {
        Ready = 0,
        Pending = 1,
        Unavailable = 2,
        Invalid = 3
    }

    public enum CustomizationDomainStatus
    {
        Valid = 0,
        ValidLegacyNoMetadata = 1,
        NeedsAliasMigration = 2,
        PreservedUnknown = 3,
        Malformed = 4,
        CatalogPending = 5,
        CatalogUnavailable = 6,
        ModelCapabilityUnavailable = 7,
        FutureSchemaUnsupported = 8
    }

    public enum CustomizationFieldStatus
    {
        RawValidResolved = 0,
        RawValidAliasAvailable = 1,
        RawPreservedUnknown = 2,
        RawBlankInvalid = 3,
        RawNumericInvalid = 4,
        RawUnsupportedFutureSchema = 5,
        EffectivePlaceholder = 6,
        UnavailableMissingCapability = 7,
        UnavailableCatalogPending = 8,
        UnavailableCatalogInvalid = 9
    }

    public enum CustomizationEditKind
    {
        SelectOption = 0,
        SelectExactColor = 1,
        SelectPaletteColor = 2,
        SetFlag = 3,
        ApplyPreset = 4,
        ResetToApprovedDefaults = 5,
        RandomizeWithSeed = 6
    }

    public enum CustomizationEditStatus
    {
        AppliedToDraft = 0,
        NoChange = 1,
        RejectedCatalogPending = 2,
        RejectedCatalogInvalid = 3,
        RejectedUnknownOption = 4,
        RejectedWrongFamily = 5,
        RejectedNumericInvalid = 6,
        RejectedUnavailableCapability = 7,
        RejectedPreservedUnknownReplacementNeedsConfirmation = 8,
        RejectedStaleDraft = 9,
        RejectedDisposed = 10,
        RejectedInvalidRequest = 11
    }

    public enum AppearanceApplyStatus
    {
        AppliedAndVerified = 0,
        RejectedStaleModel = 1,
        RejectedMissingCapability = 2,
        FailedRequiredOperation = 3,
        FailedVerification = 4,
        Disposed = 5
    }

    public enum AppearancePrepareStatus
    {
        Prepared = 0,
        RejectedInvalidDraft = 1,
        RejectedStaleCatalog = 2,
        RejectedStaleModel = 3,
        RejectedMissingCapability = 4
    }

    public enum AppearanceRollbackStatus
    {
        Restored = 0,
        Failed = 1,
        NotApplied = 2,
        Disposed = 3
    }

    public sealed class CustomizationDiagnostic
    {
        public CustomizationDiagnostic(
            string code,
            string fieldPath,
            string recordId,
            CustomizationDiagnosticSeverity severity =
                CustomizationDiagnosticSeverity.Error)
        {
            Code = code ?? string.Empty;
            FieldPath = fieldPath ?? string.Empty;
            RecordId = recordId ?? string.Empty;
            Severity = severity;
        }

        public string Code { get; }
        public string FieldPath { get; }
        public string RecordId { get; }
        public CustomizationDiagnosticSeverity Severity { get; }
    }

    public struct CustomizationColor : IEquatable<CustomizationColor>
    {
        public CustomizationColor(float red, float green, float blue)
        {
            Red = red;
            Green = green;
            Blue = blue;
        }

        public float Red { get; }
        public float Green { get; }
        public float Blue { get; }

        public bool IsFiniteUnitColor =>
            IsFiniteUnit(Red) && IsFiniteUnit(Green) && IsFiniteUnit(Blue);

        public bool Equals(CustomizationColor other)
        {
            return Red.Equals(other.Red) &&
                   Green.Equals(other.Green) &&
                   Blue.Equals(other.Blue);
        }

        public override bool Equals(object value)
        {
            return value is CustomizationColor other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Red.GetHashCode();
                hash = (hash * 397) ^ Green.GetHashCode();
                return (hash * 397) ^ Blue.GetHashCode();
            }
        }

        internal string Canonical()
        {
            return Red.ToString("R", CultureInfo.InvariantCulture) + "," +
                   Green.ToString("R", CultureInfo.InvariantCulture) + "," +
                   Blue.ToString("R", CultureInfo.InvariantCulture);
        }

        private static bool IsFiniteUnit(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) &&
                   value >= 0f && value <= 1f;
        }
    }

    public struct CustomizationScale : IEquatable<CustomizationScale>
    {
        public CustomizationScale(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }

        public bool IsFinitePositive =>
            IsFinitePositiveValue(X) && IsFinitePositiveValue(Y) &&
            IsFinitePositiveValue(Z);

        public bool Equals(CustomizationScale other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
        }

        public override bool Equals(object value)
        {
            return value is CustomizationScale other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = X.GetHashCode();
                hash = (hash * 397) ^ Y.GetHashCode();
                return (hash * 397) ^ Z.GetHashCode();
            }
        }

        internal string Canonical()
        {
            return X.ToString("R", CultureInfo.InvariantCulture) + "," +
                   Y.ToString("R", CultureInfo.InvariantCulture) + "," +
                   Z.ToString("R", CultureInfo.InvariantCulture);
        }

        private static bool IsFinitePositiveValue(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }
    }

    public sealed class CustomizationCatalogIdentity
    {
        public CustomizationCatalogIdentity(
            string gameId,
            string catalogSetId,
            string catalogId,
            string familyId,
            string schemaVersion,
            string contentVersion,
            string sourceRevision,
            string rawSha256,
            string packagedRelativePath)
        {
            GameId = gameId;
            CatalogSetId = catalogSetId;
            CatalogId = catalogId;
            FamilyId = familyId;
            SchemaVersion = schemaVersion;
            ContentVersion = contentVersion;
            SourceRevision = sourceRevision;
            RawSha256 = rawSha256;
            PackagedRelativePath = packagedRelativePath;
        }

        public string GameId { get; }
        public string CatalogSetId { get; }
        public string CatalogId { get; }
        public string FamilyId { get; }
        public string SchemaVersion { get; }
        public string ContentVersion { get; }
        public string SourceRevision { get; }
        public string RawSha256 { get; }
        public string PackagedRelativePath { get; }

        internal string Canonical()
        {
            return string.Join("|", new[]
            {
                GameId, CatalogSetId, CatalogId, FamilyId, SchemaVersion,
                ContentVersion, SourceRevision, RawSha256, PackagedRelativePath
            });
        }
    }

    public sealed class CustomizationValues : IEquatable<CustomizationValues>
    {
        public CustomizationValues(
            string bodyPresetId,
            string hairStyleId,
            string armorStyleId,
            string faceMarkId,
            string weaponStyleId,
            string offhandStyleId,
            CustomizationColor primaryColor,
            CustomizationColor hairColor,
            CustomizationColor skinColor,
            CustomizationColor eyeColor,
            CustomizationColor accentColor,
            bool capeEnabled,
            bool helmetEnabled)
        {
            BodyPresetId = bodyPresetId;
            HairStyleId = hairStyleId;
            ArmorStyleId = armorStyleId;
            FaceMarkId = faceMarkId;
            WeaponStyleId = weaponStyleId;
            OffhandStyleId = offhandStyleId;
            PrimaryColor = primaryColor;
            HairColor = hairColor;
            SkinColor = skinColor;
            EyeColor = eyeColor;
            AccentColor = accentColor;
            CapeEnabled = capeEnabled;
            HelmetEnabled = helmetEnabled;
        }

        public string BodyPresetId { get; }
        public string HairStyleId { get; }
        public string ArmorStyleId { get; }
        public string FaceMarkId { get; }
        public string WeaponStyleId { get; }
        public string OffhandStyleId { get; }
        public CustomizationColor PrimaryColor { get; }
        public CustomizationColor HairColor { get; }
        public CustomizationColor SkinColor { get; }
        public CustomizationColor EyeColor { get; }
        public CustomizationColor AccentColor { get; }
        public bool CapeEnabled { get; }
        public bool HelmetEnabled { get; }

        public string GetOption(CustomizationField field)
        {
            switch (field)
            {
                case CustomizationField.BodyPreset: return BodyPresetId;
                case CustomizationField.HairStyle: return HairStyleId;
                case CustomizationField.ArmorStyle: return ArmorStyleId;
                case CustomizationField.FaceMark: return FaceMarkId;
                case CustomizationField.WeaponStyle: return WeaponStyleId;
                case CustomizationField.OffhandStyle: return OffhandStyleId;
                default: return null;
            }
        }

        public CustomizationColor GetColor(CustomizationField field)
        {
            switch (field)
            {
                case CustomizationField.PrimaryColor: return PrimaryColor;
                case CustomizationField.HairColor: return HairColor;
                case CustomizationField.SkinColor: return SkinColor;
                case CustomizationField.EyeColor: return EyeColor;
                case CustomizationField.AccentColor: return AccentColor;
                default: throw new ArgumentOutOfRangeException(nameof(field));
            }
        }

        public bool GetFlag(CustomizationField field)
        {
            switch (field)
            {
                case CustomizationField.CapeEnabled: return CapeEnabled;
                case CustomizationField.HelmetEnabled: return HelmetEnabled;
                default: throw new ArgumentOutOfRangeException(nameof(field));
            }
        }

        public CustomizationValues WithOption(
            CustomizationField field,
            string value)
        {
            if (!CustomizationFieldMap.IsSingle(field) ||
                (field & CustomizationField.OptionFields) == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(field));
            }

            return new CustomizationValues(
                field == CustomizationField.BodyPreset ? value : BodyPresetId,
                field == CustomizationField.HairStyle ? value : HairStyleId,
                field == CustomizationField.ArmorStyle ? value : ArmorStyleId,
                field == CustomizationField.FaceMark ? value : FaceMarkId,
                field == CustomizationField.WeaponStyle ? value : WeaponStyleId,
                field == CustomizationField.OffhandStyle ? value : OffhandStyleId,
                PrimaryColor, HairColor, SkinColor, EyeColor, AccentColor,
                CapeEnabled, HelmetEnabled);
        }

        public CustomizationValues WithColor(
            CustomizationField field,
            CustomizationColor value)
        {
            if (!CustomizationFieldMap.IsSingle(field) ||
                (field & CustomizationField.ColorFields) == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(field));
            }

            return new CustomizationValues(
                BodyPresetId, HairStyleId, ArmorStyleId, FaceMarkId,
                WeaponStyleId, OffhandStyleId,
                field == CustomizationField.PrimaryColor ? value : PrimaryColor,
                field == CustomizationField.HairColor ? value : HairColor,
                field == CustomizationField.SkinColor ? value : SkinColor,
                field == CustomizationField.EyeColor ? value : EyeColor,
                field == CustomizationField.AccentColor ? value : AccentColor,
                CapeEnabled, HelmetEnabled);
        }

        public CustomizationValues WithFlag(CustomizationField field, bool value)
        {
            if (!CustomizationFieldMap.IsSingle(field) ||
                (field & CustomizationField.FlagFields) == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(field));
            }

            return new CustomizationValues(
                BodyPresetId, HairStyleId, ArmorStyleId, FaceMarkId,
                WeaponStyleId, OffhandStyleId, PrimaryColor, HairColor,
                SkinColor, EyeColor, AccentColor,
                field == CustomizationField.CapeEnabled ? value : CapeEnabled,
                field == CustomizationField.HelmetEnabled ? value : HelmetEnabled);
        }

        public bool Equals(CustomizationValues other)
        {
            return other != null &&
                   string.Equals(BodyPresetId, other.BodyPresetId, StringComparison.Ordinal) &&
                   string.Equals(HairStyleId, other.HairStyleId, StringComparison.Ordinal) &&
                   string.Equals(ArmorStyleId, other.ArmorStyleId, StringComparison.Ordinal) &&
                   string.Equals(FaceMarkId, other.FaceMarkId, StringComparison.Ordinal) &&
                   string.Equals(WeaponStyleId, other.WeaponStyleId, StringComparison.Ordinal) &&
                   string.Equals(OffhandStyleId, other.OffhandStyleId, StringComparison.Ordinal) &&
                   PrimaryColor.Equals(other.PrimaryColor) &&
                   HairColor.Equals(other.HairColor) && SkinColor.Equals(other.SkinColor) &&
                   EyeColor.Equals(other.EyeColor) && AccentColor.Equals(other.AccentColor) &&
                   CapeEnabled == other.CapeEnabled &&
                   HelmetEnabled == other.HelmetEnabled;
        }

        public override bool Equals(object value)
        {
            return Equals(value as CustomizationValues);
        }

        public override int GetHashCode()
        {
            return CustomizationFingerprint.Compute(Canonical()).GetHashCode();
        }

        internal string Canonical()
        {
            return string.Join("|", new[]
            {
                BodyPresetId, HairStyleId, ArmorStyleId, FaceMarkId,
                WeaponStyleId, OffhandStyleId, PrimaryColor.Canonical(),
                HairColor.Canonical(), SkinColor.Canonical(), EyeColor.Canonical(),
                AccentColor.Canonical(), CapeEnabled ? "1" : "0",
                HelmetEnabled ? "1" : "0"
            });
        }
    }

    public sealed class RawCustomizationSnapshot
    {
        public RawCustomizationSnapshot(
            int schemaVersion,
            long revision,
            bool hasCatalogMetadata,
            CustomizationValues values)
        {
            SchemaVersion = schemaVersion;
            Revision = revision;
            HasCatalogMetadata = hasCatalogMetadata;
            Values = values;
            Fingerprint = CustomizationFingerprint.Compute(
                schemaVersion.ToString(CultureInfo.InvariantCulture),
                revision.ToString(CultureInfo.InvariantCulture),
                hasCatalogMetadata ? "1" : "0",
                values?.Canonical());
        }

        public int SchemaVersion { get; }
        public long Revision { get; }
        public bool HasCatalogMetadata { get; }
        public CustomizationValues Values { get; }
        public string Fingerprint { get; }
    }

    public class CustomizationOptionCandidate
    {
        public CustomizationOptionCandidate(
            string familyId,
            string id,
            string contentKey,
            string requiredCapabilityId,
            int order)
        {
            FamilyId = familyId;
            Id = id;
            ContentKey = contentKey;
            RequiredCapabilityId = requiredCapabilityId;
            Order = order;
        }

        public string FamilyId { get; }
        public string Id { get; }
        public string ContentKey { get; }
        public string RequiredCapabilityId { get; }
        public int Order { get; }
    }

    public sealed class CustomizationBodyPresetCandidate : CustomizationOptionCandidate
    {
        private readonly ReadOnlyCollection<float> _scale;

        public CustomizationBodyPresetCandidate(
            string id,
            string contentKey,
            string requiredCapabilityId,
            int order,
            float[] scale)
            : base(CustomizationFamilies.BodyPresets, id, contentKey,
                requiredCapabilityId, order)
        {
            _scale = scale == null
                ? null
                : CustomizationCopies.FreezeBounded(
                    scale,
                    CustomizationTechnicalLimits.RequiredVectorComponents);
        }

        public IReadOnlyList<float> Scale => _scale;
    }

    public sealed class CustomizationColorCandidate : CustomizationOptionCandidate
    {
        private readonly ReadOnlyCollection<float> _rgb;

        public CustomizationColorCandidate(
            string familyId,
            string id,
            string contentKey,
            string requiredCapabilityId,
            int order,
            float[] rgb)
            : base(familyId, id, contentKey, requiredCapabilityId, order)
        {
            _rgb = rgb == null
                ? null
                : CustomizationCopies.FreezeBounded(
                    rgb,
                    CustomizationTechnicalLimits.RequiredVectorComponents);
        }

        public IReadOnlyList<float> Rgb => _rgb;
    }

    public sealed class CustomizationAliasCandidate
    {
        public CustomizationAliasCandidate(
            string familyId,
            string oldId,
            string newId,
            string introducedIn,
            bool requiresUserConfirmation)
        {
            FamilyId = familyId;
            OldId = oldId;
            NewId = newId;
            IntroducedIn = introducedIn;
            RequiresUserConfirmation = requiresUserConfirmation;
        }

        public string FamilyId { get; }
        public string OldId { get; }
        public string NewId { get; }
        public string IntroducedIn { get; }
        public bool RequiresUserConfirmation { get; }
    }

    public sealed class CustomizationPresetCandidate
    {
        private readonly ReadOnlyCollection<string> _requiredCapabilityIds;

        public CustomizationPresetCandidate(
            string id,
            string contentKey,
            CustomizationField fieldMask,
            CustomizationValues values,
            IEnumerable<string> requiredCapabilityIds)
        {
            Id = id;
            ContentKey = contentKey;
            FieldMask = fieldMask;
            Values = values;
            _requiredCapabilityIds = requiredCapabilityIds == null
                ? null
                : CustomizationCopies.FreezeBounded(
                    requiredCapabilityIds,
                    CustomizationTechnicalLimits.MaximumCapabilities);
        }

        public string Id { get; }
        public string ContentKey { get; }
        public CustomizationField FieldMask { get; }
        public CustomizationValues Values { get; }
        public IReadOnlyList<string> RequiredCapabilityIds => _requiredCapabilityIds;
    }

    public sealed class CustomizationPolicyCandidate
    {
        private readonly ReadOnlyDictionary<string, string> _placeholderOptionIds;

        public CustomizationPolicyCandidate(
            CustomizationValues approvedDefaults,
            CustomizationScale minimumBodyScale,
            CustomizationScale maximumBodyScale,
            IReadOnlyDictionary<string, string> placeholderOptionIds,
            bool allowCustomExactColors)
        {
            ApprovedDefaults = approvedDefaults;
            MinimumBodyScale = minimumBodyScale;
            MaximumBodyScale = maximumBodyScale;
            _placeholderOptionIds = CustomizationCopies.FreezeDictionaryBounded(
                placeholderOptionIds,
                CustomizationTechnicalLimits.MaximumPlaceholderOptions,
                out int placeholderEntryCount);
            PlaceholderEntryCount = placeholderEntryCount;
            AllowCustomExactColors = allowCustomExactColors;
        }

        public CustomizationValues ApprovedDefaults { get; }
        public CustomizationScale MinimumBodyScale { get; }
        public CustomizationScale MaximumBodyScale { get; }
        public IReadOnlyDictionary<string, string> PlaceholderOptionIds =>
            _placeholderOptionIds;
        public int PlaceholderEntryCount { get; }
        public bool AllowCustomExactColors { get; }
    }

    public sealed class CustomizationCatalogCandidate
    {
        private readonly ReadOnlyCollection<CustomizationBodyPresetCandidate> _bodyPresets;
        private readonly ReadOnlyCollection<CustomizationOptionCandidate> _options;
        private readonly ReadOnlyCollection<CustomizationColorCandidate> _colors;
        private readonly ReadOnlyCollection<CustomizationAliasCandidate> _aliases;
        private readonly ReadOnlyCollection<CustomizationPresetCandidate> _presets;

        public CustomizationCatalogCandidate(
            CustomizationCatalogIdentity identity,
            IEnumerable<CustomizationBodyPresetCandidate> bodyPresets,
            IEnumerable<CustomizationOptionCandidate> options,
            IEnumerable<CustomizationColorCandidate> colors,
            IEnumerable<CustomizationAliasCandidate> aliases,
            IEnumerable<CustomizationPresetCandidate> presets,
            CustomizationPolicyCandidate policy)
        {
            Identity = identity;
            _bodyPresets = bodyPresets == null
                ? null
                : CustomizationCopies.FreezeBounded(
                    bodyPresets,
                    CustomizationTechnicalLimits.MaximumOptions);
            _options = options == null
                ? null
                : CustomizationCopies.FreezeBounded(
                    options,
                    CustomizationTechnicalLimits.MaximumOptions);
            _colors = colors == null
                ? null
                : CustomizationCopies.FreezeBounded(
                    colors,
                    CustomizationTechnicalLimits.MaximumOptions);
            _aliases = aliases == null
                ? null
                : CustomizationCopies.FreezeBounded(
                    aliases,
                    CustomizationTechnicalLimits.MaximumAliases);
            _presets = presets == null
                ? null
                : CustomizationCopies.FreezeBounded(
                    presets,
                    CustomizationTechnicalLimits.MaximumPresets);
            Policy = policy;
        }

        public CustomizationCatalogIdentity Identity { get; }
        public IReadOnlyList<CustomizationBodyPresetCandidate> BodyPresets =>
            _bodyPresets;
        public IReadOnlyList<CustomizationOptionCandidate> Options => _options;
        public IReadOnlyList<CustomizationColorCandidate> Colors => _colors;
        public IReadOnlyList<CustomizationAliasCandidate> Aliases => _aliases;
        public IReadOnlyList<CustomizationPresetCandidate> Presets => _presets;
        public CustomizationPolicyCandidate Policy { get; }
    }

    public class CustomizationOptionDefinition
    {
        public CustomizationOptionDefinition(
            string familyId,
            string id,
            string contentKey,
            string requiredCapabilityId,
            int order)
        {
            FamilyId = familyId;
            Id = id;
            ContentKey = contentKey;
            RequiredCapabilityId = requiredCapabilityId;
            Order = order;
        }

        public string FamilyId { get; }
        public string Id { get; }
        public string ContentKey { get; }
        public string RequiredCapabilityId { get; }
        public int Order { get; }

        internal virtual string Canonical()
        {
            return string.Join("|", new[]
            {
                FamilyId, Id, ContentKey, RequiredCapabilityId,
                Order.ToString(CultureInfo.InvariantCulture)
            });
        }
    }

    public sealed class CustomizationBodyPresetDefinition : CustomizationOptionDefinition
    {
        public CustomizationBodyPresetDefinition(
            string id,
            string contentKey,
            string requiredCapabilityId,
            int order,
            CustomizationScale scale)
            : base(CustomizationFamilies.BodyPresets, id, contentKey,
                requiredCapabilityId, order)
        {
            Scale = scale;
        }

        public CustomizationScale Scale { get; }

        internal override string Canonical()
        {
            return base.Canonical() + "|" + Scale.Canonical();
        }
    }

    public sealed class CustomizationColorDefinition : CustomizationOptionDefinition
    {
        public CustomizationColorDefinition(
            string familyId,
            string id,
            string contentKey,
            string requiredCapabilityId,
            int order,
            CustomizationColor color)
            : base(familyId, id, contentKey, requiredCapabilityId, order)
        {
            Color = color;
        }

        public CustomizationColor Color { get; }

        internal override string Canonical()
        {
            return base.Canonical() + "|" + Color.Canonical();
        }
    }

    public sealed class CustomizationAliasDefinition
    {
        public CustomizationAliasDefinition(
            string familyId,
            string oldId,
            string newId,
            string introducedIn,
            bool requiresUserConfirmation)
        {
            FamilyId = familyId;
            OldId = oldId;
            NewId = newId;
            IntroducedIn = introducedIn;
            RequiresUserConfirmation = requiresUserConfirmation;
        }

        public string FamilyId { get; }
        public string OldId { get; }
        public string NewId { get; }
        public string IntroducedIn { get; }
        public bool RequiresUserConfirmation { get; }

        internal string Canonical()
        {
            return string.Join("|", new[]
            {
                FamilyId, OldId, NewId, IntroducedIn,
                RequiresUserConfirmation ? "1" : "0"
            });
        }
    }

    public sealed class CustomizationPresetDefinition
    {
        private readonly ReadOnlyCollection<string> _requiredCapabilities;

        public CustomizationPresetDefinition(
            string id,
            string contentKey,
            CustomizationField fieldMask,
            CustomizationValues values,
            IEnumerable<string> requiredCapabilities)
        {
            Id = id;
            ContentKey = contentKey;
            FieldMask = fieldMask;
            Values = values;
            _requiredCapabilities = CustomizationCopies.Freeze(requiredCapabilities);
        }

        public string Id { get; }
        public string ContentKey { get; }
        public CustomizationField FieldMask { get; }
        public CustomizationValues Values { get; }
        public IReadOnlyList<string> RequiredCapabilities => _requiredCapabilities;

        internal string Canonical()
        {
            return Id + "|" + ContentKey + "|" + ((int)FieldMask).ToString(
                       CultureInfo.InvariantCulture) + "|" + Values.Canonical() +
                   "|" + string.Join(",", _requiredCapabilities);
        }
    }

    public sealed class CustomizationPolicySnapshot
    {
        private readonly ReadOnlyDictionary<string, string> _placeholders;

        public CustomizationPolicySnapshot(
            CustomizationValues approvedDefaults,
            CustomizationScale minimumBodyScale,
            CustomizationScale maximumBodyScale,
            IDictionary<string, string> placeholders,
            bool allowCustomExactColors)
        {
            ApprovedDefaults = approvedDefaults;
            MinimumBodyScale = minimumBodyScale;
            MaximumBodyScale = maximumBodyScale;
            _placeholders = new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(placeholders, StringComparer.Ordinal));
            AllowCustomExactColors = allowCustomExactColors;
        }

        public CustomizationValues ApprovedDefaults { get; }
        public CustomizationScale MinimumBodyScale { get; }
        public CustomizationScale MaximumBodyScale { get; }
        public IReadOnlyDictionary<string, string> PlaceholderOptionIds => _placeholders;
        public bool AllowCustomExactColors { get; }

        public bool TryGetPlaceholder(string familyId, out string optionId)
        {
            return _placeholders.TryGetValue(familyId, out optionId);
        }

        internal string Canonical()
        {
            return ApprovedDefaults.Canonical() + "|" +
                   MinimumBodyScale.Canonical() + "|" +
                   MaximumBodyScale.Canonical() + "|" +
                   string.Join(",", _placeholders.OrderBy(item => item.Key,
                       StringComparer.Ordinal).Select(item => item.Key + "=" + item.Value)) +
                   "|" + (AllowCustomExactColors ? "1" : "0");
        }
    }

    public sealed class CustomizationCatalogSnapshot
    {
        private readonly ReadOnlyCollection<CustomizationBodyPresetDefinition> _bodyPresets;
        private readonly ReadOnlyCollection<CustomizationOptionDefinition> _options;
        private readonly ReadOnlyCollection<CustomizationColorDefinition> _colors;
        private readonly ReadOnlyCollection<CustomizationAliasDefinition> _aliases;
        private readonly ReadOnlyCollection<CustomizationPresetDefinition> _presets;
        private readonly Dictionary<string, CustomizationOptionDefinition> _optionIndex;
        private readonly Dictionary<string, CustomizationAliasDefinition> _aliasIndex;
        private readonly Dictionary<string, CustomizationPresetDefinition> _presetIndex;

        internal CustomizationCatalogSnapshot(
            CustomizationCatalogIdentity identity,
            IEnumerable<CustomizationBodyPresetDefinition> bodyPresets,
            IEnumerable<CustomizationOptionDefinition> options,
            IEnumerable<CustomizationColorDefinition> colors,
            IEnumerable<CustomizationAliasDefinition> aliases,
            IEnumerable<CustomizationPresetDefinition> presets,
            CustomizationPolicySnapshot policy)
        {
            Identity = identity;
            _bodyPresets = CustomizationCopies.Freeze(bodyPresets);
            _options = CustomizationCopies.Freeze(options);
            _colors = CustomizationCopies.Freeze(colors);
            _aliases = CustomizationCopies.Freeze(aliases);
            _presets = CustomizationCopies.Freeze(presets);
            Policy = policy;
            _optionIndex = new Dictionary<string, CustomizationOptionDefinition>(
                StringComparer.Ordinal);
            foreach (CustomizationOptionDefinition item in _bodyPresets.Cast<CustomizationOptionDefinition>()
                         .Concat(_options).Concat(_colors))
            {
                _optionIndex.Add(Key(item.FamilyId, item.Id), item);
            }

            _aliasIndex = _aliases.ToDictionary(
                item => Key(item.FamilyId, item.OldId),
                item => item,
                StringComparer.Ordinal);
            _presetIndex = _presets.ToDictionary(
                item => item.Id,
                item => item,
                StringComparer.Ordinal);
            Fingerprint = CustomizationFingerprint.Compute(
                Identity.Canonical(),
                string.Join("\n", _bodyPresets.Select(item => item.Canonical())),
                string.Join("\n", _options.Select(item => item.Canonical())),
                string.Join("\n", _colors.Select(item => item.Canonical())),
                string.Join("\n", _aliases.Select(item => item.Canonical())),
                string.Join("\n", _presets.Select(item => item.Canonical())),
                Policy.Canonical());
        }

        public CustomizationCatalogIdentity Identity { get; }
        public IReadOnlyList<CustomizationBodyPresetDefinition> BodyPresets => _bodyPresets;
        public IReadOnlyList<CustomizationOptionDefinition> Options => _options;
        public IReadOnlyList<CustomizationColorDefinition> Colors => _colors;
        public IReadOnlyList<CustomizationAliasDefinition> Aliases => _aliases;
        public IReadOnlyList<CustomizationPresetDefinition> Presets => _presets;
        public CustomizationPolicySnapshot Policy { get; }
        public string Fingerprint { get; }

        public bool TryGetOption(
            string familyId,
            string optionId,
            out CustomizationOptionDefinition option)
        {
            return _optionIndex.TryGetValue(Key(familyId, optionId), out option);
        }

        public bool TryGetAlias(
            string familyId,
            string oldId,
            out CustomizationAliasDefinition alias)
        {
            return _aliasIndex.TryGetValue(Key(familyId, oldId), out alias);
        }

        public bool TryGetPreset(string presetId, out CustomizationPresetDefinition preset)
        {
            return _presetIndex.TryGetValue(presetId ?? string.Empty, out preset);
        }

        public IReadOnlyList<CustomizationOptionDefinition> GetOptions(string familyId)
        {
            return _bodyPresets.Cast<CustomizationOptionDefinition>()
                .Concat(_options).Concat(_colors)
                .Where(item => string.Equals(item.FamilyId, familyId,
                    StringComparison.Ordinal))
                .OrderBy(item => item.Order)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .ToArray();
        }

        private static string Key(string familyId, string id)
        {
            return (familyId ?? string.Empty) + "\u001f" + (id ?? string.Empty);
        }
    }

    public sealed class CustomizationCatalogValidationResult
    {
        private readonly ReadOnlyCollection<CustomizationDiagnostic> _diagnostics;

        public CustomizationCatalogValidationResult(
            CustomizationCatalogSnapshot snapshot,
            IEnumerable<CustomizationDiagnostic> diagnostics)
        {
            Snapshot = snapshot;
            _diagnostics = CustomizationCopies.FreezeDiagnostics(diagnostics);
        }

        public CustomizationCatalogSnapshot Snapshot { get; }
        public IReadOnlyList<CustomizationDiagnostic> Diagnostics => _diagnostics;
        public bool IsValid => Snapshot != null && _diagnostics.All(
            item => item.Severity != CustomizationDiagnosticSeverity.Error);
    }

    public sealed class CustomizationFieldCompatibility
    {
        public CustomizationFieldCompatibility(
            CustomizationField field,
            CustomizationFieldStatus status,
            string rawId,
            string resolvedId)
        {
            Field = field;
            Status = status;
            RawId = rawId;
            ResolvedId = resolvedId;
        }

        public CustomizationField Field { get; }
        public CustomizationFieldStatus Status { get; }
        public string RawId { get; }
        public string ResolvedId { get; }
    }

    public sealed class CustomizationCompatibilityResult
    {
        private readonly ReadOnlyDictionary<CustomizationField, CustomizationFieldCompatibility> _fields;
        private readonly ReadOnlyCollection<CustomizationDiagnostic> _diagnostics;

        public CustomizationCompatibilityResult(
            CustomizationDomainStatus status,
            RawCustomizationSnapshot raw,
            IDictionary<CustomizationField, CustomizationFieldCompatibility> fields,
            IEnumerable<CustomizationDiagnostic> diagnostics)
        {
            Status = status;
            Raw = raw;
            _fields = new ReadOnlyDictionary<CustomizationField, CustomizationFieldCompatibility>(
                new Dictionary<CustomizationField, CustomizationFieldCompatibility>(fields));
            _diagnostics = CustomizationCopies.FreezeDiagnostics(diagnostics);
        }

        public CustomizationDomainStatus Status { get; }
        public RawCustomizationSnapshot Raw { get; }
        public IReadOnlyDictionary<CustomizationField, CustomizationFieldCompatibility> Fields => _fields;
        public IReadOnlyList<CustomizationDiagnostic> Diagnostics => _diagnostics;
    }

    public sealed class ModelCapabilitySnapshot
    {
        private readonly ReadOnlyCollection<string> _capabilities;
        private readonly HashSet<string> _capabilityIndex;

        internal ModelCapabilitySnapshot(
            string capabilityId,
            long revision,
            string sourceIdentity,
            CustomizationField supportedFields,
            IEnumerable<string> capabilities)
        {
            CapabilityId = capabilityId;
            Revision = revision;
            SourceIdentity = sourceIdentity;
            SupportedFields = supportedFields;
            string[] copy = CustomizationCopies.FreezeBounded(
                    capabilities,
                    CustomizationTechnicalLimits.MaximumCapabilities)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
            _capabilities = Array.AsReadOnly(copy);
            _capabilityIndex = new HashSet<string>(copy, StringComparer.Ordinal);
            Fingerprint = CustomizationFingerprint.Compute(
                capabilityId,
                revision.ToString(CultureInfo.InvariantCulture),
                sourceIdentity,
                ((int)supportedFields).ToString(CultureInfo.InvariantCulture),
                string.Join("\n", copy));
        }

        public string CapabilityId { get; }
        public long Revision { get; }
        public string SourceIdentity { get; }
        public CustomizationField SupportedFields { get; }
        public IReadOnlyList<string> Capabilities => _capabilities;
        public string Fingerprint { get; }

        public bool Supports(CustomizationField field, string capabilityId)
        {
            return (SupportedFields & field) == field &&
                   (string.IsNullOrEmpty(capabilityId) ||
                    _capabilityIndex.Contains(capabilityId));
        }
    }

    public sealed class ModelCapabilityCandidate
    {
        private readonly ReadOnlyCollection<string> _capabilities;

        public ModelCapabilityCandidate(
            string capabilityId,
            long revision,
            string sourceIdentity,
            CustomizationField supportedFields,
            IEnumerable<string> capabilities)
        {
            CapabilityId = capabilityId;
            Revision = revision;
            SourceIdentity = sourceIdentity;
            SupportedFields = supportedFields;
            _capabilities = capabilities == null
                ? null
                : CustomizationCopies.FreezeBounded(
                    capabilities,
                    CustomizationTechnicalLimits.MaximumCapabilities);
        }

        public string CapabilityId { get; }
        public long Revision { get; }
        public string SourceIdentity { get; }
        public CustomizationField SupportedFields { get; }
        public IReadOnlyList<string> Capabilities => _capabilities;
    }

    public sealed class ModelCapabilityValidationResult
    {
        private readonly ReadOnlyCollection<CustomizationDiagnostic> _diagnostics;

        public ModelCapabilityValidationResult(
            ModelCapabilitySnapshot snapshot,
            IEnumerable<CustomizationDiagnostic> diagnostics)
        {
            Snapshot = snapshot;
            _diagnostics = CustomizationCopies.FreezeDiagnostics(diagnostics);
        }

        public ModelCapabilitySnapshot Snapshot { get; }
        public IReadOnlyList<CustomizationDiagnostic> Diagnostics => _diagnostics;
        public bool IsValid => Snapshot != null && _diagnostics.All(
            item => item.Severity != CustomizationDiagnosticSeverity.Error);
    }

    public sealed class EffectiveAppearanceSnapshot
    {
        private readonly ReadOnlyDictionary<CustomizationField, CustomizationFieldStatus> _statuses;

        public EffectiveAppearanceSnapshot(
            CustomizationValues values,
            CustomizationScale bodyScale,
            IDictionary<CustomizationField, CustomizationFieldStatus> statuses,
            string catalogFingerprint,
            string modelFingerprint,
            long rawRevision)
        {
            Values = values;
            BodyScale = bodyScale;
            _statuses = new ReadOnlyDictionary<CustomizationField, CustomizationFieldStatus>(
                new Dictionary<CustomizationField, CustomizationFieldStatus>(statuses));
            CatalogFingerprint = catalogFingerprint;
            ModelFingerprint = modelFingerprint;
            RawRevision = rawRevision;
            Fingerprint = CustomizationFingerprint.Compute(
                values?.Canonical(), bodyScale.Canonical(), catalogFingerprint,
                modelFingerprint, rawRevision.ToString(CultureInfo.InvariantCulture),
                string.Join(",", _statuses.OrderBy(item => (int)item.Key)
                    .Select(item => ((int)item.Key).ToString(CultureInfo.InvariantCulture) +
                                    "=" + ((int)item.Value).ToString(CultureInfo.InvariantCulture))));
        }

        public CustomizationValues Values { get; }
        public CustomizationScale BodyScale { get; }
        public IReadOnlyDictionary<CustomizationField, CustomizationFieldStatus> FieldStatuses => _statuses;
        public string CatalogFingerprint { get; }
        public string ModelFingerprint { get; }
        public long RawRevision { get; }
        public string Fingerprint { get; }
    }

    public sealed class CustomizationQueryResult
    {
        private readonly ReadOnlyCollection<CustomizationDiagnostic> _diagnostics;

        public CustomizationQueryResult(
            CustomizationDomainStatus status,
            RawCustomizationSnapshot rawCommitted,
            EffectiveAppearanceSnapshot effectivePresentation,
            CustomizationCatalogIdentity catalog,
            ModelCapabilitySnapshot model,
            IEnumerable<CustomizationDiagnostic> diagnostics)
        {
            Status = status;
            RawCommitted = rawCommitted;
            EffectivePresentation = effectivePresentation;
            Catalog = catalog;
            Model = model;
            _diagnostics = CustomizationCopies.FreezeDiagnostics(diagnostics);
        }

        public CustomizationDomainStatus Status { get; }
        public RawCustomizationSnapshot RawCommitted { get; }
        public EffectiveAppearanceSnapshot EffectivePresentation { get; }
        public CustomizationCatalogIdentity Catalog { get; }
        public ModelCapabilitySnapshot Model { get; }
        public IReadOnlyList<CustomizationDiagnostic> Diagnostics => _diagnostics;
    }

    public sealed class CustomizationDraft
    {
        public CustomizationDraft(
            string draftId,
            long baseRawRevision,
            string baseCatalogFingerprint,
            string baseModelFingerprint,
            EffectiveAppearanceSnapshot baseEffective,
            CustomizationValues baseRaw,
            CustomizationValues proposedRaw,
            CustomizationField changedFields,
            CustomizationField basePreservedUnknownFields,
            string provenance)
        {
            DraftId = draftId;
            BaseRawRevision = baseRawRevision;
            BaseCatalogFingerprint = baseCatalogFingerprint;
            BaseModelFingerprint = baseModelFingerprint;
            BaseEffective = baseEffective;
            BaseRaw = baseRaw;
            ProposedRaw = proposedRaw;
            ChangedFields = changedFields;
            BasePreservedUnknownFields = basePreservedUnknownFields;
            PreservedUnknownFields = basePreservedUnknownFields & ~changedFields;
            Provenance = provenance ?? string.Empty;
            ProposedRawFingerprint = CustomizationFingerprint.Compute(
                proposedRaw?.Canonical());
            Fingerprint = CustomizationFingerprint.Compute(
                draftId,
                baseRawRevision.ToString(CultureInfo.InvariantCulture),
                baseCatalogFingerprint,
                baseModelFingerprint,
                baseRaw?.Canonical(),
                proposedRaw?.Canonical(),
                ((int)changedFields).ToString(CultureInfo.InvariantCulture),
                ((int)basePreservedUnknownFields).ToString(
                    CultureInfo.InvariantCulture),
                Provenance);
        }

        public string DraftId { get; }
        public long BaseRawRevision { get; }
        public string BaseCatalogFingerprint { get; }
        public string BaseModelFingerprint { get; }
        public EffectiveAppearanceSnapshot BaseEffective { get; }
        public CustomizationValues BaseRaw { get; }
        public CustomizationValues ProposedRaw { get; }
        public CustomizationField ChangedFields { get; }
        public CustomizationField BasePreservedUnknownFields { get; }
        public CustomizationField PreservedUnknownFields { get; }
        public string Provenance { get; }
        public string ProposedRawFingerprint { get; }
        public string Fingerprint { get; }
    }

    public sealed class CustomizationEditRequest
    {
        private CustomizationEditRequest(
            CustomizationEditKind kind,
            CustomizationField field,
            string valueId,
            CustomizationColor color,
            bool flagValue,
            long seed,
            CustomizationField allowedFields,
            bool confirmPreservedUnknownReplacement)
        {
            Kind = kind;
            Field = field;
            ValueId = valueId;
            Color = color;
            FlagValue = flagValue;
            Seed = seed;
            AllowedFields = allowedFields;
            ConfirmPreservedUnknownReplacement = confirmPreservedUnknownReplacement;
        }

        public CustomizationEditKind Kind { get; }
        public CustomizationField Field { get; }
        public string ValueId { get; }
        public CustomizationColor Color { get; }
        public bool FlagValue { get; }
        public long Seed { get; }
        public CustomizationField AllowedFields { get; }
        public bool ConfirmPreservedUnknownReplacement { get; }

        public static CustomizationEditRequest SelectOption(
            CustomizationField field,
            string optionId,
            bool confirmUnknown = false)
        {
            return new CustomizationEditRequest(
                CustomizationEditKind.SelectOption, field, optionId, default,
                false, 0L, CustomizationField.None, confirmUnknown);
        }

        public static CustomizationEditRequest SelectExactColor(
            CustomizationField field,
            CustomizationColor color)
        {
            return new CustomizationEditRequest(
                CustomizationEditKind.SelectExactColor, field, null, color,
                false, 0L, CustomizationField.None, false);
        }

        public static CustomizationEditRequest SelectPaletteColor(
            CustomizationField field,
            string optionId)
        {
            return new CustomizationEditRequest(
                CustomizationEditKind.SelectPaletteColor, field, optionId,
                default, false, 0L, CustomizationField.None, false);
        }

        public static CustomizationEditRequest SetFlag(
            CustomizationField field,
            bool value)
        {
            return new CustomizationEditRequest(
                CustomizationEditKind.SetFlag, field, null, default, value,
                0L, CustomizationField.None, false);
        }

        public static CustomizationEditRequest ApplyPreset(
            string presetId,
            bool confirmUnknown = false)
        {
            return new CustomizationEditRequest(
                CustomizationEditKind.ApplyPreset, CustomizationField.None,
                presetId, default, false, 0L, CustomizationField.None,
                confirmUnknown);
        }

        public static CustomizationEditRequest Reset(bool confirmUnknown = false)
        {
            return new CustomizationEditRequest(
                CustomizationEditKind.ResetToApprovedDefaults,
                CustomizationField.None, null, default, false, 0L,
                CustomizationField.All, confirmUnknown);
        }

        public static CustomizationEditRequest Randomize(
            long seed,
            CustomizationField allowedFields,
            bool confirmUnknown = false)
        {
            return new CustomizationEditRequest(
                CustomizationEditKind.RandomizeWithSeed,
                CustomizationField.None, null, default, false, seed,
                allowedFields, confirmUnknown);
        }
    }

    public sealed class CustomizationEditResult
    {
        private readonly ReadOnlyCollection<CustomizationDiagnostic> _diagnostics;

        public CustomizationEditResult(
            CustomizationEditStatus status,
            CustomizationDraft draft,
            IEnumerable<CustomizationDiagnostic> diagnostics)
        {
            Status = status;
            Draft = draft;
            _diagnostics = CustomizationCopies.FreezeDiagnostics(diagnostics);
        }

        public CustomizationEditStatus Status { get; }
        public CustomizationDraft Draft { get; }
        public IReadOnlyList<CustomizationDiagnostic> Diagnostics => _diagnostics;
    }

    public sealed class AppearanceOperation
    {
        public AppearanceOperation(
            CustomizationField field,
            string requiredCapabilityId,
            bool required)
        {
            Field = field;
            RequiredCapabilityId = requiredCapabilityId;
            Required = required;
        }

        public CustomizationField Field { get; }
        public string RequiredCapabilityId { get; }
        public bool Required { get; }
    }

    public sealed class AppearancePlan
    {
        private readonly ReadOnlyCollection<AppearanceOperation> _operations;

        public AppearancePlan(
            string planId,
            string modelFingerprint,
            EffectiveAppearanceSnapshot prior,
            EffectiveAppearanceSnapshot proposed,
            string proposedRawFingerprint,
            IEnumerable<AppearanceOperation> operations)
        {
            PlanId = planId;
            ModelFingerprint = modelFingerprint;
            Prior = prior;
            Proposed = proposed;
            ProposedRawFingerprint = proposedRawFingerprint;
            _operations = CustomizationCopies.Freeze(operations);
            PlanHash = CustomizationFingerprint.Compute(
                planId, modelFingerprint, prior?.Fingerprint,
                proposed?.Fingerprint, proposedRawFingerprint,
                string.Join(",", _operations.Select(item =>
                    ((int)item.Field).ToString(CultureInfo.InvariantCulture) +
                    ":" + item.RequiredCapabilityId + ":" +
                    (item.Required ? "1" : "0"))));
        }

        public string PlanId { get; }
        public string ModelFingerprint { get; }
        public EffectiveAppearanceSnapshot Prior { get; }
        public EffectiveAppearanceSnapshot Proposed { get; }
        public string ProposedRawFingerprint { get; }
        public IReadOnlyList<AppearanceOperation> Operations => _operations;
        public string PlanHash { get; }
    }

    public sealed class AppearancePrepareResult
    {
        private readonly ReadOnlyCollection<CustomizationDiagnostic> _diagnostics;

        public AppearancePrepareResult(
            AppearancePrepareStatus status,
            AppearancePlan plan,
            IEnumerable<CustomizationDiagnostic> diagnostics)
        {
            Status = status;
            Plan = plan;
            _diagnostics = CustomizationCopies.FreezeDiagnostics(diagnostics);
        }

        public AppearancePrepareStatus Status { get; }
        public AppearancePlan Plan { get; }
        public IReadOnlyList<CustomizationDiagnostic> Diagnostics => _diagnostics;
    }

    public sealed class CustomizationCommitPlan
    {
        public CustomizationCommitPlan(
            string operationId,
            CustomizationDraft draft,
            AppearancePlan appearancePlan,
            long expectedSaveCandidateRevision)
        {
            OperationId = operationId;
            Draft = draft;
            AppearancePlan = appearancePlan;
            ExpectedSaveCandidateRevision = expectedSaveCandidateRevision;
            PlanHash = CustomizationFingerprint.Compute(
                operationId, draft?.Fingerprint, appearancePlan?.PlanHash,
                expectedSaveCandidateRevision.ToString(CultureInfo.InvariantCulture));
        }

        public string OperationId { get; }
        public CustomizationDraft Draft { get; }
        public AppearancePlan AppearancePlan { get; }
        public long ExpectedSaveCandidateRevision { get; }
        public string PlanHash { get; }
    }

    public sealed class CustomizationCommittedEvent
    {
        public CustomizationCommittedEvent(
            string operationId,
            string planHash,
            long rawRevision,
            string catalogFingerprint,
            string modelFingerprint)
        {
            OperationId = operationId;
            PlanHash = planHash;
            RawRevision = rawRevision;
            CatalogFingerprint = catalogFingerprint;
            ModelFingerprint = modelFingerprint;
        }

        public string OperationId { get; }
        public string PlanHash { get; }
        public long RawRevision { get; }
        public string CatalogFingerprint { get; }
        public string ModelFingerprint { get; }
    }

    public static class CustomizationFieldMap
    {
        private static readonly CustomizationField[] OrderedFields =
        {
            CustomizationField.BodyPreset,
            CustomizationField.HairStyle,
            CustomizationField.ArmorStyle,
            CustomizationField.FaceMark,
            CustomizationField.WeaponStyle,
            CustomizationField.OffhandStyle,
            CustomizationField.PrimaryColor,
            CustomizationField.HairColor,
            CustomizationField.SkinColor,
            CustomizationField.EyeColor,
            CustomizationField.AccentColor,
            CustomizationField.CapeEnabled,
            CustomizationField.HelmetEnabled
        };

        public static IReadOnlyList<CustomizationField> Enumerate(CustomizationField mask)
        {
            return OrderedFields.Where(field => (mask & field) == field).ToArray();
        }

        public static string Family(CustomizationField field)
        {
            switch (field)
            {
                case CustomizationField.BodyPreset: return CustomizationFamilies.BodyPresets;
                case CustomizationField.HairStyle: return CustomizationFamilies.HairStyles;
                case CustomizationField.ArmorStyle: return CustomizationFamilies.ArmorStyles;
                case CustomizationField.FaceMark: return CustomizationFamilies.FaceMarks;
                case CustomizationField.WeaponStyle: return CustomizationFamilies.WeaponStyles;
                case CustomizationField.OffhandStyle: return CustomizationFamilies.OffhandStyles;
                case CustomizationField.PrimaryColor: return CustomizationFamilies.PrimaryColors;
                case CustomizationField.HairColor: return CustomizationFamilies.HairColors;
                case CustomizationField.SkinColor: return CustomizationFamilies.SkinColors;
                case CustomizationField.EyeColor: return CustomizationFamilies.EyeColors;
                case CustomizationField.AccentColor: return CustomizationFamilies.AccentColors;
                default: return string.Empty;
            }
        }

        public static bool IsSingle(CustomizationField field)
        {
            int value = (int)field;
            return value != 0 && (value & (value - 1)) == 0 &&
                   (field & CustomizationField.All) == field;
        }
    }

    internal static class CustomizationCopies
    {
        public static ReadOnlyCollection<T> Freeze<T>(IEnumerable<T> values)
        {
            return Array.AsReadOnly((values ?? Array.Empty<T>()).ToArray());
        }

        public static ReadOnlyCollection<T> FreezeBounded<T>(
            IEnumerable<T> values,
            int maximum)
        {
            if (maximum < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximum));
            }

            var copy = new List<T>(Math.Min(maximum, 256) + 1);
            using (IEnumerator<T> enumerator =
                   (values ?? Array.Empty<T>()).GetEnumerator())
            {
                while (copy.Count <= maximum && enumerator.MoveNext())
                {
                    copy.Add(enumerator.Current);
                }
            }

            return Array.AsReadOnly(copy.ToArray());
        }

        public static ReadOnlyDictionary<string, string> FreezeDictionaryBounded(
            IReadOnlyDictionary<string, string> values,
            int maximum,
            out int entryCount)
        {
            entryCount = 0;
            if (values == null)
            {
                return null;
            }

            if (maximum < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximum));
            }

            var copy = new Dictionary<string, string>(StringComparer.Ordinal);
            using (IEnumerator<KeyValuePair<string, string>> enumerator =
                   values.GetEnumerator())
            {
                while (entryCount <= maximum && enumerator.MoveNext())
                {
                    KeyValuePair<string, string> item = enumerator.Current;
                    entryCount++;
                    string key = item.Key ?? string.Empty;
                    if (!copy.ContainsKey(key))
                    {
                        copy.Add(key, item.Value);
                    }
                }
            }

            return new ReadOnlyDictionary<string, string>(copy);
        }

        public static ReadOnlyCollection<CustomizationDiagnostic> FreezeDiagnostics(
            IEnumerable<CustomizationDiagnostic> diagnostics)
        {
            return Array.AsReadOnly((diagnostics ?? Array.Empty<CustomizationDiagnostic>())
                .OrderBy(item => item.Code, StringComparer.Ordinal)
                .ThenBy(item => item.FieldPath, StringComparer.Ordinal)
                .ThenBy(item => item.RecordId, StringComparer.Ordinal)
                .ToArray());
        }
    }

    internal static class CustomizationFingerprint
    {
        public static string Compute(params string[] values)
        {
            string canonical = string.Join("\u001e", values ?? Array.Empty<string>());
            using (SHA256 sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
                var builder = new StringBuilder(digest.Length * 2);
                foreach (byte value in digest)
                {
                    builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }
    }
}
