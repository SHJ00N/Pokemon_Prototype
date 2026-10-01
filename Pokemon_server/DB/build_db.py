"""Build Pokemon.db offline from the checked-in PokeAPI CSV snapshot (Python 3)."""

import csv
import sqlite3
from pathlib import Path

ROOT = Path(__file__).resolve().parent
STAT_NAMES = ('hp', 'attack', 'defense', 'special-attack', 'special-defense', 'speed')
MOVE_CATEGORIES = {'status': '변화', 'physical': '물리', 'special': '특수'}
MAX_POKEMON_ID = 649
MAX_MOVE_ID = 559
TYPE_IDS = range(1, 18)
TYPE_COLORS = {
    1: '#A8A77A', 2: '#C22E28', 3: '#A98FF3', 4: '#A33EA1',
    5: '#E2BF65', 6: '#B6A136', 7: '#A6B91A', 8: '#735797',
    9: '#B7B7CE', 10: '#EE8130', 11: '#6390F0', 12: '#7AC74C',
    13: '#F7D02C', 14: '#F95587', 15: '#96D9D6',
    16: '#6F35FC', 17: '#705746',
}


def read_csv(name):
    with (ROOT / 'source' / (name + '.csv')).open(encoding='utf-8', newline='') as file:
        return list(csv.DictReader(file))


def load_type_records(language):
    names = {int(row['type_id']): row['name']
             for row in read_csv('type_names') if row['local_language_id'] == language}
    records = [(type_id, names[type_id], TYPE_COLORS[type_id]) for type_id in TYPE_IDS]
    if len({name for _, name, _ in records}) != len(records):
        raise ValueError('Type names must be unique')
    return records


def load_effectiveness_records():
    factors = {}
    for row in read_csv('type_efficacy'):
        attack_id = int(row['damage_type_id'])
        defend_id = int(row['target_type_id'])
        if attack_id not in TYPE_IDS or defend_id not in TYPE_IDS:
            continue
        factors[(attack_id, defend_id)] = int(row['damage_factor'])
    # PokéAPI의 과거 상성 기록 중 5세대까지 적용되던 값을 덮어쓴다.
    for row in read_csv('type_efficacy_past'):
        if row['generation_id'] == '5':
            pair = (int(row['damage_type_id']), int(row['target_type_id']))
            if pair[0] in TYPE_IDS and pair[1] in TYPE_IDS:
                factors[pair] = int(row['damage_factor'])
    records = []
    for (attack_id, defend_id), factor in sorted(factors.items()):
        if factor not in (0, 50, 100, 200):
            raise ValueError(f'Invalid type effectiveness: {attack_id}, {defend_id}')
        records.append((attack_id, defend_id, factor / 100))
    expected_pairs = [(attack_id, defend_id)
                      for attack_id in TYPE_IDS for defend_id in TYPE_IDS]
    if [(attack_id, defend_id) for attack_id, defend_id, _ in records] != expected_pairs:
        raise ValueError('Expected one effectiveness value for every type pair')
    return records


def load_move_records(language):
    names = {int(row['move_id']): row['name']
             for row in read_csv('move_names') if row['local_language_id'] == language}
    classes = {int(row['id']): MOVE_CATEGORIES[row['identifier']]
               for row in read_csv('move_damage_classes')}
    x_y_group = next(row['id'] for row in read_csv('version_groups')
                     if row['identifier'] == 'x-y' and row['generation_id'] == '6')
    pre_fairy_types = {int(row['move_id']): int(row['type_id'])
                       for row in read_csv('move_changelog')
                       if row['changed_in_version_group_id'] == x_y_group and row['type_id']}
    records = []
    for move in read_csv('moves'):
        move_id = int(move['id'])
        if not 1 <= move_id <= MAX_MOVE_ID:
            continue
        type_id = int(move['type_id'])
        if type_id == 18:
            type_id = pre_fairy_types[move_id]
        record = (move_id, names[move_id], type_id,
                  classes[int(move['damage_class_id'])],
                  int(move['power']) if move['power'] else None,
                  int(move['accuracy']) if move['accuracy'] else None,
                  int(move['pp']), int(move['priority']))
        if not record[1] or record[2] not in TYPE_IDS or not record[3] or record[6] <= 0:
            raise ValueError(f'Invalid move data: {record}')
        records.append(record)
    records.sort()
    if [row[0] for row in records] != list(range(1, MAX_MOVE_ID + 1)):
        raise ValueError(f'Expected exactly one move for every ID 1-{MAX_MOVE_ID}')
    return records


