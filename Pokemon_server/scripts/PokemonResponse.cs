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
