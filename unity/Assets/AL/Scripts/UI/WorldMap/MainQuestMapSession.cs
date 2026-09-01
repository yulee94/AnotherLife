using System;
using AL.Core;

namespace AL.UI.WorldMap
{
    public sealed class MainQuestMapState
    {
        internal MainQuestMapState(string objectiveId, RealmId realm, string whatToDo)
        {
            ObjectiveId = objectiveId ?? string.Empty;
            Realm = realm;
            WhatToDo = whatToDo ?? string.Empty;
        }

        public string ObjectiveId { get; }
        public RealmId Realm { get; }
        public string WhatToDo { get; }
    }

    public static class MainQuestMapSession
    {
        public static event Action Changed;

        public static MainQuestMapState Current { get; private set; }

        public static void Publish(string objectiveId, RealmId realm, string whatToDo)
        {
            Current = new MainQuestMapState(objectiveId, realm, whatToDo);
            Changed?.Invoke();
        }

        public static void Clear()
        {
            Current = null;
            Changed?.Invoke();
        }

        public static void ResetForTests()
        {
            Current = null;
            Changed = null;
        }
    }
}
