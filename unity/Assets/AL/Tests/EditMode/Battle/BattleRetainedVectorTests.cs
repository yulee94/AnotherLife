using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using AL.Battle.Computation;
using AL.Battle.Contracts;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.Battle
{
    public class BattleRetainedVectorTests
    {
        [Test]
        public void IndependentFixedPointVectorsRemainExact()
        {
            BattleVectorArtifact artifact = ReadArtifact();

            Assert.That(artifact.FixedPointVectors, Has.Length.EqualTo(4));
            foreach (FixedPointVector vector in artifact.FixedPointVectors)
            {
                Assert.That(
                    BattleFixedPoint.MultiplyAndRoundOnce(
                        vector.Value,
                        vector.MultipliersMicros),
                    Is.EqualTo(vector.Expected),
                    vector.Name);
            }
        }

        [Test]
        public void IndependentSha256EntropyVectorMatchesCanonicalBytesAndUnsignedMapping()
        {
            BattleVectorArtifact artifact = ReadArtifact();
            BattleComputationRequest request = BattleContractTestData.Request();

            Assert.That(artifact.EntropyVectors, Has.Length.EqualTo(1));
            EntropyVector vector = artifact.EntropyVectors[0];
            byte[] canonical = BattleDeterminism.BuildCanonicalDrawInput(
                request,
                vector.DrawNamespace,
                vector.RoundIndex);
            byte[] digest;
            using (SHA256 sha256 = SHA256.Create())
                digest = sha256.ComputeHash(canonical);
            uint draw = BattleDeterminism.ReadUInt32BigEndian(digest);

            Assert.That(ToLowerHex(canonical), Is.EqualTo(vector.CanonicalHex));
            Assert.That(ToLowerHex(digest), Is.EqualTo(vector.Sha256));
            Assert.That((ulong)draw, Is.EqualTo(vector.Draw));
            Assert.That(
                BattleFixedPoint.MapUInt32(draw, vector.Minimum, vector.MaximumExclusive),
                Is.EqualTo(vector.Mapped));
        }

        [Test]
        public void RetainedScenarioLocksPowerRoundsOutcomeCasualtiesRewardsAndResultHash()
        {
            BattleVectorArtifact artifact = ReadArtifact();

            Assert.That(artifact.ScenarioVectors, Has.Length.EqualTo(1));
            ScenarioVector vector = artifact.ScenarioVectors[0];
            BattleKind kind = (BattleKind)Enum.Parse(typeof(BattleKind), vector.BattleKind);
            BattleExecutionMode mode = (BattleExecutionMode)Enum.Parse(
                typeof(BattleExecutionMode),
                vector.ExecutionMode);
            BattleComputationResult computation = DeterministicBattleComputation.Compute(
                BattleContractTestData.Request(
                    kind: kind,
                    mode: mode,
                    seedHex: vector.SeedHex));
            BattleComputedResult result = computation.Value;

            Assert.That(computation.Status.ToString(), Is.EqualTo(vector.ExpectedStatus));
            Assert.That(result.AttackerPower, Is.EqualTo(vector.AttackerPower));
            Assert.That(result.OpponentPower, Is.EqualTo(vector.OpponentPower));
            Assert.That(result.Rounds.Count, Is.EqualTo(vector.RoundCount));
            Assert.That(result.Outcome.ToString(), Is.EqualTo(vector.Outcome));
            Assert.That(result.OutcomeTechnicalId, Is.EqualTo(vector.OutcomeTechnicalId));
            Assert.That(RoundSignature(result.Rounds[0]), Is.EqualTo(vector.FirstRoundSignature));
            Assert.That(
                RoundSignature(result.Rounds[result.Rounds.Count - 1]),
                Is.EqualTo(vector.LastRoundSignature));
            Assert.That(result.AttackerLosses.Select(LossSignature),
                Is.EqualTo(vector.AttackerLosses));
            Assert.That(result.OpponentLosses.Select(LossSignature),
                Is.EqualTo(vector.OpponentLosses));
            Assert.That(result.RewardProposal.Credits, Is.EqualTo(vector.Credits));
            Assert.That(result.RewardProposal.Food, Is.EqualTo(vector.Food));
            Assert.That(result.RewardProposal.Gold, Is.EqualTo(vector.Gold));
            Assert.That(result.RewardProposal.Experience, Is.EqualTo(vector.Experience));
            Assert.That(result.ComputationSha256, Is.EqualTo(vector.ComputationSha256));
            Assert.That(BattleCanonicalHash.Result(result), Is.EqualTo(vector.ComputationSha256));
        }

        [Test]
        public void VectorArtifactDeclaresIndependentGeneratorAndVersion()
        {
            BattleVectorArtifact artifact = ReadArtifact();

            Assert.That(artifact.VectorSchemaVersion, Is.EqualTo("battle_vector_v1"));
            Assert.That(artifact.Generator, Is.EqualTo("python3_stdlib_independent"));
            Assert.That(artifact.FixedPointVectors.Select(item => item.Name), Is.Unique);
            Assert.That(artifact.EntropyVectors.Select(item => item.Name), Is.Unique);
            Assert.That(artifact.ScenarioVectors.Select(item => item.Name), Is.Unique);
        }

        private static BattleVectorArtifact ReadArtifact()
        {
            string path = Path.Combine(
                Application.dataPath,
                "AL/Tests/EditMode/Battle/TestVectors/battle_sha256_v1.json");
            Assert.That(File.Exists(path), Is.True, path);
            using (FileStream stream = File.OpenRead(path))
            {
                var serializer = new DataContractJsonSerializer(typeof(BattleVectorArtifact));
                return (BattleVectorArtifact)serializer.ReadObject(stream);
            }
        }

        private static string RoundSignature(BattleRoundResult round)
        {
            return string.Join("|", new[]
            {
                round.RoundIndex.ToString(),
                round.AttackerRateMicros.ToString(),
                round.OpponentRateMicros.ToString(),
                round.DamageToOpponentMicros.ToString(),
                round.DamageToAttackerMicros.ToString(),
                round.AttackerRemainingPowerMicros.ToString(),
                round.OpponentRemainingPowerMicros.ToString()
            });
        }

        private static string LossSignature(BattleTroopLoss loss)
        {
            return loss.TroopDefinitionId + "|" + loss.Killed + "|" + loss.Wounded + "|" + loss.Survived;
        }

        private static string ToLowerHex(byte[] values)
        {
            return BitConverter.ToString(values).Replace("-", string.Empty).ToLowerInvariant();
        }

        [DataContract]
        private sealed class BattleVectorArtifact
        {
            [DataMember(Name = "vectorSchemaVersion")]
            public string VectorSchemaVersion { get; set; }

            [DataMember(Name = "generator")]
            public string Generator { get; set; }

            [DataMember(Name = "fixedPointVectors")]
            public FixedPointVector[] FixedPointVectors { get; set; }

            [DataMember(Name = "entropyVectors")]
            public EntropyVector[] EntropyVectors { get; set; }

            [DataMember(Name = "scenarioVectors")]
            public ScenarioVector[] ScenarioVectors { get; set; }
        }

        [DataContract]
        private sealed class FixedPointVector
        {
            [DataMember(Name = "name")]
            public string Name { get; set; }

            [DataMember(Name = "value")]
            public long Value { get; set; }

            [DataMember(Name = "multipliersMicros")]
            public long[] MultipliersMicros { get; set; }

            [DataMember(Name = "expected")]
            public long Expected { get; set; }
        }

        [DataContract]
        private sealed class EntropyVector
        {
            [DataMember(Name = "name")]
            public string Name { get; set; }

            [DataMember(Name = "drawNamespace")]
            public string DrawNamespace { get; set; }

            [DataMember(Name = "roundIndex")]
            public int RoundIndex { get; set; }

            [DataMember(Name = "canonicalHex")]
            public string CanonicalHex { get; set; }

            [DataMember(Name = "sha256")]
            public string Sha256 { get; set; }

            [DataMember(Name = "draw")]
            public ulong Draw { get; set; }

            [DataMember(Name = "minimum")]
            public long Minimum { get; set; }

            [DataMember(Name = "maximumExclusive")]
            public long MaximumExclusive { get; set; }

            [DataMember(Name = "mapped")]
            public long Mapped { get; set; }
        }

        [DataContract]
        private sealed class ScenarioVector
        {
            [DataMember(Name = "name")]
            public string Name { get; set; }

            [DataMember(Name = "battleKind")]
            public string BattleKind { get; set; }

            [DataMember(Name = "executionMode")]
            public string ExecutionMode { get; set; }

            [DataMember(Name = "seedHex")]
            public string SeedHex { get; set; }

            [DataMember(Name = "expectedStatus")]
            public string ExpectedStatus { get; set; }

            [DataMember(Name = "attackerPower")]
            public long AttackerPower { get; set; }

            [DataMember(Name = "opponentPower")]
            public long OpponentPower { get; set; }

            [DataMember(Name = "roundCount")]
            public int RoundCount { get; set; }

            [DataMember(Name = "outcome")]
            public string Outcome { get; set; }

            [DataMember(Name = "outcomeTechnicalId")]
            public string OutcomeTechnicalId { get; set; }

            [DataMember(Name = "firstRoundSignature")]
            public string FirstRoundSignature { get; set; }

            [DataMember(Name = "lastRoundSignature")]
            public string LastRoundSignature { get; set; }

            [DataMember(Name = "attackerLosses")]
            public string[] AttackerLosses { get; set; }

            [DataMember(Name = "opponentLosses")]
            public string[] OpponentLosses { get; set; }

            [DataMember(Name = "credits")]
            public int Credits { get; set; }

            [DataMember(Name = "food")]
            public int Food { get; set; }

            [DataMember(Name = "gold")]
            public int Gold { get; set; }

            [DataMember(Name = "experience")]
            public int Experience { get; set; }

            [DataMember(Name = "computationSha256")]
            public string ComputationSha256 { get; set; }
        }
    }
}
