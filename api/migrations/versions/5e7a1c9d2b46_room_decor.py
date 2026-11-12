"""room decor

Adds room_decor: how each room is furnished (which catalogue piece and finish fills each slot). One row per room,
created the first time a member changes anything; rooms without a row have the catalogue's defaults.

Revision ID: 5e7a1c9d2b46
Revises: 26be0572119a
Create Date: 2026-11-12 09:30:00.000000

"""
from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa

import samesky.models  # noqa: F401  (custom column types such as UTCDateTime)


# revision identifiers, used by Alembic.
revision: str = '5e7a1c9d2b46'
down_revision: Union[str, Sequence[str], None] = '26be0572119a'
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    """Upgrade schema."""
    op.create_table('room_decor',
    sa.Column('room_id', sa.String(length=36), nullable=False),
    sa.Column('choices', sa.JSON(), nullable=False),
    sa.Column('version', sa.Integer(), nullable=False),
    sa.Column('updated_by', sa.String(length=36), nullable=False),
    sa.Column('updated_at', samesky.models.UTCDateTime(), nullable=False),
    sa.ForeignKeyConstraint(['room_id'], ['rooms.id'], ondelete='CASCADE'),
    sa.ForeignKeyConstraint(['updated_by'], ['users.id'], ),
    sa.PrimaryKeyConstraint('room_id')
    )


def downgrade() -> None:
    """Downgrade schema."""
    op.drop_table('room_decor')
