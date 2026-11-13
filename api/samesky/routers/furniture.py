"""The furniture marketplace: the catalogue, and how each room is furnished."""

from __future__ import annotations

from fastapi import APIRouter, Depends
from pydantic import BaseModel, Field
from sqlalchemy.orm import Session

from ..db import get_db
from ..deps import current_user
from ..models import User
from ..realtime import hub
from ..services import furniture

router = APIRouter(prefix="/v1")


class Choice(BaseModel):
    item: str = Field(min_length=1, max_length=40)
    finish: str | None = Field(default=None, max_length=40)


class DecorIn(BaseModel):
    choices: dict[str, Choice] = Field(max_length=32)
    expected_version: int | None = None


@router.get("/furniture/catalog")
def catalog(_user: User = Depends(current_user)) -> dict:
    return furniture.CATALOG


@router.get("/rooms/{room_id}/decor")
def get_decor(room_id: str, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    row, choices = furniture.get_decor(db, user, room_id)
    return furniture.serialize(room_id, row, choices)


@router.put("/rooms/{room_id}/decor")
def put_decor(room_id: str, body: DecorIn, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    choices = {slot: choice.model_dump() for slot, choice in body.choices.items()}
    row, merged = furniture.set_decor(db, user, room_id, choices, body.expected_version)
    hub.decor_changed(room_id, user.id)
    return furniture.serialize(room_id, row, merged)
