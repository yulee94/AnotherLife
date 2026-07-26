using System;
using System.Collections.Generic;
using System.Linq;
using AL.ChampionMode.C1;
using NUnit.Framework;

namespace AL.Tests.EditMode.ChampionCombat.C1
{
    public sealed class CombatPrimitiveValidationTests
    {
        [TestCase(CombatScalarKind.Health)]
        [TestCase(CombatScalarKind.Mana)]
        [TestCase(CombatScalarKind.Damage)]
        [TestCase(CombatScalarKind.Healing)]
        [TestCase(CombatScalarKind.AttackPower)]
        [TestCase(CombatScalarKind.WorldDistance)]
        [TestCase(CombatScalarKind.Duration)]
        [TestCase(CombatScalarKind.MovementSpeed)]
        [TestCase(CombatScalarKind.RegenerationRate)]
        [TestCase(CombatScalarKind.Multiplier)]
        public void MicrosRanges_AcceptExactBoundariesAndRejectOutside(
            CombatScalarKind kind)
        {
            long ceiling = CombatTechnicalLimits.MaximumMicros(kind);

            Assert.That(
                CombatPrimitiveValidation.IsMicrosInRange(0L, kind, false),
                Is.True);
            Assert.That(
                CombatPrimitiveValidation.IsMicrosInRange(ceiling, kind, false),
                Is.True);
            Assert.That(
                CombatPrimitiveValidation.IsMicrosInRange(0L, kind, true),
                Is.False);
            Assert.That(
                CombatPrimitiveValidation.IsMicrosInRange(-1L, kind, false),
                Is.False);
            Assert.That(
                CombatPrimitiveValidation.IsMicrosInRange(ceiling + 1L, kind, false),
                Is.False);
        }

        [Test]
        public void UndefinedScalarKinds_AreRejectedByTryAndValidationApisWithoutThrowing()
        {
            var undefined = (CombatScalarKind)int.MaxValue;

            Assert.DoesNotThrow(() =>
            {
                Assert.That(
                    CombatTechnicalLimits.TryGetMaximumMicros(undefined, out _),
                    Is.False);
                Assert.That(
                    CombatPrimitiveValidation.TryGetMaximumUnits(undefined, out _),
                    Is.False);
                Assert.That(
                    CombatPrimitiveValidation.IsMicrosInRange(1L, undefined, false),
                    Is.False);
                Assert.That(
                    CombatPrimitiveValidation.TryConvertUnitsToMicros(
                        1d,
                        undefined,
                        false,
                        out _),
                    Is.False);
                Assert.That(
                    FiniteCombatScalar.TryCreate(
                        1f,
                        undefined,
                        "combat.unit.test",
                        false,
                        out _),
                    Is.False);
            });
        }

        [Test]
        public void FiniteScalar_RejectsNonFiniteNegativeZeroRequiredAndOverCeiling()
        {
            Assert.That(
                FiniteCombatScalar.TryCreate(
                    10f,
                    CombatScalarKind.Health,
                    "combat.unit.health",
                    true,
                    out FiniteCombatScalar valid),
                Is.True);
            Assert.That(valid.Value, Is.EqualTo(10f));
            Assert.That(valid.Kind, Is.EqualTo(CombatScalarKind.Health));
            Assert.That(valid.UnitProfileId, Is.EqualTo("combat.unit.health"));

            float[] invalidValues =
            {
                float.NaN,
                float.PositiveInfinity,
                float.NegativeInfinity,
                -1f,
                1_000_000_128f
            };
            Assert.That(
                invalidValues[invalidValues.Length - 1],
                Is.GreaterThan(CombatPrimitiveValidation.MaximumUnits(
                    CombatScalarKind.Health)));
            foreach (float invalid in invalidValues)
            {
                Assert.That(
                    FiniteCombatScalar.TryCreate(
                        invalid,
                        CombatScalarKind.Health,
                        "combat.unit.health",
                        false,
                        out _),
                    Is.False,
                    invalid.ToString());
            }

            Assert.That(
                FiniteCombatScalar.TryCreate(
                    0f,
                    CombatScalarKind.Health,
                    "combat.unit.health",
                    true,
                    out _),
                Is.False);
            Assert.That(
                FiniteCombatScalar.TryCreate(
                    1f,
                    CombatScalarKind.Health,
                    "invalid unit",
                    true,
                    out _),
                Is.False);
        }

