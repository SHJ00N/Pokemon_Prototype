CREATE TABLE Type (
    type_id INTEGER PRIMARY KEY,
    name TEXT NOT NULL UNIQUE,
    color_rgb TEXT NOT NULL CHECK (
        color_rgb GLOB '#[0-9A-F][0-9A-F][0-9A-F][0-9A-F][0-9A-F][0-9A-F]'
    )
);

CREATE TABLE Pokemon (
    pokemon_id INTEGER PRIMARY KEY,
    name TEXT NOT NULL,
    type1_id INTEGER NOT NULL REFERENCES Type(type_id),
    type2_id INTEGER REFERENCES Type(type_id),
    base_hp INTEGER NOT NULL,
    base_attack INTEGER NOT NULL,
    base_defense INTEGER NOT NULL,
    base_sp_attack INTEGER NOT NULL,
    base_sp_defense INTEGER NOT NULL,
    base_speed INTEGER NOT NULL
);

CREATE TABLE PokemonMove (
    move_id INTEGER NOT NULL PRIMARY KEY,
    name TEXT NOT NULL,
    type_id INTEGER NOT NULL REFERENCES Type(type_id),
    category TEXT NOT NULL,
    power INTEGER,
    accuracy INTEGER,
    pp INTEGER NOT NULL,
    priority INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE TypeEffectiveness (
    attack_type_id INTEGER NOT NULL REFERENCES Type(type_id),
    defend_type_id INTEGER NOT NULL REFERENCES Type(type_id),
    multiplier REAL NOT NULL CHECK (multiplier IN (0.0, 0.5, 1.0, 2.0)),
    PRIMARY KEY (attack_type_id, defend_type_id)
);

CREATE TABLE PokemonLearnset (
    pokemon_id INTEGER NOT NULL REFERENCES Pokemon(pokemon_id),
    move_id INTEGER NOT NULL REFERENCES PokemonMove(move_id),
    PRIMARY KEY (pokemon_id, move_id)
);

CREATE INDEX IX_PokemonLearnset_Move ON PokemonLearnset(move_id);