def main():
    language = next(row['id'] for row in read_csv('languages') if row['identifier'] == 'ko')
    type_records = load_type_records(language)
    effectiveness_records = load_effectiveness_records()
    names = {int(row['pokemon_species_id']): row['name']
             for row in read_csv('pokemon_species_names') if row['local_language_id'] == language}
    stat_names = {int(row['id']): row['identifier'] for row in read_csv('stats')}
    types = {}
    for row in read_csv('pokemon_types'):
        types.setdefault(int(row['pokemon_id']), {})[int(row['slot'])] = int(row['type_id'])
    past_types = {}
    for row in read_csv('pokemon_types_past'):
        if row['generation_id'] == '5':
            past_types.setdefault(int(row['pokemon_id']), {})[int(row['slot'])] = int(row['type_id'])
    stats = {}
    for row in read_csv('pokemon_stats'):
        stats.setdefault(int(row['pokemon_id']), {})[stat_names[int(row['stat_id'])]] = int(row['base_stat'])

    records = []
    for pokemon in read_csv('pokemon'):
        species_id = int(pokemon['species_id'])
        if pokemon['is_default'] != '1' or not 1 <= species_id <= MAX_POKEMON_ID:
            continue
        form_id = int(pokemon['id'])
        form_types = past_types.get(form_id, types[form_id])
        if set(form_types) not in ({1}, {1, 2}):
            raise ValueError(f'Invalid type slots: {species_id}')
        record = (species_id, names[species_id], form_types[1], form_types.get(2),
                  *(stats[form_id][name] for name in STAT_NAMES))
        if not record[1] or record[2] not in TYPE_IDS or (
                record[3] is not None and record[3] not in TYPE_IDS
        ) or not all(1 <= stat <= 255 for stat in record[4:]):
            raise ValueError(f'Invalid data: {record}')
        records.append(record)
    records.sort()
    if [row[0] for row in records] != list(range(1, MAX_POKEMON_ID + 1)):
        raise ValueError(f'Expected exactly one default form for every National Pokedex ID 1-{MAX_POKEMON_ID}')

    move_records = load_move_records(language)

    output = ROOT / 'Pokemon.db'
    if output.exists():
        raise FileExistsError(f'{output} already exists; move it aside before rebuilding.')
    # Exclusive creation prevents replacing an existing database.
    with output.open('xb'):
        pass
    connection = sqlite3.connect(output)
    try:
        connection.execute('PRAGMA foreign_keys = ON')
        with connection:
            connection.executescript((ROOT / 'schema.sql').read_text(encoding='utf-8'))
            connection.executemany('INSERT INTO Type VALUES (?, ?, ?)', type_records)
            connection.executemany('INSERT INTO Pokemon VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)', records)
            connection.executemany('INSERT INTO PokemonMove VALUES (?, ?, ?, ?, ?, ?, ?, ?)', move_records)
            connection.executemany('INSERT INTO TypeEffectiveness VALUES (?, ?, ?)', effectiveness_records)
        if connection.execute('PRAGMA integrity_check').fetchall() != [('ok',)]:
            raise ValueError('SQLite integrity check failed')
        if connection.execute('SELECT * FROM Pokemon ORDER BY pokemon_id').fetchall() != records:
            raise ValueError('Database does not match source data')
        if connection.execute('SELECT * FROM PokemonMove ORDER BY move_id').fetchall() != move_records:
            raise ValueError('Move data does not match source data')
        if connection.execute('SELECT * FROM Type ORDER BY type_id').fetchall() != type_records:
            raise ValueError('Type data does not match source data')
        if connection.execute('SELECT * FROM TypeEffectiveness ORDER BY attack_type_id, defend_type_id').fetchall() != effectiveness_records:
            raise ValueError('Type effectiveness does not match source data')
        if connection.execute('PRAGMA foreign_key_check').fetchall():
            raise ValueError('Foreign key check failed')
    finally:
        connection.close()
    print(f'Created {output}: {len(records)} Pokemon, {len(move_records)} moves, '
          f'{len(type_records)} types, {len(effectiveness_records)} matchups; integrity_check=ok')


if __name__ == '__main__':
    main()
