internal static class MoveCatalogRenderer
{
    public static void Draw(PokemonCatalogEntry pokemon, IReadOnlyList<MoveView> moves)
    {
        Console.WriteLine($"{pokemon.Name}이(가) 레벨업으로 배울 수 있는 기술");
        Console.WriteLine("번호 타입    기술이름 위력 명중률");
        foreach (MoveView move in moves)
        {
            Console.Write($"{move.MoveId,3}. ");
            TypeColorRenderer.WriteMarker(move.Type.ColorRgb);
            Console.Write(PokemonStatusRenderer.FitToWidth(move.Type.Name, 6));
            string power = move.Power?.ToString() ?? "-";
            string accuracy = move.Accuracy?.ToString() ?? "-";
            Console.WriteLine($" {PokemonStatusRenderer.FitToWidth(move.Name, 8)} {power,3} {accuracy,3}");
        }
        Console.WriteLine("위력 또는 명중률이 없는 기술은 -로 표시합니다.");
    }
}
