using System;
using System.Collections.Generic;

namespace AL.Validation
{
    public sealed class HarnessCheckResult
    {
        public HarnessCheckResult(string id, string verdict, string reason)
        {
            Id = id ?? string.Empty;
            Verdict = verdict ?? string.Empty;
            Reason = reason ?? string.Empty;
        }

        public string Id { get; }
        public string Verdict { get; }
        public string Reason { get; }
    }

    public sealed class HarnessSubjectReport
    {
        public HarnessSubjectReport(
            string id,
            string kind,
            string subjectType,
            string verdict,
            IReadOnlyList<HarnessCheckResult> checks,
            IReadOnlyList<string> reasons)
        {
            Id = id ?? string.Empty;
            Kind = kind ?? string.Empty;
            SubjectType = subjectType ?? string.Empty;
            Verdict = verdict ?? string.Empty;
            Checks = checks ?? Array.Empty<HarnessCheckResult>();
            Reasons = reasons ?? Array.Empty<string>();
        }

        public string Id { get; }
        public string Kind { get; }
        public string SubjectType { get; }
        public string Verdict { get; }
        public IReadOnlyList<HarnessCheckResult> Checks { get; }
        public IReadOnlyList<string> Reasons { get; }
    }

    public sealed class HarnessReport
    {
        public HarnessReport(
            string harnessId,
            string packetId,
            string overall,
            IReadOnlyList<HarnessSubjectReport> models,
            IReadOnlyList<HarnessSubjectReport> skills,
            IReadOnlyList<string> reasons)
        {
            HarnessId = harnessId ?? string.Empty;
            PacketId = packetId ?? string.Empty;
            Overall = overall ?? string.Empty;
            Models = models ?? Array.Empty<HarnessSubjectReport>();
            Skills = skills ?? Array.Empty<HarnessSubjectReport>();
            Reasons = reasons ?? Array.Empty<string>();
        }

        public string HarnessId { get; }
        public string PacketId { get; }
        public string Overall { get; }
        public IReadOnlyList<HarnessSubjectReport> Models { get; }
        public IReadOnlyList<HarnessSubjectReport> Skills { get; }
        public IReadOnlyList<string> Reasons { get; }
        public object WeightedScore => null;
    }

    public sealed class HarnessNamedValue
    {
        public HarnessNamedValue(string id, string value)
        {
            Id = id ?? string.Empty;
            Value = value ?? string.Empty;
        }

        public string Id { get; }
        public string Value { get; }
    }

    public sealed class HarnessCheckEvidence
    {
        public HarnessCheckEvidence(string id, string verdict, string reason)
        {
            Id = id ?? string.Empty;
            Verdict = verdict ?? string.Empty;
            Reason = reason ?? string.Empty;
        }

        public string Id { get; }
        public string Verdict { get; }
        public string Reason { get; }
    }

    public sealed class HarnessModelEvidence
    {
        public HarnessModelEvidence(
            string id,
            string kind,
            string[] presentMotionKeys,
            IReadOnlyList<HarnessCheckEvidence> checks,
            string playerBuildVerdict,
            bool missingRepresentative = false,
            bool hasWeightedScore = false)
        {
            Id = id ?? string.Empty;
            Kind = kind ?? string.Empty;
            PresentMotionKeys = presentMotionKeys ?? Array.Empty<string>();
            Checks = checks ?? Array.Empty<HarnessCheckEvidence>();
            PlayerBuildVerdict = playerBuildVerdict ?? string.Empty;
            MissingRepresentative = missingRepresentative;
            HasWeightedScore = hasWeightedScore;
        }

        public string Id { get; }
        public string Kind { get; }
        public string[] PresentMotionKeys { get; }
        public IReadOnlyList<HarnessCheckEvidence> Checks { get; }
        public string PlayerBuildVerdict { get; }
        public bool MissingRepresentative { get; }
        public bool HasWeightedScore { get; }
    }

    public sealed class HarnessSkillEvidence
    {
        public HarnessSkillEvidence(
            string id,
            string actorFamily,
            IReadOnlyList<HarnessNamedValue> phases,
            IReadOnlyList<HarnessNamedValue> effects,
            bool hasWeightedScore = false)
        {
            Id = id ?? string.Empty;
            ActorFamily = actorFamily ?? string.Empty;
            Phases = phases ?? Array.Empty<HarnessNamedValue>();
            Effects = effects ?? Array.Empty<HarnessNamedValue>();
            HasWeightedScore = hasWeightedScore;
        }

