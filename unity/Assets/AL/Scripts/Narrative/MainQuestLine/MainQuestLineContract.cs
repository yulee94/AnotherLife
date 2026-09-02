using System;

namespace AL.Narrative.MainQuestLine
{
    public static class MainQuestLineContract
    {
        public const string CatalogId = "al_main_quest_line_runtime";
        public const int SchemaVersion = 1;
        public const string PacketId = "ANOTHERLIFE_MAIN_QUEST_LINE";
        public const string PacketVersion = "anotherlife-main-quest-line-2026-07-23-v001";
        public const string SourceStatus = "canonical_narrative_source_complete_runtime_wired";
        public const string FileName = "al_main_quest_line_runtime.v1.json";
        public const string RelativePath = "GameData/al_main_quest_line_runtime.v1.json";
        public const string DeliveryMechanism = "hybrid_local_streaming_assets_gamedata";
        public const string EntryChapterId = "CH00_FIRST_SIGNAL";
        public const string EntryQuestId = "OMEN_1";
        public const string EntryScene = "Kingdom";
        public const string ProgressEvent = "QUEST_ACCEPTED";
        public const string ProgressedStateId = "TALK_TO_VALERIUS";
        public const string AcceptChoiceKey = "choice.omen1.accept";
        public const string ProofQuestId = "MQ_C1_PROOF_OF_WORTH";
        public const string OmenCompletedStateId = "COMPLETED";
        public const int ChapterCount = 15;
        public const int CanonicalByteLength = 8232;
        public const string CanonicalSha256 =
            "254e60de33a3d0e5ce9b686e2ed6f35d605d056bbd714bfbc92ff3a5809a064b";
        public const string EnabledSceneManifestFileName = "al_enabled_scene_manifest.v1.json";
        public const string GeneratedSceneManifestFileName = "al_generated_scene_manifest.v1.json";
        public const string DiagnosticPrefix = "AL-NARRATIVE-";
        public const string ActiveMarker = "[AL-NARRATIVE-ACTIVE]";
        public const string MissingMarker = "[AL-NARRATIVE-MISSING]";
        public const string FailedMarker = "[AL-NARRATIVE-FAILED]";
        public const string ProgressMarker = "[AL-NARRATIVE-PROGRESS]";
        public const string ResumedMarker = "[AL-NARRATIVE-RESUMED]";
        public const string SmokeEnableArgument = "--al-narrative-smoke";
        public const string SmokeOutputArgument = "--al-narrative-output";
        public const string PassStatus = "passed";
        public const string PassReason = "narrative_representative_path";
        public const int MaximumByteLength = 65536;
    }

    public enum MainQuestLineLoadStatus
    {
        Succeeded = 0,
        NotFound = 1,
        Malformed = 2,
        IdentityMismatch = 3
    }

    public sealed class MainQuestLineDiagnostic
    {
        public MainQuestLineDiagnostic(string code, string message, string expected, string actual)
        {
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            Expected = expected ?? string.Empty;
            Actual = actual ?? string.Empty;
        }

        public string Code { get; }
        public string Message { get; }
        public string Expected { get; }
        public string Actual { get; }

        public override string ToString()
        {
            return Code + ": " + Message + " expected=" + Expected + " actual=" + Actual;
        }
    }

    [Serializable]
    public sealed class MainQuestLineChapterRecord
    {
        public string id;
        public int order;
        public string mainQuestId;
        public string playMode;
        public string unlocksMainQuestId;
        public string runtimeBinding;
        public string entryScene;
        public string titleKey;
        public string titleText;
        public string[] sideQuestIds;
    }

    [Serializable]
    public sealed class MainQuestLineCatalogFile
    {
        public int schemaVersion;
        public string catalogId;
        public string packetId;
        public string packetVersion;
        public string sourceStatus;
        public string deliveryMechanism;
        public string relativePath;
        public string sourceManifestSha256;
        public string entryChapterId;
        public string entryQuestId;
        public string entryScene;
        public string progressEvent;
        public string progressedStateId;
        public string acceptChoiceKey;
        public MainQuestLineChapterRecord[] chapters;
        public string[] criticalPath;
    }

    public sealed class MainQuestLineChapter
    {
        internal MainQuestLineChapter(MainQuestLineChapterRecord record)
        {
            Id = record.id ?? string.Empty;
            Order = record.order;
            MainQuestId = record.mainQuestId ?? string.Empty;
            PlayMode = record.playMode ?? string.Empty;
            UnlocksMainQuestId = record.unlocksMainQuestId ?? string.Empty;
            RuntimeBinding = record.runtimeBinding ?? string.Empty;
            EntryScene = record.entryScene ?? string.Empty;
            TitleKey = record.titleKey ?? string.Empty;
            TitleText = record.titleText ?? string.Empty;
            SideQuestIds = record.sideQuestIds ?? Array.Empty<string>();
        }

        public string Id { get; }
        public int Order { get; }
        public string MainQuestId { get; }
        public string PlayMode { get; }
        public string UnlocksMainQuestId { get; }
        public string RuntimeBinding { get; }
        public string EntryScene { get; }
        public string TitleKey { get; }
        public string TitleText { get; }
        public string[] SideQuestIds { get; }
    }

    public sealed class MainQuestLineProgress
    {
        internal MainQuestLineProgress(MainQuestLineChapter chapter, string questStateId)
        {
            Chapter = chapter;
            QuestStateId = questStateId ?? string.Empty;
        }

        public MainQuestLineChapter Chapter { get; }
        public string QuestStateId { get; }
        public string ChapterId => Chapter != null ? Chapter.Id : string.Empty;
        public string QuestId => Chapter != null ? Chapter.MainQuestId : string.Empty;
    }
}
