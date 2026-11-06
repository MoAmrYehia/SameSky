"""Export the shared JSON contracts: contracts/openapi.json (every endpoint) and the RecapPlan schema.

Run: api/.venv/Scripts/python scripts/export_contracts.py
"""

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / "api"))

from samesky.main import create_app  # noqa: E402

ref = {"type": "object", "required": ["momentId", "sourceVersion", "sharingVersion"], "additionalProperties": False, "properties": {
    "momentId": {"type": "string", "format": "uuid"},
    "sourceVersion": {"type": "integer", "minimum": 1},
    "sharingVersion": {"type": "integer", "minimum": 1},
    "transcriptStartMs": {"type": "integer", "minimum": 0},
    "transcriptEndMs": {"type": "integer", "minimum": 0},
}}
recap_plan = {
    "$schema": "https://json-schema.org/draft/2020-12/schema",
    "$id": "https://samesky.invalid/contracts/recap-plan.schema.json",
    "title": "RecapPlan",
    "description": "Validated server-side in api/samesky/services/recaps.py::validate_plan. The server fills every identity and version field; the model only supplies prose, ids from the provided list and layout tokens.",
    "type": "object",
    "required": ["schemaVersion", "recapId", "roomId", "audienceSnapshotId", "roomDate", "roomTimeZone", "title", "chapters"],
    "properties": {
        "schemaVersion": {"const": 2},
        "recapId": {"type": "string"},
        "roomId": {"type": "string"},
        "audienceSnapshotId": {"type": "string", "description": "Echo of the server-selected audience; can never be chosen or widened by the model."},
        "roomDate": {"type": "string", "format": "date"},
        "roomTimeZone": {"type": "string", "description": "IANA zone of the room (stable reporting zone)."},
        "title": {"type": "string", "minLength": 1, "maxLength": 80},
        "closingLine": {"type": "string", "maxLength": 200},
        "omittedCount": {"type": "integer", "minimum": 0},
        "fallback": {"const": "chronological"},
        "chapters": {"type": "array", "minItems": 1, "items": {
            "type": "object",
            "required": ["contributorId", "segments", "playbackMomentIds", "objectType", "durationSeconds"],
            "properties": {
                "contributorId": {"type": "string"},
                "segments": {"type": "array", "minItems": 1, "items": {"type": "object", "required": ["text", "kind", "sources"], "properties": {
                    "text": {"type": "string", "minLength": 1, "maxLength": 400},
                    "kind": {"enum": ["stated", "visual_interpretation", "quote"]},
                    "sources": {"type": "array", "minItems": 1, "items": ref},
                }}},
                "playbackMomentIds": {"type": "array", "items": {"type": "string"}},
                "objectType": {"enum": ["frame", "cup", "postcard", "audioStone"]},
                "durationSeconds": {"type": "integer", "minimum": 5, "maximum": 60},
            },
        }},
    },
}
(ROOT / "contracts").mkdir(exist_ok=True)
(ROOT / "contracts" / "openapi.json").write_text(json.dumps(create_app().openapi(), indent=2), encoding="utf-8")
(ROOT / "contracts" / "recap-plan.schema.json").write_text(json.dumps(recap_plan, indent=2), encoding="utf-8")
print("wrote contracts/openapi.json and contracts/recap-plan.schema.json")
