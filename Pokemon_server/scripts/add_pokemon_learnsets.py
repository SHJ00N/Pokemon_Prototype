"""Store only BW2 level-up moves; safely simplify an existing learnset DB."""

import csv
import sqlite3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] / 'DB'


def load_learnset_records(root=ROOT):
    with (root / 'source/pokemon_levelup_moves_black2_white2.csv').open(encoding='utf-8', newline='') as file:
        records = sorted((int(r['pokemon_id']), int(r['move_id']))
                         for r in csv.DictReader(file))
    if (len(records) != len(set(records)) or
            {r[0] for r in records} != set(range(1, 650)) or
            any(not 1 <= move <= 559 for _, move in records)):
        raise ValueError('Invalid BW2 level-up source data')
    return records


def insert_learnsets(connection, records):
    pokemon_ids = {r[0] for r in connection.execute('SELECT pokemon_id FROM Pokemon')}
    move_ids = {r[0] for r in connection.execute('SELECT move_id FROM PokemonMove')}
    if {r[0] for r in records} != pokemon_ids or not {r[1] for r in records} <= move_ids:
        raise ValueError('Level-up moves do not match the Pokemon and moves in the database')
    existing = connection.execute('SELECT * FROM PokemonLearnset ORDER BY pokemon_id, move_id').fetchall()
    if existing and existing != records:
        raise ValueError('Existing level-up records differ; refusing to overwrite them')
    if not existing:
        connection.executemany('INSERT INTO PokemonLearnset VALUES (?, ?)', records)
    if connection.execute('SELECT * FROM PokemonLearnset ORDER BY pokemon_id, move_id').fetchall() != records:
        raise ValueError('Stored level-up moves do not match the source')
    if connection.execute('PRAGMA foreign_key_check').fetchall():
        raise ValueError('Foreign key check failed')


def migrate(database=ROOT / 'Pokemon.db'):
    if not database.is_file():
        raise FileNotFoundError(database)
    records = load_learnset_records()
    schema = (ROOT / 'schema.sql').read_text(encoding='utf-8')
    schema = schema[schema.index('CREATE TABLE PokemonLearnset'):]
    connection = sqlite3.connect(database)
    try:
        connection.execute('PRAGMA foreign_keys = ON')
        connection.execute('BEGIN IMMEDIATE')
        columns = [r[1] for r in connection.execute('PRAGMA table_info(PokemonLearnset)')]
        if columns == ['pokemon_id', 'move_id', 'level']:
            old_pairs = connection.execute('SELECT DISTINCT pokemon_id, move_id FROM PokemonLearnset ORDER BY pokemon_id, move_id').fetchall()
            if old_pairs != records:
                raise ValueError('Existing learnable move pairs differ from source; refusing migration')
            connection.execute('DROP TABLE PokemonLearnset')
        elif columns and columns != ['pokemon_id', 'move_id']:
            raise ValueError('Unexpected learnset schema; refusing migration')
        statement = ''
        for line in schema.splitlines(keepends=True):
            statement += line
            if sqlite3.complete_statement(statement):
                connection.execute(statement.replace('CREATE TABLE ', 'CREATE TABLE IF NOT EXISTS ', 1)
                                   .replace('CREATE INDEX ', 'CREATE INDEX IF NOT EXISTS ', 1))
                statement = ''
        if statement.strip():
            raise ValueError('Incomplete learnset schema')
        insert_learnsets(connection, records)
        if connection.execute('PRAGMA integrity_check').fetchall() != [('ok',)]:
            raise ValueError('SQLite integrity check failed')
        connection.commit()
    except BaseException:
        connection.rollback()
        raise
    finally:
        connection.close()
    print(f'Stored {len(records)} BW2 level-up moves in {database}')


if __name__ == '__main__':
    migrate()