        public string Id { get; }
        public string ActorFamily { get; }
        public IReadOnlyList<HarnessNamedValue> Phases { get; }
        public IReadOnlyList<HarnessNamedValue> Effects { get; }
        public bool HasWeightedScore { get; }
    }

    public sealed class HarnessPacket
    {
        public HarnessPacket(
            string packetId,
            IReadOnlyList<HarnessModelEvidence> models,
            IReadOnlyList<HarnessSkillEvidence> skills,
            bool hasWeightedScore = false)
        {
            PacketId = packetId ?? string.Empty;
            Models = models ?? Array.Empty<HarnessModelEvidence>();
            Skills = skills ?? Array.Empty<HarnessSkillEvidence>();
            HasWeightedScore = hasWeightedScore;
        }

        public string PacketId { get; }
        public IReadOnlyList<HarnessModelEvidence> Models { get; }
        public IReadOnlyList<HarnessSkillEvidence> Skills { get; }
        public bool HasWeightedScore { get; }
    }

    public static class ModelMotionSkillVfxHarness
    {
        public const string HarnessId = "mmv_harness_model_motion_skill_vfx_v001";
        public const string CatalogRelativePath = "AL/StreamingAssets/GameData/al_model_motion_skill_vfx_harness.v1.json";

        public static readonly string[] RequiredKinds = { "champion", "npc", "beast", "monster" };

        public static readonly string[] RequiredSkillMotionPhases =
        {
            "anticipation",
            "cast",
            "channel",
            "release",
            "recovery"
        };

        public static readonly string[] ModelCheckFamilies =
        {
            "mesh_topology",
            "uv_material",
            "scale_pivot",
            "skeleton_bind_pose",
            "skin_deformation",
            "animation_clips",
            "equipment_sockets",
            "colliders_hitboxes",
            "lod_impostor_budget",
            "catalog_references",
            "pooling_cleanup",
            "capture_anchors",
            "performance_memory_thermal_overdraw"
        };

        private static readonly string[] WalkingKeys =
        {
            "locomotion.walk",
            "locomotion.crawl",
            "locomotion.fly",
            "locomotion.swim"
        };

        private static readonly string[] RunningKeys =
        {
            "locomotion.run",
            "locomotion.sprint",
            "locomotion.fly"
        };

        private static readonly string[] AttackingKeys = { "attack.basic" };

        private static readonly string[] SpecialAttackKeys =
        {
            "attack.special",
            "attack.heavy",
            "attack.charged"
        };

        private static readonly string[] CastUseKeys = { "skill.cast", "skill.anticipation" };

        private static readonly string[] EffectAxes =
        {
            "telegraph",
            "result_active",
            "impact",
            "cleanup",
            "accessibility"
        };

        public static HarnessReport Evaluate(HarnessPacket packet)
        {
            packet = packet ?? new HarnessPacket("unspecified", null, null);
            var reasons = new List<string>();
            if (packet.HasWeightedScore)
            {
                reasons.Add("packet:weighted_score_forbidden:weightedScore");
            }

            var models = new List<HarnessSubjectReport>();
            for (int index = 0; index < packet.Models.Count; index++)
            {
                models.Add(EvaluateModel(packet.Models[index]));
            }

            var presentKinds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < models.Count; index++)
            {
                if (!string.IsNullOrEmpty(models[index].Kind))
                {
                    presentKinds.Add(models[index].Kind);
                }
            }

            for (int index = 0; index < RequiredKinds.Length; index++)
            {
                string kind = RequiredKinds[index];
                if (presentKinds.Contains(kind))
                {
                    continue;
                }

                string id = "missing_" + kind + "_representative";
                string reason = "missing_representative:" + kind;
                reasons.Add(reason);
                models.Add(
                    new HarnessSubjectReport(
                        id,
                        kind,
                        "model",
                        "FAIL",
                        new[] { new HarnessCheckResult("representative", "FAIL", reason) },
                        new[] { "model:" + id + ":" + reason }));
            }

            var skills = new List<HarnessSubjectReport>();
            for (int index = 0; index < packet.Skills.Count; index++)
            {
                skills.Add(EvaluateSkill(packet.Skills[index]));
            }

            if (skills.Count == 0)
            {
                reasons.Add("missing_skill_coverage");
                skills.Add(
                    new HarnessSubjectReport(
                        "missing_skill_coverage",
                        string.Empty,
                        "skill",
                        "FAIL",
                        new[] { new HarnessCheckResult("coverage", "FAIL", "missing_skill_coverage") },
                        new[] { "skill:missing_skill_coverage:missing_skill_coverage" }));
            }