        [Test]
        public void FiniteVector_ValidatesEveryComponentBeforeUseAndBoundsWorldCoordinates()
        {
            Assert.That(
                FiniteCombatVector3.TryCreate(
                    -100_000f,
                    0f,
                    100_000f,
                    "combat.unit.meters",
                    out FiniteCombatVector3 valid),
                Is.True);
            Assert.That(valid.X, Is.EqualTo(-100_000f));
            Assert.That(valid.Z, Is.EqualTo(100_000f));

            float[] invalidComponents =
            {
                float.NaN,
                float.PositiveInfinity,
                float.NegativeInfinity,
                100_001f,
                -100_001f
            };
            foreach (float invalid in invalidComponents)
            {
                Assert.That(
                    FiniteCombatVector3.TryCreate(
                        invalid,
                        0f,
                        0f,
                        "combat.unit.meters",
                        out _),
                    Is.False,
                    invalid.ToString());
                Assert.That(
                    FiniteCombatVector3.TryCreate(
                        0f,
                        invalid,
                        0f,
                        "combat.unit.meters",
                        out _),
                    Is.False,
                    invalid.ToString());
                Assert.That(
                    FiniteCombatVector3.TryCreate(
                        0f,
                        0f,
                        invalid,
                        "combat.unit.meters",
                        out _),
                    Is.False,
                    invalid.ToString());
            }
        }

        [Test]
        public void UnitConversion_IsExactFiniteCheckedAndCeilingBounded()
        {
            Assert.That(
                CombatPrimitiveValidation.TryConvertUnitsToMicros(
                    0.1d,
                    CombatScalarKind.Duration,
                    false,
                    out long micros),
                Is.True);
            Assert.That(micros, Is.EqualTo(100_000L));
            Assert.That(
                CombatPrimitiveValidation.TryConvertUnitsToMicros(
                    86_400d,
                    CombatScalarKind.Duration,
                    true,
                    out micros),
                Is.True);
            Assert.That(micros, Is.EqualTo(CombatTechnicalLimits.DurationMaximumMicros));

            double[] invalid =
            {
                double.NaN,
                double.PositiveInfinity,
                double.NegativeInfinity,
                -0.1d,
                86_400.000001d,
                0.0000001d
            };
            foreach (double value in invalid)
            {
                Assert.That(
                    CombatPrimitiveValidation.TryConvertUnitsToMicros(
                        value,
                        CombatScalarKind.Duration,
                        false,
                        out _),
                    Is.False,
                    value.ToString());
            }
        }

        [Test]
        public void StableIds_AreUtf8BoundedControlFreeAndOrdinal()
        {
            string[] invalid =
            {
                null,
                string.Empty,
                " ",
                " id",
                "id ",
                "two ids",
                "id\nbreak",
                "id\u0000",
                new string('a', CombatTechnicalLimits.MaximumStableIdUtf8Bytes + 1),
                new string('\u00e9', 129),
                "\ud800"
            };
            foreach (string value in invalid)
            {
                Assert.That(CombatPrimitiveValidation.IsStableId(value), Is.False);
                Assert.That(CombatStableId.TryCreate(value, out _), Is.False);
            }

            Assert.That(CombatStableId.TryCreate("Skill.Id", out CombatStableId upper), Is.True);
            Assert.That(CombatStableId.TryCreate("skill.id", out CombatStableId lower), Is.True);
            Assert.That(upper, Is.Not.EqualTo(lower));
            Assert.That(upper.Value, Is.EqualTo("Skill.Id"));
        }

