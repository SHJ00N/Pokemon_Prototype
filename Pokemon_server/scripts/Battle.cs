internal sealed class Battle
{
    private static readonly int[] PikachuMoveIds = [85, 98, 129, 231];
    private readonly GameData data;
    private readonly PokemonInfo player;
    private readonly PokemonInfo opponent;
    private readonly PokemonMoveInfo[] playerMoves;
    private readonly PokemonMoveInfo[] opponentMoves;
    private int playerHp;
    private int opponentHp;

    public Battle(GameData data, PokemonInfo player, PokemonMoveInfo[] playerMoves)
    {
        this.data = data;
        this.player = player;
        this.playerMoves = playerMoves;
        opponent = data.PokemonById[25];
        opponentMoves = PikachuMoveIds.Select(id => data.MovesById[id]).ToArray();
        playerHp = player.BaseHp;
        opponentHp = opponent.BaseHp;
    }

    public PokemonResponse Start() => Response(null);

    public PokemonResponse Play(int slot)
    {
        if (slot is < 1 or > 4)
            return Response("1~4번 기술을 선택해 주세요.");
        if (playerHp == 0 || opponentHp == 0)
            return Response("전투가 이미 종료되었습니다.");

        PokemonMoveInfo playerMove = playerMoves[slot - 1];
        PokemonMoveInfo opponentMove = opponentMoves[Random.Shared.Next(opponentMoves.Length)];
        bool playerFirst = playerMove.Priority != opponentMove.Priority
            ? playerMove.Priority > opponentMove.Priority
            : player.BaseSpeed != opponent.BaseSpeed
                ? player.BaseSpeed > opponent.BaseSpeed
                : Random.Shared.Next(2) == 0;

        var actions = new List<AttackEvent>(2);
        if (playerFirst)
        {
            actions.Add(Attack(true, playerMove));
            if (opponentHp > 0) actions.Add(Attack(false, opponentMove));
        }
        else
        {
            actions.Add(Attack(false, opponentMove));
            if (playerHp > 0) actions.Add(Attack(true, playerMove));
        }
        return Response(null, actions.ToArray());
    }

    private AttackEvent Attack(bool byPlayer, PokemonMoveInfo move)
    {
        PokemonInfo attacker = byPlayer ? player : opponent;
        PokemonInfo defender = byPlayer ? opponent : player;
        string attackerName = byPlayer ? player.Name : $"상대 {opponent.Name}";
        bool hit = move.Accuracy is null || Random.Shared.Next(100) < move.Accuracy.Value;
        int damage = 0;
        string message;
        if (!hit)
            message = $"{attackerName}의 {move.Name}! 공격이 빗나갔다.";
        else if (move.Power is null || move.Power <= 0 || move.Category == "변화")
            message = $"{attackerName}의 {move.Name}! 변화 기술 효과는 아직 구현되지 않았다.";
        else
        {
            int attack = move.Category == "특수" ? attacker.BaseSpAttack : attacker.BaseAttack;
            int defense = Math.Max(1, move.Category == "특수" ? defender.BaseSpDefense : defender.BaseDefense);
            double multiplier = data.GetMultiplier(move.TypeId, defender.Type1Id) *
                (defender.Type2Id is int second ? data.GetMultiplier(move.TypeId, second) : 1);
            double stab = attacker.Type1Id == move.TypeId || attacker.Type2Id == move.TypeId ? 1.5 : 1;
            damage = multiplier == 0 ? 0 : Math.Max(1, (int)Math.Floor(
                (2 + move.Power.Value * (double)attack / defense / 6) * stab * multiplier));
            if (byPlayer) opponentHp = Math.Max(0, opponentHp - damage);
            else playerHp = Math.Max(0, playerHp - damage);
            message = $"{attackerName}의 {move.Name}! {damage} 데미지" +
                (multiplier == 0 ? " (효과 없음)" : multiplier > 1 ? " (효과가 굉장했다)" : "");
        }
        return new AttackEvent(byPlayer ? "player" : "server", move.Name, message,
            playerHp, opponentHp, hit, damage);
    }

    private PokemonResponse Response(string? error, AttackEvent[]? actions = null)
    {
        PokemonView View(PokemonInfo pokemon, int hp)
        {
            TypeView Type(int id)
            {
                PokemonTypeInfo type = data.TypesById[id];
                return new(type.Id, type.Name, type.ColorRgb);
            }
            TypeView[] types = pokemon.Type2Id is int second
                ? [Type(pokemon.Type1Id), Type(second)] : [Type(pokemon.Type1Id)];
            return new PokemonView(pokemon.Id, pokemon.Name, hp, pokemon.BaseHp, types);
        }
        MoveView[] moves = playerMoves.Select(move =>
        {
            PokemonTypeInfo type = data.TypesById[move.TypeId];
            return new MoveView(move.Id, move.Name, new TypeView(type.Id, type.Name, type.ColorRgb));
        }).ToArray();
        return new PokemonResponse(View(player, playerHp), error, moves,
            View(opponent, opponentHp), actions,
            playerHp == 0 ? "server" : opponentHp == 0 ? "player" : null);
    }
}
