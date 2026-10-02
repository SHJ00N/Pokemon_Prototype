internal sealed record PokemonCatalogEntry(int PokemonId, string Name, TypeView[] Types, int[] LearnableMoveIds);
internal sealed record PokemonCatalogResponse(PokemonCatalogEntry[] Pokemon, MoveView[] Moves);

internal static class PokemonCatalog
{
    public static PokemonCatalogResponse Create(GameData data)
    {
        TypeView Type(int id)
        {
            PokemonTypeInfo type = data.TypesById[id];
            return new TypeView(type.Id, type.Name, type.ColorRgb);
        }
        PokemonCatalogEntry[] pokemon = data.PokemonById.Values
            .OrderBy(entry => entry.Id)
            .Select(entry => new PokemonCatalogEntry(entry.Id, entry.Name,
                entry.Type2Id is int second
                    ? [Type(entry.Type1Id), Type(second)]
                    : [Type(entry.Type1Id)],
                data.LearnableMovesByPokemonId[entry.Id].Order().ToArray()))
            .ToArray();
        MoveView[] moves = data.MovesById.Values.OrderBy(move => move.Id)
            .Select(move => new MoveView(move.Id, move.Name, Type(move.TypeId), move.Power, move.Accuracy))
            .ToArray();
        return new PokemonCatalogResponse(pokemon, moves);
    }
}