            var allReasons = new List<string>(reasons);
            var verdicts = new List<string>();
            if (packet.HasWeightedScore)
            {
                verdicts.Add("FAIL");
            }

            AppendSubject(models, allReasons, verdicts);
            AppendSubject(skills, allReasons, verdicts);
            return new HarnessReport(
                HarnessId,
                packet.PacketId,
                Combine(verdicts),
                models,
                skills,
                DistinctSorted(allReasons));
        }

        public static HarnessPacket CreateCompletePacket()
        {
            return new HarnessPacket(
                "fixture-complete",
                new[]
                {
                    CreateCompleteModel("champion"),
                    CreateCompleteModel("npc"),
                    CreateCompleteModel("beast"),
                    CreateCompleteModel("monster")
                },
                new[]
                {
                    CreateCompleteSkill("champion_skill", "champion"),
                    CreateCompleteSkill("npc_skill", "npc"),
                    CreateCompleteSkill("beast_skill", "beast"),
                    CreateCompleteSkill("monster_skill", "monster")
                });
        }

        public static HarnessModelEvidence CreateCompleteModel(string kind, string[] motionKeys = null)
        {
            string[] keys = motionKeys ?? DefaultMotionKeys(kind);
            var checks = new HarnessCheckEvidence[ModelCheckFamilies.Length];
            for (int index = 0; index < ModelCheckFamilies.Length; index++)
            {
                checks[index] = new HarnessCheckEvidence(ModelCheckFamilies[index], "PASS", "fixture");
            }

            return new HarnessModelEvidence(
                kind + "_representative",
                kind,
                keys,
                checks,
                "PASS");
        }

        public static HarnessSkillEvidence CreateCompleteSkill(string id, string family)
        {
            return new HarnessSkillEvidence(
                id,
                family,
                new[]
                {
                    new HarnessNamedValue("anticipation", "motion_skill_anticipation"),
                    new HarnessNamedValue("cast", "motion_skill_cast"),
                    new HarnessNamedValue("channel", "motion_skill_channel"),
                    new HarnessNamedValue("release", "motion_skill_release"),
                    new HarnessNamedValue("recovery", "motion_skill_recovery")
                },
                new[]
                {
                    new HarnessNamedValue("telegraph", "telegraph_ground_ring"),
                    new HarnessNamedValue("active", "active_melee_slash"),
                    new HarnessNamedValue("impact", "impact_hit_flash"),
                    new HarnessNamedValue("cleanup", "cleanup_release"),
                    new HarnessNamedValue("accessibility", "a11y_high_contrast_shape")
                });
        }

        public static string[] DefaultMotionKeys(string kind)
        {
            if (kind == "beast")
            {
                return new[]
                {
                    "locomotion.walk",
                    "locomotion.run",
                    "attack.basic",
                    "attack.special",
                    "skill.cast"
                };
            }

            if (kind == "monster")
            {
                return new[]
                {
                    "locomotion.crawl",
                    "locomotion.run",
                    "attack.basic",
                    "attack.special",
                    "skill.anticipation"
                };
            }

            return new[]
            {
                "locomotion.walk",
                "locomotion.run",
                "attack.basic",
                "attack.heavy",
                "skill.cast"
            };
        }

        public static string FormatOwnerReport(HarnessReport report)
        {
            report = report ?? Evaluate(new HarnessPacket("unspecified", null, null));
            var lines = new List<string>
            {
                "# Model / Motion / Skill-VFX Validation Report",
                "",
                "- Harness: `" + report.HarnessId + "`",
                "- Packet: `" + report.PacketId + "`",
                "- Overall: **" + report.Overall + "**",
                "- Weighted score: forbidden (not computed)",
                "- Owner creative/visual approval: separate gate",
                "",
                "## Models",
                ""
            };
            for (int index = 0; index < report.Models.Count; index++)
            {
                HarnessSubjectReport row = report.Models[index];
                lines.Add("- " + row.Id + " (" + row.Kind + "): " + row.Verdict);
            }

            lines.Add("");
            lines.Add("## Skills");
            lines.Add("");
            for (int index = 0; index < report.Skills.Count; index++)
            {
                HarnessSubjectReport row = report.Skills[index];
                lines.Add("- " + row.Id + " (" + row.Kind + "): " + row.Verdict);
            }

            return string.Join("\n", lines);
        }

