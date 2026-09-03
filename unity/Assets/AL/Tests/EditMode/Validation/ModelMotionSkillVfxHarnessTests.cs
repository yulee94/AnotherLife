using System.Collections.Generic;
using System.IO;
using System.Linq;
using AL.ChampionMode.Skills;
using AL.Validation;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.Validation
{
    public sealed class ModelMotionSkillVfxHarnessTests
    {
        [Test]
        public void CompleteChampionNpcBeastAndMonsterPacketPasses()
        {
            HarnessReport report = ModelMotionSkillVfxHarness.Evaluate(
                ModelMotionSkillVfxHarness.CreateCompletePacket());
            Assert.That(report.Overall, Is.EqualTo("PASS"));
            Assert.That(report.Models.Count, Is.EqualTo(4));
            Assert.That(report.Skills.Count, Is.EqualTo(4));
            Assert.That(report.Models.Select(row => row.Kind), Is.EquivalentTo(new[]
            {
                "champion",
                "npc",
                "beast",
                "monster"
            }));
            Assert.That(report.Models.All(row => row.Verdict == "PASS"), Is.True);
            Assert.That(report.Skills.All(row => row.Verdict == "PASS"), Is.True);
            Assert.That(report.WeightedScore, Is.Null);
        }

        [Test]
        public void MissingWalkingFailsEvenWhenOtherAxesPass()
        {
            HarnessPacket packet = ModelMotionSkillVfxHarness.CreateCompletePacket();
            HarnessModelEvidence champion = WithoutMotion(
                packet.Models[0],
                key => key != "locomotion.walk");
            HarnessReport report = ModelMotionSkillVfxHarness.Evaluate(
                ReplaceModel(packet, 0, champion));
            Assert.That(report.Overall, Is.EqualTo("FAIL"));
            Assert.That(report.Models[0].Verdict, Is.EqualTo("FAIL"));
            Assert.That(
                report.Reasons.Any(reason => reason.Contains("missing_motion_axis:walking")),
                Is.True);
            Assert.That(report.Models.Skip(1).All(row => row.Verdict == "PASS"), Is.True);
        }

        [Test]
        public void MissingRunningAttackSpecialAndCastEachFailClosed()
        {
            AssertAxisFailure("npc", "running", key => key != "locomotion.run");
            AssertAxisFailure("beast", "attacking", key => key != "attack.basic");
            AssertAxisFailure(
                "monster",
                "special_attack",
                key => key != "attack.special" && key != "attack.heavy" && key != "attack.charged");
            AssertAxisFailure(
                "champion",
                "cast_use",
                key => key != "skill.cast" && !key.StartsWith("skill."));
        }

        [Test]
        public void MissingSkillTelegraphOrCastMotionFailsClosed()
        {
            HarnessPacket packet = ModelMotionSkillVfxHarness.CreateCompletePacket();
            HarnessSkillEvidence skill = new HarnessSkillEvidence(
                packet.Skills[0].Id,
                packet.Skills[0].ActorFamily,
                packet.Skills[0].Phases,
                packet.Skills[0].Effects.Select(value =>
                    value.Id == "telegraph" ? new HarnessNamedValue(value.Id, string.Empty) : value).ToArray());
            HarnessReport report = ModelMotionSkillVfxHarness.Evaluate(ReplaceSkill(packet, 0, skill));
            Assert.That(report.Overall, Is.EqualTo("FAIL"));
            Assert.That(
                report.Reasons.Any(reason => reason.Contains("missing_skill_effect:telegraph")),
                Is.True);

            packet = ModelMotionSkillVfxHarness.CreateCompletePacket();
            skill = new HarnessSkillEvidence(
                packet.Skills[2].Id,
                packet.Skills[2].ActorFamily,
                packet.Skills[2].Phases.Select(value =>
                    value.Id == "cast" ? new HarnessNamedValue(value.Id, string.Empty) : value).ToArray(),
                packet.Skills[2].Effects);
            report = ModelMotionSkillVfxHarness.Evaluate(ReplaceSkill(packet, 2, skill));
            Assert.That(report.Overall, Is.EqualTo("FAIL"));
            Assert.That(
                report.Reasons.Any(reason => reason.Contains("missing_skill_motion:cast")),
                Is.True);
        }

        [Test]
        public void MissingMonsterRepresentativeFailsClosed()
        {
            HarnessPacket complete = ModelMotionSkillVfxHarness.CreateCompletePacket();
            var models = new List<HarnessModelEvidence>();
            for (int index = 0; index < complete.Models.Count; index++)
            {
                if (complete.Models[index].Kind != "monster")
                {
                    models.Add(complete.Models[index]);
                }
            }

            HarnessReport report = ModelMotionSkillVfxHarness.Evaluate(
                new HarnessPacket(complete.PacketId, models, complete.Skills));
            Assert.That(report.Overall, Is.EqualTo("FAIL"));
            HarnessSubjectReport monster = report.Models.First(row => row.Kind == "monster");
            Assert.That(monster.Verdict, Is.EqualTo("FAIL"));
            Assert.That(
                report.Reasons.Any(reason => reason.Contains("missing_representative:monster")),
                Is.True);
        }

        [Test]
        public void WeightedScoreAndMissingPlayerBuildDoNotPass()
        {
            HarnessReport scored = ModelMotionSkillVfxHarness.Evaluate(
                new HarnessPacket(
                    "scored",
                    ModelMotionSkillVfxHarness.CreateCompletePacket().Models,
                    ModelMotionSkillVfxHarness.CreateCompletePacket().Skills,
                    hasWeightedScore: true));
            Assert.That(scored.Overall, Is.EqualTo("FAIL"));
            Assert.That(
                scored.Reasons.Any(reason => reason.Contains("weighted_score_forbidden")),
                Is.True);

            HarnessPacket complete = ModelMotionSkillVfxHarness.CreateCompletePacket();
            var blockedModels = new List<HarnessModelEvidence>();
            for (int index = 0; index < complete.Models.Count; index++)
            {
                HarnessModelEvidence model = complete.Models[index];
                blockedModels.Add(
                    new HarnessModelEvidence(
                        model.Id,
                        model.Kind,
                        model.PresentMotionKeys,
                        model.Checks,
                        string.Empty));
            }

            HarnessReport blocked = ModelMotionSkillVfxHarness.Evaluate(
                new HarnessPacket(complete.PacketId, blockedModels, complete.Skills));
            Assert.That(blocked.Overall, Is.EqualTo("BLOCKED"));
            Assert.That(blocked.Models.All(row => row.Verdict == "BLOCKED"), Is.True);
        }

        [Test]
        public void PackagedSkillsEmitExplicitPerSkillVerdicts()
        {
            Assert.True(SkillCastTraceValidator.TryValidate(ReadPackagedSkills(), out SkillCastTraceReport trace));
            Assert.That(trace.Passed, Is.True);
            Assert.True(SkillCastPresentationCatalog.TryParse(
                ReadPackagedSkills(),
                out SkillCastPresentationSnapshot snapshot));

            var skills = new List<HarnessSkillEvidence>(snapshot.Bindings.Count);
            for (int index = 0; index < snapshot.Bindings.Count; index++)
            {
                SkillCastBinding binding = snapshot.Bindings[index];
                skills.Add(
                    new HarnessSkillEvidence(
                        binding.SkillId,
                        binding.ActorFamily,
                        new[]
                        {
                            new HarnessNamedValue("anticipation", binding.MotionAnticipationId),
                            new HarnessNamedValue("cast", binding.MotionCastId),
                            new HarnessNamedValue("channel", binding.MotionChannelId),
                            new HarnessNamedValue("release", binding.MotionReleaseId),
                            new HarnessNamedValue("recovery", binding.MotionRecoveryId)
                        },
                        new[]
                        {
                            new HarnessNamedValue("telegraph", binding.TelegraphModuleId),
                            new HarnessNamedValue("active", binding.ActiveEffectModuleId),
                            new HarnessNamedValue("impact", binding.ImpactModuleId),
                            new HarnessNamedValue("cleanup", binding.CleanupModuleId),
                            new HarnessNamedValue("accessibility", binding.AccessibilityVariantId)
                        }));
            }

            HarnessReport report = ModelMotionSkillVfxHarness.Evaluate(
                new HarnessPacket(
                    "packaged-skills",
                    ModelMotionSkillVfxHarness.CreateCompletePacket().Models,
                    skills));
            Assert.That(report.Skills.Count, Is.GreaterThanOrEqualTo(8));
            Assert.That(report.Skills.All(row => row.Verdict == "PASS"), Is.True);
            Assert.That(report.Overall, Is.EqualTo("PASS"));
        }

        [Test]
        public void CatalogAndOwnerReportStayExplicit()
        {
            string catalog = File.ReadAllText(Path.GetFullPath(Path.Combine(
                Application.dataPath,
                ModelMotionSkillVfxHarness.CatalogRelativePath)));
            Assert.That(catalog, Does.Contain(ModelMotionSkillVfxHarness.HarnessId));
            Assert.That(catalog, Does.Contain("\"champion\""));
            Assert.That(catalog, Does.Contain("\"monster\""));
            Assert.That(catalog, Does.Contain("\"weightedScoreForbidden\": true"));
            string markdown = ModelMotionSkillVfxHarness.FormatOwnerReport(
                ModelMotionSkillVfxHarness.Evaluate(ModelMotionSkillVfxHarness.CreateCompletePacket()));
            Assert.That(markdown, Does.Contain("Overall: **PASS**"));
            Assert.That(markdown, Does.Contain("Weighted score: forbidden"));
        }

        private static void AssertAxisFailure(
            string kind,
            string axis,
            System.Func<string, bool> keep)
        {
            HarnessPacket packet = ModelMotionSkillVfxHarness.CreateCompletePacket();
            int index = 0;
            for (; index < packet.Models.Count; index++)
            {
                if (packet.Models[index].Kind == kind)
                {
                    break;
                }
            }

            HarnessReport report = ModelMotionSkillVfxHarness.Evaluate(
                ReplaceModel(packet, index, WithoutMotion(packet.Models[index], keep)));
            Assert.That(report.Overall, Is.EqualTo("FAIL"), axis);
            Assert.That(
                report.Reasons.Any(reason => reason.Contains("missing_motion_axis:" + axis)),
                Is.True,
                axis);
        }

        private static HarnessModelEvidence WithoutMotion(
            HarnessModelEvidence model,
            System.Func<string, bool> keep)
        {
            return new HarnessModelEvidence(
                model.Id,
                model.Kind,
                model.PresentMotionKeys.Where(keep).ToArray(),
                model.Checks,
                model.PlayerBuildVerdict,
                model.MissingRepresentative,
                model.HasWeightedScore);
        }

        private static HarnessPacket ReplaceModel(
            HarnessPacket packet,
            int index,
            HarnessModelEvidence model)
        {
            var models = packet.Models.ToList();
            models[index] = model;
            return new HarnessPacket(packet.PacketId, models, packet.Skills, packet.HasWeightedScore);
        }

        private static HarnessPacket ReplaceSkill(
            HarnessPacket packet,
            int index,
            HarnessSkillEvidence skill)
        {
            var skills = packet.Skills.ToList();
            skills[index] = skill;
            return new HarnessPacket(packet.PacketId, packet.Models, skills, packet.HasWeightedScore);
        }

        private static string ReadPackagedSkills()
        {
            return File.ReadAllText(Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "AL",
                "StreamingAssets",
                "GameData",
                "skill_weather.v1.json")));
        }
    }
}
