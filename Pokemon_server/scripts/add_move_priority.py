"""Add fifth-generation move priority to an existing Pokemon.db."""
import csv
import sqlite3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] / 'DB'
database = ROOT / 'Pokemon.db'
source = ROOT / 'source' / 'moves.csv'
with source.open(newline='', encoding='utf-8') as file:
    priorities = {int(row['id']): int(row['priority']) for row in csv.DictReader(file)
                  if 1 <= int(row['id']) <= 559}

with sqlite3.connect(database) as connection:
    columns = {row[1] for row in connection.execute('PRAGMA table_info(PokemonMove)')}
    if 'priority' not in columns:
        connection.execute('ALTER TABLE PokemonMove ADD COLUMN priority INTEGER NOT NULL DEFAULT 0')
    move_ids = {row[0] for row in connection.execute('SELECT move_id FROM PokemonMove')}
    if move_ids != set(priorities):
        raise ValueError('Database moves do not match source IDs 1-559')
    connection.executemany('UPDATE PokemonMove SET priority = ? WHERE move_id = ?',
                           ((priorities[move_id], move_id) for move_id in sorted(move_ids)))
    if connection.execute('PRAGMA integrity_check').fetchone()[0] != 'ok':
        raise ValueError('SQLite integrity check failed')
print(f'Updated priority for {len(priorities)} moves in {database}')
