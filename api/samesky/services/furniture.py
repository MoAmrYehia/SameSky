"""The furniture marketplace: one catalogue, and each room's furnishing choices.

The catalogue (``furniture_catalog.json``) lists the pieces a room can hold, by slot: one table, the chairs, a rug,
a lamp, a plant, the wall colour, the floor. Every piece is included: nothing is priced, sold or owned, so there are
no purchases, balances or entitlements to keep. A room stores only which piece and finish fills each slot.

Any active member may furnish the room: it is the room they share. A room that has never been furnished has the
defaults (the first piece of each slot in its first finish) at version 0.
"""

from __future__ import annotations

import json
from pathlib import Path

from sqlalchemy.orm import Session

from ..errors import conflict, invalid
from ..models import RoomDecor, User, utcnow
from .access import require_member

CATALOG_PATH = Path(__file__).resolve().parent.parent / "furniture_catalog.json"
CATALOG: dict = json.loads(CATALOG_PATH.read_text(encoding="utf-8"))
_SLOTS: dict[str, dict] = {slot["id"]: slot for slot in CATALOG["slots"]}


def _default_choice(slot: dict) -> dict:
    item = slot["items"][0]
    return {"item": item["id"], "finish": item["finishes"][0] if item["finishes"] else None}


def defaults() -> dict:
    """The room as it comes: every slot's first piece in its first finish."""
    return {slot["id"]: _default_choice(slot) for slot in CATALOG["slots"]}


def _check(slot_id: str, item_id: str, finish: str | None) -> dict:
    slot = _SLOTS.get(slot_id)
    if slot is None:
        raise invalid("unknown_slot", "That isn't a part of the room that can be furnished.")
    item = next((i for i in slot["items"] if i["id"] == item_id), None)
    if item is None:
        raise invalid("unknown_item", "That piece isn't in the catalogue.")
    if not item["finishes"]:
        if finish is not None:
            raise invalid("unknown_finish", "That piece comes in one finish only.")
        return {"item": item["id"], "finish": None}
    if finish is None:
        return {"item": item["id"], "finish": item["finishes"][0]}
    if finish not in item["finishes"]:
        raise invalid("unknown_finish", "That piece doesn't come in that finish.")
    return {"item": item["id"], "finish": finish}


def resolve(stored: dict | None) -> dict:
    """Stored choices with the defaults filled in. Anything the catalogue no longer has falls back to the default,
    so a room never points at a piece that can't be drawn."""
    out = defaults()
    for slot_id, choice in (stored or {}).items():
        if slot_id not in _SLOTS or not isinstance(choice, dict):
            continue
        try:
            out[slot_id] = _check(slot_id, str(choice.get("item")), choice.get("finish"))
        except Exception:  # noqa: BLE001  (a retired piece or finish: keep the default)
            continue
    return out


def get_decor(db: Session, actor: User, room_id: str) -> tuple[RoomDecor | None, dict]:
    require_member(db, room_id, actor.id)
    row = db.get(RoomDecor, room_id)
    return row, resolve(row.choices if row else None)


def set_decor(db: Session, actor: User, room_id: str, choices: dict, expected_version: int | None = None) -> tuple[RoomDecor, dict]:
    """Changes the given slots and leaves the others as they are."""
    require_member(db, room_id, actor.id)
    changed = {slot_id: _check(slot_id, c["item"], c.get("finish")) for slot_id, c in choices.items()}
    row = db.get(RoomDecor, room_id)
    current_version = row.version if row else 0
    if expected_version is not None and expected_version != current_version:
        # the same policy as objects on the table: the client loads the latest and applies its change again
        raise conflict("version_conflict", "Someone changed the room at the same time. The latest version has been loaded.")
    merged = {**resolve(row.choices if row else None), **changed}
    if row is None:
        row = RoomDecor(room_id=room_id, choices=merged, version=1, updated_by=actor.id)
        db.add(row)
    else:
        row.choices = merged   # a new dict, so the JSON column is written
        row.version = current_version + 1
        row.updated_by = actor.id
        row.updated_at = utcnow()
    db.commit()
    return row, merged


def serialize(room_id: str, row: RoomDecor | None, choices: dict) -> dict:
    return {
        "roomId": room_id,
        "version": row.version if row else 0,
        "choices": choices,
        "updatedBy": row.updated_by if row else None,
        "updatedAt": row.updated_at.isoformat() if row else None,
    }
