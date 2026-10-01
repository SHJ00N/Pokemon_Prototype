using Microsoft.Data.Sqlite;

internal sealed record PokemonInfo(
    int Id,
    string Name,
    int Type1Id,
    int? Type2Id,
    int BaseHp,
    int BaseAttack,
    int BaseDefense,
    int BaseSpAttack,
    int BaseSpDefense,
    int BaseSpeed);

internal static class PokemonData
{
    public static IReadOnlyDictionary<int, PokemonInfo> Load(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT pokemon_id, name, type1_id, type2_id,
                   base_hp, base_attack, base_defense,
                   base_sp_attack, base_sp_defense, base_speed
            FROM Pokemon
            ORDER BY pokemon_id
            """;

        var pokemonById = new Dictionary<int, PokemonInfo>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var pokemon = new PokemonInfo(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.IsDBNull(3) ? null : reader.GetInt32(3),
                reader.GetInt32(4),
                reader.GetInt32(5),
                reader.GetInt32(6),
                reader.GetInt32(7),
                reader.GetInt32(8),
                reader.GetInt32(9));

            if (!pokemonById.TryAdd(pokemon.Id, pokemon))
                throw new InvalidDataException($"중복된 포켓몬 도감번호: {pokemon.Id}");
        }

        if (pokemonById.Count == 0)
            throw new InvalidDataException("포켓몬 DB가 비어 있습니다.");

        return pokemonById;
    }
}
