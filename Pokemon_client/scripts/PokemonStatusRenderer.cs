using System.Text;

internal sealed record TypeView(int TypeId, string Name, string ColorRgb);

internal sealed record PokemonView(
    int PokemonId, string Name, int CurrentHp, int MaxHp, TypeView[] Types);

internal sealed record MoveView(int MoveId, string Name, TypeView Type);

internal sealed record PokemonSelection(int PokemonId, int[] MoveIds);
internal sealed record MoveChoice(int Slot);
internal sealed record AttackEvent(string Attacker, string MoveName, string Message,
    int PlayerHp, int OpponentHp, bool Hit, int Damage);

internal sealed record PokemonResponse(
    PokemonView? Pokemon, string? Error, MoveView[]? Moves = null,
    PokemonView? Opponent = null, AttackEvent[]? Actions = null, string? Winner = null);

internal static class PokemonStatusRenderer
{
    public static void Draw(PokemonView pokemon, int x, int y, int? innerWidth = null)
    {
        if (pokemon.MaxHp <= 0)
            throw new ArgumentOutOfRangeException(nameof(pokemon), "전체 체력은 1 이상이어야 합니다.");

        int width = innerWidth ?? GetInnerWidth(x);
        Console.SetCursorPosition(x, y);
        Console.Write($"+{new string('=', width + 2)}+");
        Console.SetCursorPosition(x, y + 1);
        WritePokemonNameRow(pokemon, width);
        Console.SetCursorPosition(x, y + 2);
        WriteHpRow(pokemon, width);
        Console.SetCursorPosition(x, y + 3);
        Console.Write($"+{new string('=', width + 2)}+");
    }

    public static void UpdateHp(PokemonView pokemon, int x, int hpY, int? innerWidth = null)
    {
        Console.SetCursorPosition(x, hpY);
        WriteHpRow(pokemon, innerWidth ?? GetInnerWidth(x));
    }

    public static void DrawMoves(IReadOnlyList<MoveView> moves, int x, int y, int? innerWidth = null)
    {
        if (moves.Count != 4)
            throw new ArgumentException("기술은 정확히 4개여야 합니다.", nameof(moves));

        int width = innerWidth ?? GetInnerWidth(x);
        int leftWidth = width / 2;
        int rightWidth = width - leftWidth;

        Console.SetCursorPosition(x, y);
        Console.Write($"+{new string('=', width + 2)}+");
        for (int row = 0; row < 2; row++)
        {
            int leftIndex = row * 2;
            Console.SetCursorPosition(x, y + row + 1);
            Console.Write("| ");
            WriteMoveCell(moves[leftIndex], leftIndex + 1, leftWidth);
            WriteMoveCell(moves[leftIndex + 1], leftIndex + 2, rightWidth);
            Console.Write(" |");
        }
        Console.SetCursorPosition(x, y + 3);
        Console.Write($"+{new string('=', width + 2)}+");
    }

    private static int GetInnerWidth(int x)
    {
        int width = Math.Min(38, Console.BufferWidth - x - 5);
        if (width < 20)
            throw new InvalidOperationException("체력 상자를 그리려면 콘솔 너비가 더 필요합니다.");
        return width;
    }

    private static void WriteHpRow(PokemonView pokemon, int innerWidth)
    {
        int barWidth = Math.Min(16, innerWidth - 17);
        int filled = (int)Math.Round(
            (double)Math.Clamp(pokemon.CurrentHp, 0, pokemon.MaxHp) * barWidth / pokemon.MaxHp);

        Console.Write("| HP [");
        var previousColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write(new string('█', filled));
        Console.ForegroundColor = previousColor;
        Console.Write(new string(' ', barWidth - filled));
        Console.Write("] ");
        string hpText = $"{pokemon.CurrentHp}/{pokemon.MaxHp}";
        Console.Write(hpText);
        int hpContentWidth = 6 + barWidth + hpText.Length;
        Console.Write(new string(' ', Math.Max(0, innerWidth - hpContentWidth)) + " |");
    }

    private static void WritePokemonNameRow(PokemonView pokemon, int innerWidth)
    {
        if (pokemon.Types is not { Length: >= 1 and <= 2 })
            throw new InvalidDataException("포켓몬 타입 정보가 올바르지 않습니다.");

        Console.Write("| ");
        foreach (TypeView type in pokemon.Types)
        {
            TypeColorRenderer.WriteMarker(type.ColorRgb);
            Console.Write(' ');
        }
        Console.Write(FitToWidth(pokemon.Name, innerWidth - pokemon.Types.Length * 2));
        Console.Write(" |");
    }

    private static void WriteMoveCell(MoveView move, int number, int width)
    {
        Console.Write($"{number}. ");
        TypeColorRenderer.WriteMarker(move.Type.ColorRgb);
        Console.Write(' ');
        Console.Write(FitToWidth(move.Name, width - 5));
    }

    private static string FitToWidth(string text, int width)
    {
        if (DisplayWidth(text) <= width)
            return text + new string(' ', width - DisplayWidth(text));

        var result = new StringBuilder();
        int used = 0;
        foreach (Rune rune in text.EnumerateRunes())
        {
            int runeWidth = GetRuneWidth(rune);
            if (used + runeWidth > width - 1)
                break;
            result.Append(rune);
            used += runeWidth;
        }
        result.Append('…');
        return result.ToString() + new string(' ', width - used - 1);
    }

    private static int DisplayWidth(string text)
    {
        int width = 0;
        foreach (Rune rune in text.EnumerateRunes())
            width += GetRuneWidth(rune);
        return width;
    }

    private static int GetRuneWidth(Rune rune)
    {
        int codePoint = rune.Value;
        return codePoint is >= 0x1100 and <= 0x11FF
            or >= 0x2E80 and <= 0xA4CF
            or >= 0xAC00 and <= 0xD7AF
            or >= 0xF900 and <= 0xFAFF
            or >= 0xFF01 and <= 0xFF60 ? 2 : 1;
    }
}