        [Test]
        public void VersionsAndHashes_AreStrictAndCaseSensitive()
        {
            Assert.That(CombatPrimitiveValidation.IsVersion("combat-v1"), Is.True);
            Assert.That(CombatPrimitiveValidation.IsVersion(
                new string('v', CombatTechnicalLimits.MaximumVersionUtf8Bytes)), Is.True);
            Assert.That(CombatPrimitiveValidation.IsVersion(
                new string('v', CombatTechnicalLimits.MaximumVersionUtf8Bytes + 1)), Is.False);
            Assert.That(CombatPrimitiveValidation.IsSupportedSchemaVersion("1"), Is.True);
            Assert.That(CombatPrimitiveValidation.IsSupportedSchemaVersion("01"), Is.False);
            Assert.That(CombatContractVersion.TryCreate("combat-v1", out _), Is.True);

            string lowerHash = new string('a', 64);
            Assert.That(CombatPrimitiveValidation.IsSha256(lowerHash), Is.True);
            Assert.That(CombatSha256.TryCreate(lowerHash, out CombatSha256 hash), Is.True);
            Assert.That(hash.Value, Is.EqualTo(lowerHash));
            Assert.That(CombatPrimitiveValidation.IsSha256(new string('A', 64)), Is.False);
            Assert.That(CombatPrimitiveValidation.IsSha256(new string('a', 63)), Is.False);
            Assert.That(CombatPrimitiveValidation.IsSha256(new string('a', 65)), Is.False);
            Assert.That(CombatPrimitiveValidation.IsSha256(
                new string('a', 63) + "g"), Is.False);
        }

        [Test]
        public void Diagnostics_AreSafeBoundedAndDeterministicallyOrdered()
        {
            var later = new CombatDiagnostic(
                "AL-SKILL-CATALOG-Z",
                CombatDiagnosticSeverity.Error,
                CombatDiagnosticDomain.SkillCatalog,
                "$.z",
                "unsafe\nmessage",
                CombatBlockScope.Construction);
            var earlier = new CombatDiagnostic(
                "AL-SKILL-CATALOG-A",
                CombatDiagnosticSeverity.Warning,
                CombatDiagnosticDomain.SkillCatalog,
                "$.\u0000a",
                "safe",
                CombatBlockScope.None);
            var input = new List<CombatDiagnostic> { later, earlier };

            IReadOnlyList<CombatDiagnostic> ordered =
                CombatDiagnosticOrdering.Order(input);
            input.Clear();

            Assert.That(ordered.Select(item => item.Code), Is.EqualTo(new[]
            {
                "AL-SKILL-CATALOG-A",
                "AL-SKILL-CATALOG-Z"
            }));
            Assert.That(ordered[0].FieldPath, Does.Not.Contain("\u0000"));
            Assert.That(ordered[1].Message, Does.Not.Contain("\n"));
            Assert.That(ordered[1].BlocksConstruction, Is.True);
            Assert.That(ordered[1].BlocksAction, Is.False);
            Assert.Throws<NotSupportedException>(() =>
                ((IList<CombatDiagnostic>)ordered)[0] = later);
        }

        [Test]
        public void DiagnosticConstruction_RejectsUnknownCodesAndEnumValues()
        {
            Assert.Throws<ArgumentException>(() => new CombatDiagnostic(
                "OTHER-CODE",
                CombatDiagnosticSeverity.Error,
                CombatDiagnosticDomain.Contract,
                "$",
                "message",
                CombatBlockScope.None));
            Assert.Throws<ArgumentException>(() => new CombatDiagnostic(
                "AL-SKILL-CATALOG-lower",
                CombatDiagnosticSeverity.Error,
                CombatDiagnosticDomain.SkillCatalog,
                "$",
                "message",
                CombatBlockScope.None));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CombatDiagnostic(
                "AL-SKILL-CATALOG-ENUM",
                (CombatDiagnosticSeverity)99,
                CombatDiagnosticDomain.SkillCatalog,
                "$",
                "message",
                CombatBlockScope.None));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CombatDiagnostic(
                "AL-SKILL-CATALOG-ENUM",
                CombatDiagnosticSeverity.Error,
                (CombatDiagnosticDomain)99,
                "$",
                "message",
                CombatBlockScope.None));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CombatDiagnostic(
                "AL-SKILL-CATALOG-ENUM",
                CombatDiagnosticSeverity.Error,
                CombatDiagnosticDomain.SkillCatalog,
                "$",
                "message",
                (CombatBlockScope)(1 << 20)));
        }

        [Test]
        public void ImmutableHelper_CopiesAndBoundsCollections()
        {
            var input = new List<string> { "first", "second" };
            IReadOnlyList<string> frozen = CombatImmutable.Freeze(input, "input");
            input[0] = "mutated";

            Assert.That(frozen, Is.EqualTo(new[] { "first", "second" }));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<string>)frozen)[0] = "mutated");
            Assert.Throws<ArgumentException>(() =>
                CombatImmutable.Freeze(
                    new string[CombatTechnicalLimits.MaximumReferenceEntries + 1],
                    "input"));
        }
    }
}
