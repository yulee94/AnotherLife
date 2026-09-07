using System;
using AL.Core;
using AL.UI.CharacterCreation;
using AL.UI.FirstUserIdentity;

namespace AL.Data.Runtime
{
    public sealed class MvpLoopSnapshot
    {
        public MvpLoopSnapshot(
            RealmId realm,
            ClassFamily? classFamily,
            bool identityConfirmed,
            string lastResultId,
            string lastBuildId,
            int lastBuildLevel,
            string username = "")
        {
            Realm = realm;
            ClassFamily = classFamily;
            IdentityConfirmed = identityConfirmed;
            LastResultId = lastResultId ?? string.Empty;
            LastBuildId = lastBuildId ?? string.Empty;
            LastBuildLevel = lastBuildLevel;
            Username = username ?? string.Empty;
        }

        public RealmId Realm { get; }
        public ClassFamily? ClassFamily { get; }
        public bool IdentityConfirmed { get; }
        public string LastResultId { get; }
        public string LastBuildId { get; }
        public int LastBuildLevel { get; }
        public string Username { get; }

        public FirstUserRace People =>
            FirstUserIdentityDerivation.TryDeriveRace(Realm, out FirstUserRace race)
                ? race
                : FirstUserRace.Unknown;

        public bool HasConfirmedChampion =>
            IdentityConfirmed &&
            FirstUserIdentityDerivation.IsSupportedRealm(Realm) &&
            ClassFamily.HasValue &&
            FirstUserIdentityDerivation.IsSupportedClassFamily(ClassFamily.Value) &&
            CharacterCreationIdentity.TryNormalize(Username, out _, out _);

        public bool ShouldSkipCreate => HasConfirmedChampion;
    }

    public enum MvpLoopPrepareDisposition
    {
        Prepared = 0,
        Duplicate = 1,
        Rejected = 2
    }

    public readonly struct MvpLoopCommitRequest
    {
        public MvpLoopCommitRequest(
            string transactionId,
            RealmId expectedRealm,
            ClassFamily classFamily,
            bool confirmIdentity,
            string lastResultId,
            string buildingId,
            int buildingLevel)
            : this(
                transactionId,
                expectedRealm,
                classFamily,
                confirmIdentity,
                lastResultId,
                buildingId,
                buildingLevel,
                string.Empty,
                null)
        {
        }

        public MvpLoopCommitRequest(
            string transactionId,
            RealmId expectedRealm,
            ClassFamily classFamily,
            bool confirmIdentity,
            string lastResultId,
            string buildingId,
            int buildingLevel,
            string username)
            : this(
                transactionId,
                expectedRealm,
                classFamily,
                confirmIdentity,
                lastResultId,
                buildingId,
                buildingLevel,
                username,
                null)
        {
        }

        public MvpLoopCommitRequest(
            string transactionId,
            RealmId expectedRealm,
            ClassFamily classFamily,
            bool confirmIdentity,
            string lastResultId,
            string buildingId,
            int buildingLevel,
            ChampionCustomizationState appearance)
            : this(
                transactionId,
                expectedRealm,
                classFamily,
                confirmIdentity,
                lastResultId,
                buildingId,
                buildingLevel,
                string.Empty,
                appearance)
        {
        }

        public MvpLoopCommitRequest(
            string transactionId,
            RealmId expectedRealm,
            ClassFamily classFamily,
            bool confirmIdentity,
            string lastResultId,
            string buildingId,
            int buildingLevel,
            string username,
            ChampionCustomizationState appearance)
        {
            TransactionId = transactionId ?? string.Empty;
            ExpectedRealm = expectedRealm;
            ClassFamily = classFamily;
            ConfirmIdentity = confirmIdentity;
            LastResultId = lastResultId ?? string.Empty;
            BuildingId = buildingId ?? string.Empty;
            BuildingLevel = buildingLevel;
            Username = username ?? string.Empty;
            Appearance = appearance;
        }

        public string TransactionId { get; }
        public RealmId ExpectedRealm { get; }
        public ClassFamily ClassFamily { get; }
        public bool ConfirmIdentity { get; }
        public string LastResultId { get; }
        public string BuildingId { get; }
        public int BuildingLevel { get; }
        public string Username { get; }
        public ChampionCustomizationState Appearance { get; }
    }

