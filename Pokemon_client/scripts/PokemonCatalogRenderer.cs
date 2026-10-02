internal sealed record PokemonCatalogEntry(int PokemonId, string Name, TypeView[] Types, int[] LearnableMoveIds);
internal sealed record PokemonCatalogResponse(PokemonCatalogEntry[] Pokemon, MoveView[] Moves);

internal static class PokemonCatalogRenderer
{
    private const int NameWidth = 10;
    private const int TypeNameWidth = 6;
    public static void Draw(IReadOnlyList<PokemonCatalogEntry> pokemon)
    {
        Console.WriteLine("포켓몬 목록");
        foreach (PokemonCatalogEntry entry in pokemon.OrderBy(entry => entry.PokemonId))
        {
            Console.Write($"{entry.PokemonId,3}. {PokemonStatusRenderer.FitToWidth(entry.Name, NameWidth)}");
            foreach (TypeView type in entry.Types)
            {
                Console.Write(' ');
                TypeColorRenderer.WriteMarker(type.ColorRgb);
                Console.Write(PokemonStatusRenderer.FitToWidth(type.Name, TypeNameWidth));
            }
            Console.WriteLine();
        }
        Console.WriteLine();
    }
}