        private static HarnessSubjectReport EvaluateModel(HarnessModelEvidence model)
        {
            string id = string.IsNullOrEmpty(model.Id) ? "unnamed_model" : model.Id;
            var reasons = new List<string>();
            var checks = new List<HarnessCheckResult>();
            if (model.HasWeightedScore)
            {
                reasons.Add("model:" + id + ":weighted_score_forbidden:score");
            }

            if (Array.IndexOf(RequiredKinds, model.Kind) < 0)
            {
                reasons.Add("model:" + id + ":unknown_kind:" + model.Kind);
            }

            if (model.MissingRepresentative)
            {
                reasons.Add("model:" + id + ":missing_representative:" + model.Kind);
            }

            var present = new HashSet<string>(model.PresentMotionKeys ?? Array.Empty<string>(), StringComparer.Ordinal);
            AddMotionAxis(id, "walking", WalkingKeys, false, present, checks, reasons);
            AddMotionAxis(id, "running", RunningKeys, false, present, checks, reasons);
            AddMotionAxis(id, "attacking", AttackingKeys, true, present, checks, reasons);
            AddMotionAxis(id, "special_attack", SpecialAttackKeys, false, present, checks, reasons);
            AddMotionAxis(id, "cast_use", CastUseKeys, false, present, checks, reasons);

            var checkMap = new Dictionary<string, HarnessCheckEvidence>(StringComparer.Ordinal);
            for (int index = 0; index < model.Checks.Count; index++)
            {
                HarnessCheckEvidence row = model.Checks[index];
                if (!string.IsNullOrEmpty(row.Id) && !checkMap.ContainsKey(row.Id))
                {
                    checkMap.Add(row.Id, row);
                }
            }

            for (int index = 0; index < ModelCheckFamilies.Length; index++)
            {
                string familyId = ModelCheckFamilies[index];
                if (!checkMap.TryGetValue(familyId, out HarnessCheckEvidence row))
                {
                    string reason = "missing_evidence:" + familyId;
                    reasons.Add("model:" + id + ":" + reason);
                    checks.Add(new HarnessCheckResult(familyId, "BLOCKED", reason));
                    continue;
                }

                string verdict = row.Verdict;
                string reasonText = row.Reason ?? string.Empty;
                if (verdict != "PASS" && verdict != "FAIL" && verdict != "BLOCKED")
                {
                    reasonText = "invalid_verdict:" + familyId;
                    verdict = "FAIL";
                    reasons.Add("model:" + id + ":" + reasonText);
                }
                else if (verdict != "PASS")
                {
                    reasons.Add("model:" + id + ":" + familyId + ":" + (string.IsNullOrEmpty(reasonText) ? verdict.ToLowerInvariant() : reasonText));
                }

                checks.Add(new HarnessCheckResult(familyId, verdict, reasonText));
            }

            string playerBuild = model.PlayerBuildVerdict;
            if (playerBuild != "PASS" && playerBuild != "FAIL" && playerBuild != "BLOCKED")
            {
                playerBuild = "BLOCKED";
                reasons.Add("model:" + id + ":missing_evidence:player_build_presentation");
            }
            else if (playerBuild != "PASS")
            {
                reasons.Add("model:" + id + ":player_build:" + playerBuild);
            }

            var verdicts = new List<string>();
            for (int index = 0; index < checks.Count; index++)
            {
                verdicts.Add(checks[index].Verdict);
            }

            verdicts.Add(playerBuild);
            if (model.HasWeightedScore || model.MissingRepresentative)
            {
                verdicts.Add("FAIL");
            }

            return new HarnessSubjectReport(
                id,
                model.Kind,
                "model",
                Combine(verdicts),
                checks,
                DistinctSorted(reasons));
        }