    /// <summary>
    /// Read/write helper for the 3D-first MVP loop persisted in
    /// <c>SaveGameData.ChampionCustomization</c> plus the existing
    /// <c>SelectedRealm</c> and <c>Buildings</c> slots. No new top-level field.
    /// </summary>
    public static class MvpLoopSaveCodec
    {
        public const string PersistenceSlot = "SaveGameData.ChampionCustomization";
        public const string PersistenceSlotPath = "$.ChampionCustomization";
        public const string OneBuildSlot = "SaveGameData.Buildings";
        public const string RealmSlot = "SaveGameData.SelectedRealm";
        public const string DefaultOneBuildId = "TownHall";

        public static MvpLoopSnapshot Read(SaveGameData save)
        {
            if (save == null)
            {
                return new MvpLoopSnapshot(
                    RealmId.None,
                    null,
                    false,
                    string.Empty,
                    string.Empty,
                    0);
            }

            ChampionCustomizationState customization = save.ChampionCustomization;
            ClassFamily? classFamily = null;
            if (customization != null &&
                TryDecodeClassFamily(customization.ClassFamilyId, out ClassFamily decoded))
            {
                classFamily = decoded;
            }

            string lastBuildId = string.Empty;
            int lastBuildLevel = 0;
            if (save.Buildings != null)
            {
                for (int i = 0; i < save.Buildings.Count; i++)
                {
                    BuildingState building = save.Buildings[i];
                    if (building == null || string.IsNullOrWhiteSpace(building.BuildingId))
                    {
                        continue;
                    }

                    lastBuildId = building.BuildingId;
                    lastBuildLevel = building.Level;
                    break;
                }
            }

            return new MvpLoopSnapshot(
                save.SelectedRealm,
                classFamily,
                customization != null && customization.IdentityConfirmed,
                customization == null ? string.Empty : customization.LastResultId ?? string.Empty,
                lastBuildId,
                lastBuildLevel,
                customization == null ? string.Empty : customization.Username ?? string.Empty);
        }

        public static void RestoreSessionIdentity(SaveGameData save)
        {
            MvpLoopSnapshot snapshot = Read(save);
            if (!string.IsNullOrEmpty(snapshot.Username))
            {
                CharacterCreationIdentity.RememberPersisted(snapshot.Username);
            }

            if (!snapshot.HasConfirmedChampion)
            {
                return;
            }

            if (SliceRunState.HasConfirmedChampion)
            {
                if (SliceRunState.Champion != null &&
                    string.IsNullOrWhiteSpace(SliceRunState.Champion.Username))
                {
                    SliceRunState.Champion.Username = snapshot.Username;
                    SliceRunState.Champion.Family = snapshot.ClassFamily.Value;
                    SliceRunState.Champion.Realm = snapshot.Realm;
                }

                return;
            }

            SliceRunState.ConfirmChampion(new ChampionState
            {
                Username = snapshot.Username,
                Family = snapshot.ClassFamily.Value,
                Realm = snapshot.Realm
            });
        }

