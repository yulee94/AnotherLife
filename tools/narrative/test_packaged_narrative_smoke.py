#!/usr/bin/env python3
"""Unit tests for packaged narrative evidence evaluation."""

from __future__ import annotations

import unittest

from packaged_narrative_smoke import (
    compare_editor_and_package,
    evaluate_evidence_document,
    evaluate_player_log,
)


def sample_evidence(*, editor: bool) -> dict:
    return {
        "schemaVersion": 1,
        "status": "passed",
        "reasonCode": "narrative_representative_path",
        "applicationIsEditor": editor,
        "unityVersion": "6000.3.22f1",
        "buildGuid": "editor" if editor else "player-guid",
        "enabledSceneManifestSha256": "a" * 64,
        "generatedSceneManifestSha256": "b" * 64,
        "narrativeCatalogSha256": "c" * 64,
        "narrativePacketVersion": "anotherlife-main-quest-line-2026-07-23-v001",
        "entryChapterId": "CH00_FIRST_SIGNAL",
        "entryQuestId": "OMEN_1",
        "progressedQuestStateId": "TALK_TO_VALERIUS",
        "resumedQuestStateId": "TALK_TO_VALERIUS",
        "sceneSequence": ["Boot", "Kingdom"],
        "isolatedSaveClaimed": True,
    }


class PackagedNarrativeSmokeTests(unittest.TestCase):
    def test_valid_evidence_passes(self) -> None:
        result = evaluate_evidence_document(sample_evidence(editor=False))
        self.assertEqual(result["status"], "passed")

    def test_missing_content_marker_is_stop_ship(self) -> None:
        result = evaluate_player_log("[AL-NARRATIVE-MISSING] catalog=al_main_quest_line_runtime")
        self.assertEqual(result["status"], "stop_ship")
        self.assertEqual(result["reasonCode"], "narrative_failure_token")

    def test_resume_mismatch_is_stop_ship(self) -> None:
        evidence = sample_evidence(editor=False)
        evidence["resumedQuestStateId"] = "OFFERED"
        result = evaluate_evidence_document(evidence)
        self.assertEqual(result["status"], "stop_ship")
        self.assertEqual(result["reasonCode"], "narrative_resume_mismatch")

    def test_editor_package_hash_divergence_is_stop_ship(self) -> None:
        editor = sample_evidence(editor=True)
        packaged = sample_evidence(editor=False)
        packaged["narrativeCatalogSha256"] = "d" * 64
        result = compare_editor_and_package(editor, packaged)
        self.assertEqual(result["status"], "stop_ship")
        self.assertEqual(result["reasonCode"], "editor_package_divergence")
        self.assertEqual(result["divergedFields"], ["narrativeCatalogSha256"])

    def test_editor_package_equivalent_when_material_fields_match(self) -> None:
        result = compare_editor_and_package(
            sample_evidence(editor=True),
            sample_evidence(editor=False),
        )
        self.assertEqual(result["status"], "passed")
        self.assertEqual(result["reasonCode"], "editor_package_equivalent")


if __name__ == "__main__":
    unittest.main()
