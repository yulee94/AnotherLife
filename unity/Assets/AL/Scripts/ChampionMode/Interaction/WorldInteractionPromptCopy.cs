namespace AL.ChampionMode.Interaction
{
    /// <summary>
    /// Player-facing prompt strings. Verbs are Talk/Use. Subjects are the authored
    /// C1 role/site nouns — no invented NPC names.
    /// </summary>
    public static class WorldInteractionPromptCopy
    {
        public const string InteractGlyph = "F";
        public const string TalkVerb = "Talk";
        public const string UseVerb = "Use";
        public const string GuideSubject = "Realm Guide";
        public const string CovenantSiteSubject = "Covenant Site";
        public const string GuideObjectiveText =
            "Meet the realm guide who interprets the Celestial Tear's response.";
        public const string CovenantObjectiveText =
            "Restore the damaged covenant site without sacrificing its keepers.";

        public static string Verb(WorldInteractionKind kind)
        {
            return kind == WorldInteractionKind.Use ? UseVerb : TalkVerb;
        }

        public static string Subject(string catalogId)
        {
            if (catalogId == FirstSessionWorldInteractables.CovenantSiteCatalogId)
            {
                return CovenantSiteSubject;
            }

            if (catalogId == FirstSessionWorldInteractables.GuideCatalogId)
            {
                return GuideSubject;
            }

            return string.Empty;
        }

        public static string ObjectiveText(string catalogId)
        {
            if (catalogId == FirstSessionWorldInteractables.CovenantSiteCatalogId)
            {
                return CovenantObjectiveText;
            }

            if (catalogId == FirstSessionWorldInteractables.GuideCatalogId)
            {
                return GuideObjectiveText;
            }

            return string.Empty;
        }

        public static string Compose(string glyph, WorldInteractionKind kind, string catalogId)
        {
            string subject = Subject(catalogId);
            if (string.IsNullOrEmpty(subject))
            {
                return string.Empty;
            }

            string key = string.IsNullOrEmpty(glyph) ? InteractGlyph : glyph;
            return "[" + key + "]  " + Verb(kind).ToUpperInvariant() + "   " + subject;
        }
    }
}