        public static FirstUserRouteSnapshot ToRouteSnapshot(
            MvpLoopSnapshot snapshot,
            bool hostReady,
            bool writable)
        {
            snapshot = snapshot ?? Read(null);
            bool realm = FirstUserIdentityDerivation.IsSupportedRealm(snapshot.Realm);
            bool classFamily = snapshot.ClassFamily.HasValue &&
                FirstUserIdentityDerivation.IsSupportedClassFamily(snapshot.ClassFamily.Value);
            bool confirmed = snapshot.HasConfirmedChampion;
            FirstUserJourneyStep cursorStep = confirmed
                ? FirstUserJourneyStep.Complete
                : !realm
                    ? FirstUserJourneyStep.Realm
                    : !classFamily
                        ? FirstUserJourneyStep.ClassSelection
                        : FirstUserJourneyStep.Customization;
            return new FirstUserRouteSnapshot(
                realmValidated: realm,
                originRaceValidated: realm,
                classSelectionValidated: classFamily,
                customizationValidated: confirmed,
                handleValidated: confirmed,
                authoritativeReceiptVerified: confirmed,
                localProjectionVerified: confirmed,
                hostReady: hostReady,
                writable: writable,
                evidenceOrigin: FirstUserRouteEvidenceOrigin.ProductionAuthority,
                cursor: new FirstUserRouteCursorEvidence(
                    confirmed || realm || classFamily
                        ? FirstUserRouteCursorState.Matching
                        : FirstUserRouteCursorState.Missing,
                    confirmed || realm || classFamily
                        ? cursorStep
                        : FirstUserJourneyStep.Invalid));
        }

        public static bool TryEncodeClassFamily(ClassFamily classFamily, out string id)
        {
            switch (classFamily)
            {
                case ClassFamily.Warrior:
                    id = "warrior";
                    return true;
                case ClassFamily.Mage:
                    id = "mage";
                    return true;
                case ClassFamily.Ranger:
                    id = "ranger";
                    return true;
                case ClassFamily.Assassin:
                    id = "assassin";
                    return true;
                default:
                    id = string.Empty;
                    return false;
            }
        }

        public static bool TryDecodeClassFamily(string id, out ClassFamily classFamily)
        {
            if (string.Equals(id, "warrior", StringComparison.Ordinal))
            {
                classFamily = ClassFamily.Warrior;
                return true;
            }

            if (string.Equals(id, "mage", StringComparison.Ordinal))
            {
                classFamily = ClassFamily.Mage;
                return true;
            }

            if (string.Equals(id, "ranger", StringComparison.Ordinal))
            {
                classFamily = ClassFamily.Ranger;
                return true;
            }

            if (string.Equals(id, "assassin", StringComparison.Ordinal))
            {
                classFamily = ClassFamily.Assassin;
                return true;
            }

            classFamily = default;
            return false;
        }

        public static bool IsAllowedLastResultId(string lastResultId)
        {
            if (string.IsNullOrEmpty(lastResultId))
            {
                return true;
            }

            if (lastResultId.Length > 64)
            {
                return false;
            }

            for (int i = 0; i < lastResultId.Length; i++)
            {
                char c = lastResultId[i];
                bool allowed = (c >= 'a' && c <= 'z') ||
                               (c >= '0' && c <= '9') ||
                               c == '_' ||
                               c == ':';
                if (!allowed)
                {
                    return false;
                }
            }

            return true;
        }

        public static bool TryNormalizeUsername(string raw, out string normalized)
        {
            if (string.IsNullOrEmpty(raw))
            {
                normalized = string.Empty;
                return true;
            }

            return CharacterCreationIdentity.TryNormalize(raw, out normalized, out _);
        }

