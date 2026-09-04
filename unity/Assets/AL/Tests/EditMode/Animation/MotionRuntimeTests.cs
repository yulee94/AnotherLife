using System;
using System.Linq;
using AL.Motion;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AL.Tests.EditMode.Animation
{
    public sealed class MotionRuntimeTests
    {
        [Test]
        public void CatalogResolvesExactMotionKeysAndStableFallbackChain()
        {
            var idleClip = new AnimationClip { name = "idle" };
            var attackClip = new AnimationClip { name = "attack" };
            try
            {
                var catalog = new MotionCatalogSnapshot(
                    "idle.neutral",
                    new[]
                    {
                        new MotionClipDefinition(
                            "rmc_clip_attack_basic_v001",
                            "attack.basic",
                            attackClip,
                            "idle.neutral",
                            MotionRootMode.Bounded,
                            MotionPriority.Attack,
                            false,
                            false),
                        new MotionClipDefinition(
                            "rmc_clip_idle_neutral_v001",
                            "idle.neutral",
                            idleClip,
                            null,
                            MotionRootMode.InPlace,
                            MotionPriority.Idle,
                            true,
                            false)
                    });

                Assert.That(catalog.TryResolve("attack.chain", out MotionClipDefinition fallback), Is.True);
                Assert.That(fallback.MotionKey, Is.EqualTo("idle.neutral"));
                Assert.That(catalog.TryResolve("ATTACK.BASIC", out _), Is.True);
                Assert.That(catalog.TryGetExact("ATTACK.BASIC", out _), Is.False);
                Assert.That(catalog.TryGetExact("attack.basic", out MotionClipDefinition exact), Is.True);
                Assert.That(exact.ClipId, Is.EqualTo("rmc_clip_attack_basic_v001"));

                Assert.Throws<InvalidOperationException>(() =>
                    new MotionCatalogSnapshot(
                        "idle.neutral",
                        new[]
                        {
                            new MotionClipDefinition(
                                "rmc_clip_idle_a_v001",
                                "idle.neutral",
                                idleClip,
                                null,
                                MotionRootMode.InPlace,
                                MotionPriority.Idle,
                                true,
                                false),
                            new MotionClipDefinition(
                                "rmc_clip_idle_b_v001",
                                "idle.neutral",
                                idleClip,
                                null,
                                MotionRootMode.InPlace,
                                MotionPriority.Idle,
                                true,
                                false)
                        }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(idleClip);
                UnityEngine.Object.DestroyImmediate(attackClip);
            }
        }

        [Test]
        public void TransitionPriorityCancellationAndRecoveryRemainBounded()
        {
            var idle = new AnimationClip { name = "idle" };
            var cast = new AnimationClip { name = "cast" };
            var cancellation = new AnimationClip { name = "cancellation" };
            var interruption = new AnimationClip { name = "interruption" };
            var defeat = new AnimationClip { name = "defeat" };
            try
            {
                var catalog = new MotionCatalogSnapshot(
                    "idle.neutral",
                    new[]
                    {
                        Definition("idle", "idle.neutral", idle, MotionPriority.Idle),
                        Definition("cast", "skill.cast", cast, MotionPriority.Skill),
                        Definition(
                            "cancel",
                            "skill.cancellation",
                            cancellation,
                            MotionPriority.Interruption),
                        Definition(
                            "interrupt",
                            "skill.interruption",
                            interruption,
                            MotionPriority.Interruption),
                        Definition("defeat", "defeat", defeat, MotionPriority.Defeat)
                    });
                var transitions = new MotionTransitionMachine(catalog);

                Assert.That(transitions.TryRequest("skill.cast", 7, out _), Is.True);
                Assert.That(
                    transitions.TryRequest("idle.neutral", 8, out MotionTransitionResult rejected),
                    Is.False);
                Assert.That(rejected.Outcome, Is.EqualTo(MotionTransitionOutcome.RejectedPriority));

                MotionTransitionResult cancelled = transitions.Cancel(7, true);
                Assert.That(cancelled.Outcome, Is.EqualTo(MotionTransitionOutcome.CancelledPreCommit));
                Assert.That(cancelled.Active.MotionKey, Is.EqualTo("skill.cancellation"));
                Assert.That(cancelled.BlendSeconds, Is.LessThanOrEqualTo(0.15f));

                transitions.CompleteCurrent();
                Assert.That(transitions.Current.MotionKey, Is.EqualTo("idle.neutral"));
                Assert.That(transitions.TryRequest("skill.cast", 9, out _), Is.True);
                Assert.That(transitions.MarkCommitted(9), Is.True);
                MotionTransitionResult interrupted = transitions.Cancel(9, true);
                Assert.That(interrupted.Outcome, Is.EqualTo(MotionTransitionOutcome.InterruptedPostCommit));
                Assert.That(interrupted.Active.MotionKey, Is.EqualTo("skill.interruption"));

                Assert.That(transitions.TryRequest("defeat", 10, out _), Is.True);
                Assert.That(transitions.Current.MotionKey, Is.EqualTo("defeat"));
                transitions.CompleteCurrent();
                Assert.That(transitions.Current.MotionKey, Is.EqualTo("idle.neutral"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(idle);
                UnityEngine.Object.DestroyImmediate(cast);
                UnityEngine.Object.DestroyImmediate(cancellation);
                UnityEngine.Object.DestroyImmediate(interruption);
                UnityEngine.Object.DestroyImmediate(defeat);
            }
        }

        [Test]
        public void RootMotionStrideTurnAndFootCorrectionRespectAuthorityBounds()
        {
            MotionRootDelta inPlace = MotionRootPolicy.Resolve(
                MotionRootMode.InPlace,
                new Vector3(3f, 2f, 4f),
                80f,
                0.5f,
                20f,
                false);
            Assert.That(inPlace.Position, Is.EqualTo(Vector3.zero));
            Assert.That(inPlace.YawDegrees, Is.Zero);

            MotionRootDelta bounded = MotionRootPolicy.Resolve(
                MotionRootMode.Bounded,
                new Vector3(3f, 2f, 4f),
                80f,
                0.5f,
                20f,
                false);
            Assert.That(new Vector2(bounded.Position.x, bounded.Position.z).magnitude,
                Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(bounded.Position.y, Is.Zero);
            Assert.That(bounded.YawDegrees, Is.EqualTo(20f));

            MotionRootDelta authored = MotionRootPolicy.Resolve(
                MotionRootMode.Authored,
                new Vector3(0.2f, 0.3f, 0.4f),
                -15f,
                1f,
                30f,
                true);
            Assert.That(authored.Position, Is.EqualTo(new Vector3(0.2f, 0.3f, 0.4f)));
            Assert.That(authored.YawDegrees, Is.EqualTo(-15f));

            Assert.That(
                MotionWarp.CalculateStridePlaybackSpeed(1f, 6f, 0.5f, 0.75f, 1.25f),
                Is.EqualTo(1.25f));
            Assert.That(MotionWarp.CalculateTurnScale(90f, 45f), Is.EqualTo(0.5f));

            Vector3 correction = MotionGroundingMath.ClampContactCorrection(
                new Vector3(0f, 0.04f, 0f),
                new Vector3(0.08f, 0f, 0f),
                0.02f,
                0.01f);
            Assert.That(new Vector2(correction.x, correction.z).magnitude,
                Is.EqualTo(0.02f).Within(0.0001f));
            Assert.That(correction.y, Is.EqualTo(-0.01f).Within(0.0001f));
        }

        [Test]
        public void EventOrderingDeduplicationAndHitboxWindowsAreSpeedStableAndFailClosed()
        {
            var timeline = new MotionEventTimeline(
                30,
                31,
                new[]
                {
                    new MotionEventDefinition(
                        "rmc_event_contact_begin_v001",
                        "al.motion.contact.begin",
                        0,
                        0,
                        new MotionStaticPayload { ContactId = "foot_l" }),
                    new MotionEventDefinition(
                        "rmc_event_hitbox_request_begin_v001",
                        "al.motion.hitbox.request_begin",
                        10,
                        1,
                        new MotionStaticPayload { WindowId = "attack.primary" }),
                    new MotionEventDefinition(
                        "rmc_event_hitbox_request_end_v001",
                        "al.motion.hitbox.request_end",
                        20,
                        2,
                        new MotionStaticPayload { WindowId = "attack.primary" }),
                    new MotionEventDefinition(
                        "rmc_event_contact_end_v001",
                        "al.motion.contact.end",
                        30,
                        3,
                        new MotionStaticPayload { ContactId = "foot_l" })
                });

            MotionEventDispatch[] slow = timeline.Collect(0d, 2d, 0.5f, 42).ToArray();
            MotionEventDispatch[] fast = timeline.Collect(0d, 0.5d, 2f, 42).ToArray();
            Assert.That(
                slow.Select(value => value.EventName),
                Is.EqualTo(fast.Select(value => value.EventName)));
            Assert.That(slow.Select(value => value.EventOrdinal), Is.Ordered);
            Assert.That(slow[1].NormalizedTime, Is.EqualTo(10f / 30f).Within(0.0001f));

            var deduplicator = new MotionEventDeduplicator();
            Assert.That(deduplicator.TryAccept(1001, slow[1]), Is.True);
            Assert.That(deduplicator.TryAccept(1001, slow[1]), Is.False);
            Assert.That(deduplicator.TryAccept(1002, slow[1]), Is.True);

            var windows = new MotionWindowTracker(
                (sequence, windowId) => sequence == 42 && windowId == "attack.primary");
            Assert.That(windows.Apply(slow[1]), Is.True);
            Assert.That(windows.IsOpen("attack.primary"), Is.True);
            Assert.That(
                windows.Apply(timeline.Collect(0d, 0.5d, 1f, 41).ElementAt(1)),
                Is.False);
            windows.CloseAll(42);
            Assert.That(windows.IsOpen("attack.primary"), Is.False);
            Assert.That(windows.Apply(slow[2]), Is.False,
                "A late close cannot recreate or authorize a hitbox window.");
        }

        [Test]
        public void SocketsAttachmentsAndGenericLimbIkUseCanonicalBindings()
        {
            var root = new GameObject("rig");
            var motionRoot = Child(root.transform, "motion_root");
            var bodyRoot = Child(motionRoot, "body_root");
            var limb = Child(bodyRoot, "limb_front_01_l");
            var contact = Child(limb, "contact_front_l");
            var socket = Child(contact, "socket_contact_front_l");
            var target = new GameObject("ik_target").transform;
            var attachment = new GameObject("contact_marker");
            try
            {
                var sockets = new MotionSocketRegistry(
                    root.transform,
                    new[]
                    {
                        new MotionSocketDefinition(
                            "rmc_socket_contact_front_l_v001",
                            "motion_root/body_root/limb_front_01_l/contact_front_l/" +
                            "socket_contact_front_l")
                    },
                    new[]
                    {
                        new MotionSocketAlias(
                            "LegacyContactFrontL",
                            "rmc_socket_contact_front_l_v001")
                    });

                Assert.That(
                    sockets.TryResolve(
                        "rmc_socket_contact_front_l_v001",
                        out Transform resolved),
                    Is.True);
                Assert.That(resolved, Is.SameAs(socket));
                Assert.That(sockets.TryResolveAlias("legacycontactfrontl", out _), Is.False);
                Assert.That(sockets.TryResolveAlias("LegacyContactFrontL", out resolved), Is.True);
                Assert.That(sockets.Attach(attachment.transform, "rmc_socket_contact_front_l_v001"),
                    Is.True);
                Assert.That(attachment.transform.parent, Is.SameAs(socket));
                Assert.That(attachment.transform.localPosition, Is.EqualTo(Vector3.zero));

                contact.position = new Vector3(0f, 0.04f, 0f);
                var grounding = root.AddComponent<MotionGroundingDriver>();
                grounding.Configure(
                    new[]
                    {
                        new MotionGroundContactBinding(
                            "contact_front_l",
                            contact,
                            target,
                            false,
                            AvatarIKGoal.LeftFoot)
                    },
                    0.02f,
                    0.01f);
                grounding.SetContactWeight("contact_front_l", 1f);
                Assert.That(
                    grounding.ApplyGroundSample(
                        "contact_front_l",
                        new Vector3(0.08f, 0f, 0f),
                        Vector3.up),
                    Is.True);
                Assert.That(new Vector2(target.position.x, target.position.z).magnitude,
                    Is.EqualTo(0.02f).Within(0.0001f));
                Assert.That(target.position.y, Is.EqualTo(0.03f).Within(0.0001f));
                Assert.That(grounding.GetContactWeight("contact_front_l"), Is.EqualTo(1f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(attachment);
                UnityEngine.Object.DestroyImmediate(target.gameObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PlayableRuntimeBlendsAdditiveLayersAndKeepsSafeMotionOnMissingClip()
        {
            var root = new GameObject("motion_runtime");
            Transform chest = Child(root.transform, "chest");
            Animator animator = root.AddComponent<Animator>();
            AnimationClip idle = CreateClip("idle", "", "m_LocalPosition.x", 0f, 0f);
            AnimationClip attack = CreateClip("attack", "", "m_LocalPosition.z", 0f, 0.1f);
            AnimationClip reaction = CreateClip(
                "reaction",
                "chest",
                "localEulerAnglesRaw.y",
                0f,
                5f);
            var mask = new AvatarMask { name = "upper_body_mask" };
            mask.AddTransformPath(chest, true);
            try
            {
                var catalog = new MotionCatalogSnapshot(
                    "idle.neutral",
                    new[]
                    {
                        Definition("idle", "idle.neutral", idle, MotionPriority.Idle),
                        new MotionClipDefinition(
                            "rmc_clip_attack_basic_v001",
                            "attack.basic",
                            attack,
                            "idle.neutral",
                            MotionRootMode.Bounded,
                            MotionPriority.Attack,
                            false,
                            false)
                    });
                var controller = root.AddComponent<MotionRuntimeController>();
                controller.Configure(
                    animator,
                    catalog,
                    new[]
                    {
                        new MotionLayerDefinition(
                            "rmc_layer_champion_reaction_v001",
                            true,
                            mask,
                            40)
                    });

                Assert.That(controller.IsGraphValid, Is.True);
                Assert.That(controller.CurrentMotionKey, Is.EqualTo("idle.neutral"));
                controller.ConfigureEventRuntime(
                    root.GetInstanceID(),
                    new System.Collections.Generic.Dictionary<string, MotionEventTimeline>
                    {
                        ["rmc_clip_idle_v001"] = new MotionEventTimeline(
                            30,
                            31,
                            Array.Empty<MotionEventDefinition>())
                    },
                    null);
                Assert.DoesNotThrow(() => controller.Tick(0.01f));
                Assert.That(controller.RequestMotion("missing.optional", 1), Is.True);
                Assert.That(controller.LastRequestUsedFallback, Is.True);
                Assert.That(controller.CurrentMotionKey, Is.EqualTo("idle.neutral"));
                var loopingEvents =
                    new System.Collections.Generic.List<MotionEventDispatch>();
                controller.MotionEventDispatched += loopingEvents.Add;
                controller.ConfigureEventRuntime(
                    root.GetInstanceID(),
                    new System.Collections.Generic.Dictionary<string, MotionEventTimeline>
                    {
                        ["rmc_clip_idle_v001"] = new MotionEventTimeline(
                            30,
                            31,
                            new[]
                            {
                                new MotionEventDefinition(
                                    "rmc_event_phase_enter_v001",
                                    "al.motion.phase.enter",
                                    3,
                                    0,
                                    new MotionStaticPayload { Phase = "idle" })
                            })
                    },
                    null);
                controller.Tick(0.2f);
                controller.Tick(1f);
                Assert.That(loopingEvents, Has.Count.EqualTo(2));
                Assert.That(controller.RequestMotion("attack.basic", 2), Is.True);
                Assert.That(controller.CurrentMotionKey, Is.EqualTo("attack.basic"));
                MotionControllerProfile championProfile =
                    AssetDatabase.LoadAssetAtPath<MotionControllerProfile>(
                        "Assets/AL/Resources/Motion/Profiles/ChampionMotionControllerProfile.asset");
                MotionEventNameRegistry eventNames = MotionEventNameRegistry.FromManifestJson(
                    championProfile.RequiredMotionManifest.text);
                var animationEvents =
                    new System.Collections.Generic.List<MotionEventDispatch>();
                controller.MotionEventDispatched += animationEvents.Add;
                controller.ConfigureEventRuntime(
                    root.GetInstanceID(),
                    new System.Collections.Generic.Dictionary<string, MotionEventTimeline>(),
                    null,
                    eventNames);
                const string hitboxBeginEnvelope =
                    "{\"schemaVersion\":1," +
                    "\"eventId\":\"rmc_event_hitbox_request_begin_v001\"," +
                    "\"actionSequence\":0,\"eventOrdinal\":3,\"normalizedTime\":0," +
                    "\"windowId\":\"primary\"}";
                controller.AL_MotionEventV1(hitboxBeginEnvelope);
                controller.AL_MotionEventV1(hitboxBeginEnvelope);
                Assert.DoesNotThrow(() => controller.AL_MotionEventV1("{malformed"));
                Assert.That(animationEvents, Has.Count.EqualTo(1));
                Assert.That(
                    animationEvents[0].EventName,
                    Is.EqualTo("al.motion.hitbox.request_begin"));
                Assert.That(animationEvents[0].ActionSequence, Is.EqualTo(2));
                Assert.That(
                    controller.SetLayer(
                        "rmc_layer_champion_reaction_v001",
                        reaction,
                        0.75f,
                        1f),
                    Is.True);
                Assert.That(controller.ActiveLayerCount, Is.EqualTo(1));

                controller.Tick(0.2f);
                Assert.That(controller.CurrentLocalTime, Is.GreaterThan(0d));
                controller.Release();
                controller.Release();
                Assert.That(controller.IsGraphValid, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mask);
                UnityEngine.Object.DestroyImmediate(idle);
                UnityEngine.Object.DestroyImmediate(attack);
                UnityEngine.Object.DestroyImmediate(reaction);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ReusableChampionNpcAndBeastProfilesEncodeRigRetargetLayerAndImportPolicy()
        {
            MotionControllerProfile champion = AssetDatabase.LoadAssetAtPath<MotionControllerProfile>(
                "Assets/AL/Resources/Motion/Profiles/ChampionMotionControllerProfile.asset");
            MotionControllerProfile npc = AssetDatabase.LoadAssetAtPath<MotionControllerProfile>(
                "Assets/AL/Resources/Motion/Profiles/NpcMotionControllerProfile.asset");
            MotionControllerProfile beast = AssetDatabase.LoadAssetAtPath<MotionControllerProfile>(
                "Assets/AL/Resources/Motion/Profiles/BeastMotionControllerProfile.asset");
            MotionImportPreset humanoid = AssetDatabase.LoadAssetAtPath<MotionImportPreset>(
                "Assets/AL/Editor/Motion/ImportPresets/HumanoidMotionImportPreset.asset");
            MotionImportPreset generic = AssetDatabase.LoadAssetAtPath<MotionImportPreset>(
                "Assets/AL/Editor/Motion/ImportPresets/GenericExactMotionImportPreset.asset");
            MotionImportPreset slagwhistle = AssetDatabase.LoadAssetAtPath<MotionImportPreset>(
                "Assets/AL/Editor/Motion/ImportPresets/SlagwhistleExactMotionImportPreset.asset");

            Assert.That(champion, Is.Not.Null);
            Assert.That(npc, Is.Not.Null);
            Assert.That(beast, Is.Not.Null);
            Assert.That(humanoid, Is.Not.Null);
            Assert.That(generic, Is.Not.Null);
            Assert.That(slagwhistle, Is.Not.Null);
            Assert.That(champion.SubjectKind, Is.EqualTo(MotionSubjectKind.Champion));
            Assert.That(champion.RigClassification, Is.EqualTo(MotionRigClassification.Humanoid));
            Assert.That(champion.MaximumLayers, Is.EqualTo(4));
            Assert.That(npc.SubjectKind, Is.EqualTo(MotionSubjectKind.Npc));
            Assert.That(npc.MaximumLayers, Is.EqualTo(3));
            Assert.That(beast.SubjectKind, Is.EqualTo(MotionSubjectKind.Beast));
            Assert.That(beast.RigClassification, Is.EqualTo(MotionRigClassification.Generic));
            Assert.That(beast.MaximumLayers, Is.EqualTo(2));
            Assert.That(champion.RequiredMotionManifest, Is.Not.Null);
            MotionEventNameRegistry eventNames = MotionEventNameRegistry.FromManifestJson(
                champion.RequiredMotionManifest.text);
            Assert.That(eventNames.Count, Is.EqualTo(10));
            Assert.That(
                eventNames.TryResolve(
                    "rmc_event_hitbox_request_begin_v001",
                    out string hitboxEventName),
                Is.True);
            Assert.That(hitboxEventName, Is.EqualTo("al.motion.hitbox.request_begin"));
            Assert.That(humanoid.RigClassification, Is.EqualTo(MotionRigClassification.Humanoid));
            Assert.That(humanoid.SampleRateHz, Is.EqualTo(30));
            Assert.That(humanoid.BakeAxisConversion, Is.True);
            Assert.That(humanoid.GlobalScale, Is.EqualTo(1f));
            Assert.That(generic.RetargetMode, Is.EqualTo(MotionRetargetMode.GenericExactSignature));
            Assert.That(
                slagwhistle.RetargetProfileId,
                Is.EqualTo("rmc_retarget_slagwhistle_exact_v001"));

            var idle = new AnimationClip { name = "blend_idle" };
            var run = new AnimationClip { name = "blend_run" };
            try
            {
                var tree = new MotionBlendTree1D(
                    new[]
                    {
                        new MotionBlendPoint(
                            0f,
                            Definition("blend_idle", "idle.neutral", idle, MotionPriority.Idle)),
                        new MotionBlendPoint(
                            6f,
                            Definition("blend_run", "locomotion.run", run, MotionPriority.Locomotion))
                    });
                MotionBlendSample sample = tree.Evaluate(3f);
                Assert.That(sample.Lower.MotionKey, Is.EqualTo("idle.neutral"));
                Assert.That(sample.Upper.MotionKey, Is.EqualTo("locomotion.run"));
                Assert.That(sample.LowerWeight, Is.EqualTo(0.5f));
                Assert.That(sample.UpperWeight, Is.EqualTo(0.5f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(idle);
                UnityEngine.Object.DestroyImmediate(run);
            }
        }

        private static AnimationClip CreateClip(
            string name,
            string path,
            string property,
            float start,
            float end)
        {
            var clip = new AnimationClip { name = name, frameRate = 30f };
            clip.SetCurve(
                path,
                typeof(Transform),
                property,
                AnimationCurve.Linear(0f, start, 1f, end));
            return clip;
        }

        private static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static MotionClipDefinition Definition(
            string id,
            string key,
            AnimationClip clip,
            MotionPriority priority)
        {
            return new MotionClipDefinition(
                "rmc_clip_" + id + "_v001",
                key,
                clip,
                null,
                MotionRootMode.InPlace,
                priority,
                key == "idle.neutral",
                false);
        }
    }
}
