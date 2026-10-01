using System.Text;

internal static class BattleSceneRenderer
{
    public static void Run(PokemonView player, PokemonView opponent,
        IReadOnlyList<MoveView> moves, Func<int, PokemonResponse> playTurn)
    {
        if (Console.BufferWidth < 76 || Console.BufferHeight < 20)
            throw new InvalidOperationException("전투 화면에는 너비 76칸, 높이 20칸 이상의 터미널이 필요합니다.");
        Console.Clear();
        int rightX = Console.BufferWidth / 2 + 1;
        int innerWidth = Math.Min(34, Console.BufferWidth / 2 - 7);
        int maxRows = Console.BufferHeight - 17;
        string[] left = Load(player.PokemonId, rightX - 4, maxRows, true);
        string[] right = Load(opponent.PokemonId, Console.BufferWidth - rightX - 2, maxRows, false);
        int rows = Math.Max(left.Length, right.Length);
        int statusY = rows + 2, movesY = statusY + 5, logY = movesY + 5, promptY = logY + 2;
        PokemonStatusRenderer.Draw(player, 2, statusY, innerWidth);
        PokemonStatusRenderer.Draw(opponent, rightX, statusY, innerWidth);
        PokemonStatusRenderer.DrawMoves(moves, 2, movesY, innerWidth);

        object screenLock = new();
        using var stop = new ManualResetEventSlim(false);
        int playerBlink = 0, opponentBlink = 0, lower = 0, playerSink = 0, opponentSink = 0;
        Exception? animationError = null;
        void DrawSprites()
        {
            var frame = new StringBuilder();
            for (int row = 0; row <= rows; row++) frame.Append($"\x1b[{row + 1};1H\x1b[2K");
            AppendSprite(frame, left, 2, lower, playerSink, playerBlink == 0 || playerBlink % 2 == 0);
            AppendSprite(frame, right, rightX, lower, opponentSink, opponentBlink == 0 || opponentBlink % 2 == 0);
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
                        if (++tick % 2 == 0) lower = 1 - lower;
                        if (playerBlink > 0) playerBlink--;
                        if (opponentBlink > 0) opponentBlink--;
                        DrawSprites();
                    }
            }
            catch (Exception ex) { animationError = ex; stop.Set(); }
        }) { IsBackground = true, Name = "Pokemon battle animation" };

        Console.CursorVisible = false;
        try
        {
            lock (screenLock) DrawSprites();
            animation.Start();
            int turn = 1;
            while (true)
            {
                lock (screenLock) WriteLine(promptY, $"턴 {turn}: 사용할 기술을 고르세요 (1~4)");
                int slot;
                while (true)
                {
                    ConsoleKeyInfo key = Console.ReadKey(intercept: true);
                    if (key.KeyChar is >= '1' and <= '4') { slot = key.KeyChar - '0'; break; }
                }
                // 선택을 확정한 순간 안내를 지워 공격 연출과 종료 화면에 남지 않게 한다.
                lock (screenLock) WriteLine(promptY, "");
                PokemonResponse response = playTurn(slot);
                if (response.Error is not null)
                {
                    lock (screenLock) WriteLine(logY, response.Error);
                    continue;
                }
                foreach (AttackEvent action in response.Actions ?? [])
                {
                    lock (screenLock)
                    {
                        player = player with { CurrentHp = action.PlayerHp };
                        opponent = opponent with { CurrentHp = action.OpponentHp };
                        PokemonStatusRenderer.UpdateHp(player, 2, statusY + 2, innerWidth);
                        PokemonStatusRenderer.UpdateHp(opponent, rightX, statusY + 2, innerWidth);
                        WriteLine(logY, action.Message);
                        if (action.Damage > 0)
                        {
                            if (action.Attacker == "player") opponentBlink = 6;
                            else playerBlink = 6;
                        }
                    }
                    Thread.Sleep(600);
                }
                if (response.Winner is null) { turn++; continue; }
                while (playerBlink > 0 || opponentBlink > 0) Thread.Sleep(90);
                bool playerFainted = response.Winner == "server";
                lock (screenLock)
                    WriteLine(logY + 1, $"{(playerFainted ? player.Name : opponent.Name)}이(가) 쓰러졌습니다. " +
                        (playerFainted ? "패배했습니다." : "승리했습니다."));
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
                lock (screenLock) WriteLine(promptY, "전투 종료");
                break;
            }
        }
        finally
        {
            stop.Set();
            if (animation.IsAlive) animation.Join();
            Console.CursorVisible = true;
            Console.SetCursorPosition(0, Math.Min(Console.BufferHeight - 1, promptY + 1));
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

    private static void AppendSprite(StringBuilder frame, string[] lines, int x, int bob, int sink, bool visible)
    {
        if (!visible) return;
        int floor = bob + lines.Length - 1;
        for (int row = 0; row < lines.Length; row++)
        {
            int y = bob + sink + row;
            if (y > floor) break;
            frame.Append($"\x1b[{y + 1};{x + 1}H").Append(lines[row]).Append("\x1b[0m");
        }
    }

    private static void WriteLine(int y, string message)
    {
        Console.SetCursorPosition(0, y);
        Console.Write(new string(' ', Console.BufferWidth - 1));
        Console.SetCursorPosition(2, y);
        Console.Write(message);
    }
}
