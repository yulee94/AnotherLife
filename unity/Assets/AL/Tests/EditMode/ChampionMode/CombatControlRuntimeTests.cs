using System.IO;
using System.Linq;
using AL.ChampionMode.Control;
using AL.ChampionMode.Skills;
using AL.Input;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.ChampionMode
{
    public sealed class CombatControlRuntimeTests
    {
        [Test]
        public void PackagedProfileParsesIntoOneRuntimeAuthority()
        {
            CombatControlProfile profile;
            Assert.True(CombatControlCatalog.TryParse(ReadPackaged(), out profile));
            Assert.That(profile.JumpHeightMeters, Is.EqualTo(1.25f));
            Assert.That(profile.GravityMetersPerSecondSquared, Is.EqualTo(-25f));
            Assert.That(profile.CoyoteTimeSeconds, Is.EqualTo(0.10f));
            Assert.That(profile.JumpBufferSeconds, Is.EqualTo(0.12f));
            Assert.That(profile.AirControlMultiplier, Is.EqualTo(0.50f));
            Assert.That(profile.ResolveMinimumDurationMultiplier, Is.EqualTo(0.25f));
            Assert.That(profile.ResolveGainPerSecond, Is.EqualTo(35f));
            Assert.That(profile.ResolveDecayDelaySeconds, Is.EqualTo(3f));
            Assert.That(profile.ResolveDecayPerSecond, Is.EqualTo(15f));
            Assert.That(profile.HardControlMaximumSeconds, Is.EqualTo(2.5f));
            Assert.That(profile.HardControlImmunitySeconds, Is.EqualTo(2.5f));
            Assert.That(profile.DefaultControlResistance, Is.Zero);
        }

        [Test]
        public void PlayableSkillLoadoutsParseApprovedControlEffects()
        {
            Assert.True(SkillLoadoutCatalog.TryParse(ReadPackaged(), out SkillLoadoutData[] loadouts));

            SkillLoadoutData burst = loadouts.Single(skill => skill.id == "warzone_burst");
            Assert.That(burst.controlKind, Is.EqualTo(CrowdControlKind.Root));
            Assert.That(burst.controlDurationSeconds, Is.EqualTo(1.5f));
            Assert.That(burst.controlSeverity, Is.EqualTo(0.6f));

            SkillLoadoutData breaker = loadouts.Single(skill => skill.id == "warmaster_breaker");
            Assert.That(breaker.controlKind, Is.EqualTo(CrowdControlKind.Knockdown));
            Assert.That(breaker.controlDurationSeconds, Is.EqualTo(1.25f));
            Assert.That(breaker.controlSeverity, Is.EqualTo(1f));

            SkillLoadoutData guard = loadouts.Single(skill => skill.id == "renewing_guard");
            Assert.That(guard.cleanseSoftControl, Is.True);
            Assert.That(guard.controlWardSeconds, Is.EqualTo(2f));
        }

        [Test]
        public void ControlApplicationUsesResistanceAndHardControlCap()
        {
            Assert.True(CombatControlCatalog.TryParse(ReadPackaged(), out CombatControlProfile profile));
            var state = new CrowdControlState(profile, 0.25f);

            CrowdControlApplication root = state.Apply(
                CrowdControlKind.Root,
                2f,
                0.5f);
            CrowdControlApplication knockdown = state.Apply(
                CrowdControlKind.Knockdown,
                10f,
                1f);

            Assert.That(root.Status, Is.EqualTo(CrowdControlApplicationStatus.Applied));
            Assert.That(root.AppliedDurationSeconds, Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(knockdown.Status, Is.EqualTo(CrowdControlApplicationStatus.Applied));
            Assert.That(knockdown.AppliedDurationSeconds, Is.EqualTo(2.5f).Within(0.0001f));
            Assert.That(state.IsActive(CrowdControlKind.Root), Is.True);
            Assert.That(state.IsActive(CrowdControlKind.Knockdown), Is.True);
        }

        [Test]
        public void ResolveGrantsThenDecaysHardControlImmunity()
        {
            Assert.True(CombatControlCatalog.TryParse(ReadPackaged(), out CombatControlProfile profile));
            var state = new CrowdControlState(profile, 0f);

            state.Apply(CrowdControlKind.Knockdown, 2.5f, 1f);
            state.Apply(CrowdControlKind.Stun, 2f, 1f);

            Assert.That(state.Resolve, Is.EqualTo(100f).Within(0.0001f));
            Assert.That(state.IsHardControlImmune, Is.True);
            CrowdControlApplication blocked = state.Apply(
                CrowdControlKind.Knockdown,
                2f,
                1f);
            Assert.That(blocked.Status, Is.EqualTo(CrowdControlApplicationStatus.Immune));
            Assert.That(blocked.AppliedDurationSeconds, Is.Zero);

            state.Advance(2.5f);
            Assert.That(state.IsHardControlImmune, Is.False);
            Assert.That(state.Resolve, Is.EqualTo(100f).Within(0.0001f));

            state.Advance(1.5f);
            Assert.That(state.Resolve, Is.EqualTo(85f).Within(0.0001f));
        }

        [Test]
        public void SoftCleanseRemovesRootAndSilenceAndGrantsWard()
        {
            Assert.True(CombatControlCatalog.TryParse(ReadPackaged(), out CombatControlProfile profile));
            var state = new CrowdControlState(profile, 0f);
            state.Apply(CrowdControlKind.Root, 1f, 0.5f);
            state.Apply(CrowdControlKind.Silence, 1f, 0.5f);
            state.Apply(CrowdControlKind.Knockdown, 1f, 1f);

            state.CleanseSoftControl(2f);

            Assert.That(state.IsActive(CrowdControlKind.Root), Is.False);
            Assert.That(state.IsActive(CrowdControlKind.Silence), Is.False);
            Assert.That(state.IsActive(CrowdControlKind.Knockdown), Is.True);
            Assert.That(state.IsSoftControlWardActive, Is.True);
            Assert.That(
                state.Apply(CrowdControlKind.Root, 1f, 0.5f).Status,
                Is.EqualTo(CrowdControlApplicationStatus.Immune));

            state.Advance(2f);

            Assert.That(state.IsSoftControlWardActive, Is.False);
            Assert.That(
                state.Apply(CrowdControlKind.Root, 1f, 0.5f).Status,
                Is.EqualTo(CrowdControlApplicationStatus.Applied));
        }

        [Test]
        public void ControlKindsExposeApprovedMovementAndActionGates()
        {
            Assert.True(CombatControlCatalog.TryParse(ReadPackaged(), out CombatControlProfile profile));

            var rooted = new CrowdControlState(profile, 0f);
            rooted.Apply(CrowdControlKind.Root, 1f, 0.5f);
            Assert.That(rooted.BlocksMovement, Is.True);
            Assert.That(rooted.BlocksJump, Is.True);
            Assert.That(rooted.BlocksSkillCasting, Is.False);
            Assert.That(rooted.BlocksBasicAttack, Is.False);

            var silenced = new CrowdControlState(profile, 0f);
            silenced.Apply(CrowdControlKind.Silence, 1f, 0.5f);
            Assert.That(silenced.BlocksMovement, Is.False);
            Assert.That(silenced.BlocksSkillCasting, Is.True);
            Assert.That(silenced.BlocksBasicAttack, Is.False);

            var stunned = new CrowdControlState(profile, 0f);
            stunned.Apply(CrowdControlKind.Stun, 1f, 1f);
            Assert.That(stunned.BlocksMovement, Is.True);
            Assert.That(stunned.BlocksJump, Is.True);
            Assert.That(stunned.BlocksSkillCasting, Is.True);
            Assert.That(stunned.BlocksBasicAttack, Is.True);
            Assert.That(stunned.InterruptsAllActions, Is.True);
        }

        [Test]
        public void GameplayInputKeepsJumpAndDodgeOnIndependentBindings()
        {
            Assert.That(GameInput.Jump.name, Is.EqualTo("Jump"));
            Assert.That(
                GameInput.Jump.bindings.Any(binding =>
                    binding.effectivePath == "<Keyboard>/space"),
                Is.True);
            Assert.That(
                GameInput.Dodge.bindings.Any(binding =>
                    binding.effectivePath == "<Keyboard>/leftAlt"),
                Is.True);
            Assert.That(
                GameInput.Dodge.bindings.Any(binding =>
                    binding.effectivePath == "<Keyboard>/space"),
                Is.False,
                "Jump and dodge cannot consume the same gameplay key.");
        }

        private static string ReadPackaged()
        {
            string path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "AL",
                "StreamingAssets",
                "GameData",
                "skill_weather.v1.json"));
            return File.ReadAllText(path);
        }
    }
}
