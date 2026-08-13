using System;
using AL.Core;

namespace AL.UI.FirstUserIdentity
{
    public enum FirstUserRace
    {
        Unknown = 0,
        Humans = 1,
        Dwarves = 2,
        Elves = 3,
        DarkElves = 4
    }

    public enum FirstUserIdentityDraftStep
    {
        Realm = 0,
        ClassFamily = 1,
        CustomizationReady = 2
    }

    public enum FirstUserIdentityDraftTransitionStatus
    {
        Applied = 0,
        WrongStep = 1,
        SelectionRequired = 2,
        InvalidRealm = 3,
        InvalidClassFamily = 4,
        DraftClosed = 5
    }

    public sealed class FirstUserIdentityDraftSnapshot
    {
        public FirstUserIdentityDraftSnapshot(
            FirstUserIdentityDraftStep step,
            RealmId realm,
            FirstUserRace race,
            ClassFamily? classFamily)
        {
            Step = step;
            Realm = realm;
            Race = race;
            ClassFamily = classFamily;
        }

        public FirstUserIdentityDraftStep Step { get; }
        public RealmId Realm { get; }
        public FirstUserRace Race { get; }
        public ClassFamily? ClassFamily { get; }

        public bool HasRealm =>
            FirstUserIdentityDerivation.IsSupportedRealm(Realm) &&
            FirstUserIdentityDerivation.IsSupportedRace(Race);

        public bool HasClassFamily =>
            ClassFamily.HasValue &&
            FirstUserIdentityDerivation.IsSupportedClassFamily(ClassFamily.Value);

        public bool IsCustomizationReady =>
            Step == FirstUserIdentityDraftStep.CustomizationReady &&
            HasRealm &&
            HasClassFamily;
    }

    public sealed class FirstUserIdentityDraftTransitionResult
    {
        public FirstUserIdentityDraftTransitionResult(
            FirstUserIdentityDraftTransitionStatus status,
            FirstUserIdentityDraftSnapshot snapshot)
        {
            Status = status;
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        public FirstUserIdentityDraftTransitionStatus Status { get; }
        public FirstUserIdentityDraftSnapshot Snapshot { get; }
        public bool WasApplied => Status == FirstUserIdentityDraftTransitionStatus.Applied;
    }

    public static class FirstUserIdentityDerivation
    {
        public static bool TryDeriveRace(RealmId realm, out FirstUserRace race)
        {
            switch (realm)
            {
                case RealmId.Crownlands:
                    race = FirstUserRace.Humans;
                    return true;
                case RealmId.Stonehold:
                    race = FirstUserRace.Dwarves;
                    return true;
                case RealmId.Eldergrove:
                    race = FirstUserRace.Elves;
                    return true;
                case RealmId.Umbral:
                    race = FirstUserRace.DarkElves;
                    return true;
                default:
                    race = FirstUserRace.Unknown;
                    return false;
            }
        }

        public static bool IsSupportedRealm(RealmId realm)
        {
            return TryDeriveRace(realm, out _);
        }

        public static bool IsSupportedRace(FirstUserRace race)
        {
            return race == FirstUserRace.Humans ||
                   race == FirstUserRace.Dwarves ||
                   race == FirstUserRace.Elves ||
                   race == FirstUserRace.DarkElves;
        }

        public static bool IsSupportedClassFamily(ClassFamily classFamily)
        {
            return classFamily == AL.Core.ClassFamily.Warrior ||
                   classFamily == AL.Core.ClassFamily.Mage ||
                   classFamily == AL.Core.ClassFamily.Ranger ||
                   classFamily == AL.Core.ClassFamily.Assassin;
        }
    }

    public sealed class FirstUserIdentityDraftFlow
    {
        private FirstUserIdentityDraftStep _step = FirstUserIdentityDraftStep.Realm;
        private RealmId _realm = RealmId.None;
        private FirstUserRace _race = FirstUserRace.Unknown;
        private ClassFamily? _classFamily;

        public FirstUserIdentityDraftSnapshot Snapshot => CreateSnapshot();