        public static MvpLoopPrepareDisposition PrepareCandidate(
            SaveGameData candidate,
            MvpLoopCommitRequest request,
            out string message)
        {
            message = string.Empty;
            if (candidate == null)
            {
                message = "AL-MVP-LOOP-AUTHORITY-CONFLICT";
                return MvpLoopPrepareDisposition.Rejected;
            }

            if (string.IsNullOrWhiteSpace(request.TransactionId))
            {
                message = "AL-MVP-LOOP-TRANSACTION-INVALID";
                return MvpLoopPrepareDisposition.Rejected;
            }

            if (candidate.SelectedRealm != request.ExpectedRealm ||
                !FirstUserIdentityDerivation.IsSupportedRealm(request.ExpectedRealm) ||
                !TryEncodeClassFamily(request.ClassFamily, out string classFamilyId) ||
                !IsAllowedLastResultId(request.LastResultId) ||
                !TryNormalizeUsername(request.Username, out string incomingUsername))
            {
                message = "AL-MVP-LOOP-REQUEST-INVALID";
                return MvpLoopPrepareDisposition.Rejected;
            }

            if (request.ConfirmIdentity && string.IsNullOrEmpty(classFamilyId))
            {
                message = "AL-MVP-LOOP-REQUEST-INVALID";
                return MvpLoopPrepareDisposition.Rejected;
            }

            if (!string.IsNullOrEmpty(request.BuildingId) &&
                (request.BuildingLevel < 1 || request.BuildingId.Length > 64))
            {
                message = "AL-MVP-LOOP-REQUEST-INVALID";
                return MvpLoopPrepareDisposition.Rejected;
            }

            candidate.ChampionCustomization ??= new ChampionCustomizationState();
            ChampionCustomizationState customization = candidate.ChampionCustomization;
            bool sameClass = string.Equals(
                customization.ClassFamilyId ?? string.Empty,
                classFamilyId,
                StringComparison.Ordinal);
            bool sameConfirm = customization.IdentityConfirmed == request.ConfirmIdentity;
            bool sameResult = string.IsNullOrEmpty(request.LastResultId) || string.Equals(
                customization.LastResultId ?? string.Empty,
                request.LastResultId ?? string.Empty,
                StringComparison.Ordinal);
            bool sameUsername = string.IsNullOrEmpty(incomingUsername) ||
                                string.Equals(
                                    customization.Username ?? string.Empty,
                                    incomingUsername,
                                    StringComparison.Ordinal);
            bool sameBuild = string.IsNullOrEmpty(request.BuildingId) ||
                             HasBuilding(candidate, request.BuildingId, request.BuildingLevel);
            bool sameLook = request.Appearance == null ||
                            AL.UI.CharacterCreation.CharacterCreationLook.Matches(customization, request.Appearance);
            if (sameClass && sameConfirm && sameResult && sameUsername && sameBuild && sameLook)
            {
                return MvpLoopPrepareDisposition.Duplicate;
            }

            if (request.Appearance != null)
            {
                AL.UI.CharacterCreation.CharacterCreationLook.CopyInto(customization, request.Appearance);
            }

            customization.ClassFamilyId = classFamilyId;
            if (request.ConfirmIdentity)
            {
                customization.IdentityConfirmed = true;
            }

            if (!string.IsNullOrEmpty(request.LastResultId))
            {
                customization.LastResultId = request.LastResultId;
            }

            if (!string.IsNullOrEmpty(incomingUsername))
            {
                customization.Username = incomingUsername;
            }

            if (!string.IsNullOrEmpty(request.BuildingId))
            {
                ApplyOneBuild(candidate, request.BuildingId, request.BuildingLevel);
            }

            return MvpLoopPrepareDisposition.Prepared;
        }

        public static void ApplyOneBuild(SaveGameData save, string buildingId, int level)
        {
            if (save == null || string.IsNullOrWhiteSpace(buildingId) || level < 1)
            {
                return;
            }

            save.Buildings ??= new System.Collections.Generic.List<BuildingState>();
            for (int i = 0; i < save.Buildings.Count; i++)
            {
                BuildingState existing = save.Buildings[i];
                if (existing != null &&
                    string.Equals(existing.BuildingId, buildingId, StringComparison.Ordinal))
                {
                    existing.Level = level;
                    return;
                }
            }

            save.Buildings.Add(new BuildingState
            {
                BuildingId = buildingId,
                Level = level
            });
        }

        private static bool HasBuilding(SaveGameData save, string buildingId, int level)
        {
            if (save?.Buildings == null)
            {
                return false;
            }

            for (int i = 0; i < save.Buildings.Count; i++)
            {
                BuildingState building = save.Buildings[i];
                if (building != null &&
                    string.Equals(building.BuildingId, buildingId, StringComparison.Ordinal) &&
                    building.Level == level)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
