namespace AL.UI.QuestHud
{
    /// <summary>
    /// Player-facing Quest HUD copy. Never emit catalog IDs, enum names, or
    /// outer-realm / Warzone identifiers before the gate prompt.
    /// </summary>
    public static class QuestHudCopy
    {
        public const string RootName = "QuestHud";
        public const string SlotName = "QuestHudSlot";
        public const string TitleName = "QuestHudTitle";
        public const string WhatName = "QuestHudWhat";
        public const string WhereName = "QuestHudWhere";
        public const string AcceptName = "QuestHudAccept";
        public const string ContinueName = "QuestHudContinue";
        public const string CompleteName = "QuestHudComplete";
        public const string AutoQuestName = "QuestHudAutoQuest";
        public const string HostName = "QuestHudHost";

        public const string Accept = "Accept";
        public const string Continue = "Continue";
        public const string Complete = "Complete";
        public const string AutoQuestOn = "Auto-Quest ON";
        public const string AutoQuestOff = "Auto-Quest OFF";

        public const string Capital = "Capital";
        public const string SkyCastle = "Sky Castle";
        public const string RealmGuide = "Realm Guide";
        public const string CovenantSite = "Covenant Site";
        public const string Castle = "Castle";
        public const string Areas = "Areas";
        public const string WarzoneGate = "Warzone Gate";

        public const string TeachStoresId = "teach_castle_stores";
        public const string TeachStoresTitle = "The Castle Board";
        public const string TeachStoresWhat = "Read the stores and the timers on the castle board.";

        public const string WarzoneGateId = "warzone_gate_prompt";
        public const string WarzoneGateTitle = "The Outer Gate";
        public const string WarzoneGateWhat = "Choose whether to enter the Warzone.";
    }
}