        private static HarnessSubjectReport EvaluateSkill(HarnessSkillEvidence skill)
        {
            string id = string.IsNullOrEmpty(skill.Id) ? "unnamed_skill" : skill.Id;
            var reasons = new List<string>();
            var checks = new List<HarnessCheckResult>();
            if (skill.HasWeightedScore)
            {
                reasons.Add("skill:" + id + ":weighted_score_forbidden:score");
            }

            var phases = ToMap(skill.Phases);
            var effects = ToMap(skill.Effects);
            for (int index = 0; index < RequiredSkillMotionPhases.Length; index++)
            {
                string phase = RequiredSkillMotionPhases[index];
                if (HasValue(phases, phase))
                {
                    checks.Add(new HarnessCheckResult("motion:" + phase, "PASS", string.Empty));
                }
                else
                {
                    string reason = "missing_skill_motion:" + phase;
                    reasons.Add("skill:" + id + ":" + reason);
                    checks.Add(new HarnessCheckResult("motion:" + phase, "FAIL", reason));
                }
            }

            for (int index = 0; index < EffectAxes.Length; index++)
            {
                string axis = EffectAxes[index];
                string field = axis == "result_active" ? "active" : axis;
                if (HasValue(effects, field))
                {
                    checks.Add(new HarnessCheckResult("effect:" + axis, "PASS", string.Empty));
                }
                else
                {
                    string reason = "missing_skill_effect:" + axis;
                    reasons.Add("skill:" + id + ":" + reason);
                    checks.Add(new HarnessCheckResult("effect:" + axis, "FAIL", reason));
                }
            }

            if (HasValue(effects, "telegraph") && HasValue(effects, "impact"))
            {
                checks.Add(new HarnessCheckResult("telegraph_result_accord", "PASS", string.Empty));
            }
            else
            {
                reasons.Add("skill:" + id + ":telegraph_result_accord");
                checks.Add(new HarnessCheckResult("telegraph_result_accord", "FAIL", "telegraph_result_accord"));
            }

            var verdicts = new List<string>();
            for (int index = 0; index < checks.Count; index++)
            {
                verdicts.Add(checks[index].Verdict);
            }

            if (skill.HasWeightedScore)
            {
                verdicts.Add("FAIL");
            }

            return new HarnessSubjectReport(
                id,
                skill.ActorFamily,
                "skill",
                Combine(verdicts),
                checks,
                DistinctSorted(reasons));
        }

        private static void AddMotionAxis(
            string modelId,
            string axisId,
            string[] keys,
            bool requireAll,
            HashSet<string> present,
            List<HarnessCheckResult> checks,
            List<string> reasons)
        {
            bool ok = requireAll ? ContainsAll(present, keys) : ContainsAny(present, keys);
            if (ok)
            {
                checks.Add(new HarnessCheckResult("motion:" + axisId, "PASS", string.Empty));
                return;
            }

            string reason = "missing_motion_axis:" + axisId;
            reasons.Add("model:" + modelId + ":" + reason);
            checks.Add(new HarnessCheckResult("motion:" + axisId, "FAIL", reason));
        }

        private static bool ContainsAll(HashSet<string> present, string[] keys)
        {
            for (int index = 0; index < keys.Length; index++)
            {
                if (!present.Contains(keys[index]))
                {
                    return false;
                }
            }

            return keys.Length > 0;
        }

        private static bool ContainsAny(HashSet<string> present, string[] keys)
        {
            for (int index = 0; index < keys.Length; index++)
            {
                if (present.Contains(keys[index]))
                {
                    return true;
                }
            }

            return false;
        }

        private static Dictionary<string, string> ToMap(IReadOnlyList<HarnessNamedValue> values)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            if (values == null)
            {
                return map;
            }

            for (int index = 0; index < values.Count; index++)
            {
                HarnessNamedValue row = values[index];
                if (!string.IsNullOrEmpty(row.Id) && !map.ContainsKey(row.Id))
                {
                    map.Add(row.Id, row.Value ?? string.Empty);
                }
            }

            return map;
        }

        private static bool HasValue(Dictionary<string, string> map, string key)
        {
            return map.TryGetValue(key, out string value) && !string.IsNullOrWhiteSpace(value);
        }

        private static void AppendSubject(
            List<HarnessSubjectReport> subjects,
            List<string> reasons,
            List<string> verdicts)
        {
            for (int index = 0; index < subjects.Count; index++)
            {
                HarnessSubjectReport row = subjects[index];
                verdicts.Add(row.Verdict);
                for (int reason = 0; reason < row.Reasons.Count; reason++)
                {
                    reasons.Add(row.Reasons[reason]);
                }
            }
        }

        private static string Combine(List<string> verdicts)
        {
            bool blocked = false;
            for (int index = 0; index < verdicts.Count; index++)
            {
                if (verdicts[index] == "FAIL")
                {
                    return "FAIL";
                }

                if (verdicts[index] == "BLOCKED")
                {
                    blocked = true;
                }
            }

            return blocked ? "BLOCKED" : "PASS";
        }

        private static IReadOnlyList<string> DistinctSorted(List<string> values)
        {
            values.Sort(StringComparer.Ordinal);
            var unique = new List<string>();
            string previous = null;
            for (int index = 0; index < values.Count; index++)
            {
                string value = values[index];
                if (value == previous)
                {
                    continue;
                }

                unique.Add(value);
                previous = value;
            }

            return unique;
        }
    }
}