        public FirstUserIdentityDraftTransitionResult PreviewRealm(RealmId realm)
        {
            FirstUserIdentityDraftTransitionResult closed = RejectIfClosed();
            if (closed != null)
            {
                return closed;
            }

            if (_step != FirstUserIdentityDraftStep.Realm)
            {
                return Result(FirstUserIdentityDraftTransitionStatus.WrongStep);
            }

            if (!FirstUserIdentityDerivation.TryDeriveRace(realm, out FirstUserRace race))
            {
                return Result(FirstUserIdentityDraftTransitionStatus.InvalidRealm);
            }

            _realm = realm;
            _race = race;
            _classFamily = null;
            return Result(FirstUserIdentityDraftTransitionStatus.Applied);
        }

        public FirstUserIdentityDraftTransitionResult ConfirmRealmPreview()
        {
            FirstUserIdentityDraftTransitionResult closed = RejectIfClosed();
            if (closed != null)
            {
                return closed;
            }

            if (_step != FirstUserIdentityDraftStep.Realm)
            {
                return Result(FirstUserIdentityDraftTransitionStatus.WrongStep);
            }

            if (!CreateSnapshot().HasRealm)
            {
                return Result(FirstUserIdentityDraftTransitionStatus.SelectionRequired);
            }

            _step = FirstUserIdentityDraftStep.ClassFamily;
            _classFamily = null;
            return Result(FirstUserIdentityDraftTransitionStatus.Applied);
        }

        public FirstUserIdentityDraftTransitionResult PreviewClassFamily(ClassFamily classFamily)
        {
            FirstUserIdentityDraftTransitionResult closed = RejectIfClosed();
            if (closed != null)
            {
                return closed;
            }

            if (_step != FirstUserIdentityDraftStep.ClassFamily)
            {
                return Result(FirstUserIdentityDraftTransitionStatus.WrongStep);
            }

            if (!FirstUserIdentityDerivation.IsSupportedClassFamily(classFamily))
            {
                return Result(FirstUserIdentityDraftTransitionStatus.InvalidClassFamily);
            }

            _classFamily = classFamily;
            return Result(FirstUserIdentityDraftTransitionStatus.Applied);
        }

        public FirstUserIdentityDraftTransitionResult ReturnToRealmPreview()
        {
            FirstUserIdentityDraftTransitionResult closed = RejectIfClosed();
            if (closed != null)
            {
                return closed;
            }

            if (_step != FirstUserIdentityDraftStep.ClassFamily)
            {
                return Result(FirstUserIdentityDraftTransitionStatus.WrongStep);
            }

            _step = FirstUserIdentityDraftStep.Realm;
            _classFamily = null;
            return Result(FirstUserIdentityDraftTransitionStatus.Applied);
        }

        public FirstUserIdentityDraftTransitionResult ConfirmDraftForCustomization()
        {
            FirstUserIdentityDraftTransitionResult closed = RejectIfClosed();
            if (closed != null)
            {
                return closed;
            }

            if (_step != FirstUserIdentityDraftStep.ClassFamily)
            {
                return Result(FirstUserIdentityDraftTransitionStatus.WrongStep);
            }

            FirstUserIdentityDraftSnapshot snapshot = CreateSnapshot();
            if (!snapshot.HasRealm || !snapshot.HasClassFamily)
            {
                return Result(FirstUserIdentityDraftTransitionStatus.SelectionRequired);
            }

            _step = FirstUserIdentityDraftStep.CustomizationReady;
            return Result(FirstUserIdentityDraftTransitionStatus.Applied);
        }

        private FirstUserIdentityDraftTransitionResult RejectIfClosed()
        {
            return _step == FirstUserIdentityDraftStep.CustomizationReady
                ? Result(FirstUserIdentityDraftTransitionStatus.DraftClosed)
                : null;
        }

        private FirstUserIdentityDraftTransitionResult Result(
            FirstUserIdentityDraftTransitionStatus status)
        {
            return new FirstUserIdentityDraftTransitionResult(status, CreateSnapshot());
        }

        private FirstUserIdentityDraftSnapshot CreateSnapshot()
        {
            return new FirstUserIdentityDraftSnapshot(_step, _realm, _race, _classFamily);
        }
    }
}
