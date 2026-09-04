#!/usr/bin/env python3
from pathlib import Path
import json
import sys

from jsonschema import Draft202012Validator

root = Path(__file__).resolve().parents[2]
schema_path = root / "unity/SharedContracts/Schemas/al-main-quest-line-runtime.schema.json"
catalog_path = root / "unity/Assets/AL/StreamingAssets/GameData/al_main_quest_line_runtime.v1.json"
schema = json.loads(schema_path.read_text(encoding="utf-8"))
catalog = json.loads(catalog_path.read_text(encoding="utf-8"))
errors = sorted(Draft202012Validator(schema).iter_errors(catalog), key=lambda e: list(e.path))
if errors:
    for error in errors:
        print(error.message)
    raise SystemExit(1)
print("runtime catalog schema: PASS")
