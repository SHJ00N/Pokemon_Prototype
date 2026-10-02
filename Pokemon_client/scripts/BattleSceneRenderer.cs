using System.Text;
using System.Text.RegularExpressions;

internal static class BattleSceneRenderer
{
    private const int SelectionDelayMs = 1000;
    private const int AttackIntervalMs = 1500;
    private const int AttackStepMs = 60;
    private const int AttackDistance = 6;
    private static readonly int[] AttackOffsets = [2, AttackDistance, 0]; // 2칸 전진 → 추가 4칸 전진 → 제자리
    private static readonly Regex AnsiCode = new(@"\x1B\[[0-?]*[ -/]*[@-~]", RegexOptions.Compiled);

    public static void Run(PokemonResponse initial, Func<MoveChoice, PokemonResponse> playTurn)
    {
        PokemonView player = initial.Pokemon!;
        PokemonView opponent = initial.Opponent!;
        IReadOnlyList<MoveView> moves = initial.Moves!;
        PokemonView[] team = initial.Team ?? throw new InvalidDataException("팀 정보가 없습니다.");
        int activeSlot = initial.ActiveSlot;
        bool requiresSwitch = initial.RequiresSwitch;
        if (Console.BufferWidth < 76 || Console.BufferHeight < 20)
            throw new InvalidOperationException("전투 화면에는 너비 76칸, 높이 20칸 이상의 터미널이 필요합니다.");
        Console.Clear();
        int rightX = Console.BufferWidth / 2 + 1;
        int innerWidth = Math.Min(34, Console.BufferWidth / 2 - 7);
        int maxRows = Console.BufferHeight - 17;
        var teamSprites = team.Select(member => Load(member.PokemonId,
            rightX - 2 - AttackDistance, maxRows, true)).ToArray();
        string[] left = teamSprites[activeSlot - 1];
        string[] right = Load(opponent.PokemonId, Console.BufferWidth - rightX - 2, maxRows, false);
        int[] leftWidths = left.Select(line => AnsiCode.Replace(line, "").Length).ToArray();
        int rows = Math.Max(teamSprites.Max(sprite => sprite.Length), right.Length);
        int statusY = rows + 2, movesY = statusY + 5, logY = movesY + 5, promptY = logY + 2;
        PokemonStatusRenderer.Draw(player, 2, statusY, innerWidth);
        PokemonStatusRenderer.Draw(opponent, rightX, statusY, innerWidth);
        PokemonStatusRenderer.DrawMoves(moves, 2, movesY, innerWidth);

        object screenLock = new();
        using var stop = new ManualResetEventSlim(false);
        int playerBlink = 0, opponentBlink = 0, lower = 0, playerSink = 0, opponentSink = 0;
        int playerAdvance = 0, opponentAdvance = 0;
        Exception? animationError = null;
        void DrawSprites()
        {
            var frame = new StringBuilder();
            int leftX = 2 + playerAdvance;
            int opponentX = rightX - opponentAdvance;
            for (int row = 0; row <= rows; row++)
            {
                frame.Append($"\x1b[{row + 1};1H").Append(' ', leftX);
                int leftLine = SpriteLine(row, left.Length, lower, playerSink,
                    playerBlink == 0 || playerBlink % 2 == 0);
                if (leftLine >= 0)
                    frame.Append(left[leftLine]).Append("\x1b[0m");
                int leftWidth = leftLine >= 0 ? leftWidths[leftLine] : 0;
                frame.Append(' ', opponentX - leftX - leftWidth);
                int rightLine = SpriteLine(row, right.Length, lower, opponentSink,
                    opponentBlink == 0 || opponentBlink % 2 == 0);
                if (rightLine >= 0)
                    frame.Append(right[rightLine]);
                frame.Append("\x1b[0m\x1b[0K");
            }
            Console.Write(frame.ToString());
        }
        Thread animation = new(() =>
        {
            int tick = 0;
            try
            {
                while (!stop.Wait(90))
                    lock (screenLock)
                    {
                        bool changed = false;
                        if (++tick % 2 == 0) { lower = 1 - lower; changed = true; }
                        if (playerBlink > 0) { playerBlink--; changed = true; }
                        if (opponentBlink > 0) { opponentBlink--; changed = true; }
                        if (changed) DrawSprites();
                    }
            }
            catch (Exception ex) { animationError = ex; stop.Set(); }
        }) { IsBackground = true, Name = "Pokemon battle animation" };

        string PartyText() => string.Join(" / ", team.Select((member, index) =>
            $"{index + 1}.{PokemonStatusRenderer.FitToWidth(member.Name, 8).TrimEnd()} " +
            (member.CurrentHp == 0 ? "기절" : $"{member.CurrentHp}/{member.MaxHp}") +
            (index + 1 == activeSlot ? "*" : "")));

        Console.CursorVisible = false;
        try
        {
            lock (screenLock) DrawSprites();
            animation.Start();
            int turn = 1;
            while (true)
            {
                bool forcedSwitch = requiresSwitch;
                lock (screenLock)
                {
                    WriteLine(promptY, forcedSwitch
                        ? "쓰러졌습니다. 교체할 포켓몬을 고르세요 (1~3)"
                        : $"턴 {turn}: 기술 1~4 / 교체 S");
                    WriteLine(promptY + 1, forcedSwitch ? PartyText() : "");
                }
                MoveChoice choice;
                bool choosingSwitch = forcedSwitch;
                while (true)
                {
                    ConsoleKeyInfo key = Console.ReadKey(intercept: true);
                    if (!choosingSwitch && key.Key == ConsoleKey.S)
                    {
                        choosingSwitch = true;
                        lock (screenLock)
                        {
                            WriteLine(promptY, "교체할 포켓몬 1~3 (취소: Esc)");
                            WriteLine(promptY + 1, PartyText());
                        }
                        continue;
                    }
                    if (choosingSwitch && !forcedSwitch && key.Key == ConsoleKey.Escape)
                    {
                        choosingSwitch = false;
                        lock (screenLock)
                        {
                            WriteLine(promptY, $"턴 {turn}: 기술 1~4 / 교체 S");
                            WriteLine(promptY + 1, "");
                        }
                        continue;
                    }
                    if (choosingSwitch && key.KeyChar is >= '1' and <= '3')
                    {
                        int target = key.KeyChar - '0';
                        if (target == activeSlot || team[target - 1].CurrentHp == 0)
                        {
                            lock (screenLock) WriteLine(logY, "현재 포켓몬 또는 쓰러진 포켓몬은 선택할 수 없습니다.");
                            continue;
                        }
                        choice = new MoveChoice(SwitchSlot: target);
                        break;
                    }
                    if (!choosingSwitch && key.KeyChar is >= '1' and <= '4')
                    {
                        choice = new MoveChoice(key.KeyChar - '0');
                        break;
                    }
                }
                // 선택을 확정한 순간 안내를 지워 공격 연출과 종료 화면에 남지 않게 한다.
                lock (screenLock) { WriteLine(promptY, ""); WriteLine(promptY + 1, ""); }
                // 애니메이션 스레드는 계속 움직이고, 전투 요청만 잠시 기다린다.
                Thread.Sleep(SelectionDelayMs);
                PokemonResponse response = playTurn(choice);
                if (response.Error is not null)
                {
                    lock (screenLock) WriteLine(logY, response.Error);
                    continue;
                }
                foreach (AttackEvent action in response.Actions ?? [])
                {
                    if (action.IsSwitch)
                    {
                        lock (screenLock)
                        {
                            activeSlot = response.ActiveSlot;
                            player = response.Pokemon! with { CurrentHp = action.PlayerHp };
                            moves = response.Moves!;
                            left = teamSprites[activeSlot - 1];
                            leftWidths = left.Select(line => AnsiCode.Replace(line, "").Length).ToArray();
                            playerSink = playerBlink = playerAdvance = 0;
                            PokemonStatusRenderer.Draw(player, 2, statusY, innerWidth);
                            PokemonStatusRenderer.DrawMoves(moves, 2, movesY, innerWidth);
                            WriteLine(logY, action.Message);
                            WriteLine(logY + 1, "");
                            DrawSprites();
                        }
                        Thread.Sleep(AttackIntervalMs);
                        continue;
                    }
                    bool playerAttacking = action.Attacker == "player";
                    string attackerName = playerAttacking ? player.Name : $"상대 {opponent.Name}";
                    lock (screenLock) WriteLine(logY, $"{attackerName}의 {action.MoveName}!");
                    for (int step = 0; step < AttackOffsets.Length; step++)
                    {
                        lock (screenLock)
                        {
                            if (playerAttacking) playerAdvance = AttackOffsets[step];
                            else opponentAdvance = AttackOffsets[step];
                            // 상대에게 가장 가까워진 순간 공격 결과와 피격 연출을 표시한다.
                            if (step == 1)
                            {
                                player = player with { CurrentHp = action.PlayerHp };
                                opponent = opponent with { CurrentHp = action.OpponentHp };
                                PokemonStatusRenderer.UpdateHp(player, 2, statusY + 2, innerWidth);
                                PokemonStatusRenderer.UpdateHp(opponent, rightX, statusY + 2, innerWidth);
                                WriteLine(logY, action.Message);
                                if (action.Damage > 0)
                                {
                                    if (playerAttacking) opponentBlink = 6;
                                    else playerBlink = 6;
                                }
                            }
                            DrawSprites();
                        }
                        Thread.Sleep(AttackStepMs);
                    }
                    Thread.Sleep(AttackIntervalMs);
                }
                team = response.Team!;
                requiresSwitch = response.RequiresSwitch;
                if (response.Winner is null && !requiresSwitch)
                {
                    if (!forcedSwitch) turn++;
                    continue;
                }
                while (true)
                {
                    lock (screenLock)
                        if (playerBlink == 0 && opponentBlink == 0) break;
                    if (stop.IsSet) throw new InvalidOperationException("애니메이션이 중단되었습니다.", animationError);
                    Thread.Sleep(90);
                }
                bool playerFainted = player.CurrentHp == 0;
                lock (screenLock)
                    WriteLine(logY + 1, $"{(playerFainted ? player.Name : opponent.Name)}이(가) 쓰러졌습니다. " +
                        (response.Winner is null ? "다른 포켓몬으로 교체하세요." :
                            playerFainted ? "패배했습니다." : "승리했습니다."));
                int sinkLimit = playerFainted ? left.Length : right.Length;
                for (int offset = 3; offset < sinkLimit + 3; offset += 3)
                {
                    lock (screenLock)
                    {
                        if (playerFainted) playerSink = Math.Min(offset, sinkLimit);
                        else opponentSink = Math.Min(offset, sinkLimit);
                        DrawSprites();
                    }
                    Thread.Sleep(80);
                }
                if (response.Winner is null)
                {
                    turn++;
                    continue;
                }
                lock (screenLock) { WriteLine(promptY, "전투 종료"); WriteLine(promptY + 1, ""); }
                break;
            }
        }
        finally
        {
            stop.Set();
            if (animation.IsAlive) animation.Join();
            Console.CursorVisible = true;
            Console.SetCursorPosition(0, Math.Min(Console.BufferHeight - 1, promptY + 2));
        }
        if (animationError is not null)
            throw new InvalidOperationException("포켓몬 애니메이션 중 오류가 발생했습니다.", animationError);
    }

    private static string[] Load(int id, int columns, int rows, bool flip)
    {
        string file = $"{id:D4}.txt";
        string[] paths =
        [
            Path.Combine("resources", "pokemon_scripts", file),
            Path.Combine("Pokemon_client", "resources", "pokemon_scripts", file),
            Path.Combine(AppContext.BaseDirectory, "resources", "pokemon_scripts", file)
        ];
        string path = paths.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException($"포켓몬 그림 파일을 찾을 수 없습니다: {file}");
        return PokemonRenderer.LoadAnimationLines(path, rows, columns, flip);
    }

    private static int SpriteLine(int row, int length, int bob, int sink, bool visible)
    {
        int line = row - bob - sink;
        return visible && line >= 0 && line < length && row <= bob + length - 1
            ? line : -1;
    }

    private static void WriteLine(int y, string message)
    {
        Console.SetCursorPosition(0, y);
        Console.Write(new string(' ', Console.BufferWidth - 1));
        Console.SetCursorPosition(2, y);
        Console.Write(message);
    }
}
