internal static class PokemonSelectionReader
{
    public static PokemonSelection? Read(PokemonCatalogResponse catalog, int teamSlot = 1, PokemonSelection? previous = null)
    {
        var pokemonById = catalog.Pokemon.ToDictionary(entry => entry.PokemonId);
        var movesById = catalog.Moves.ToDictionary(move => move.MoveId);
        var learnableMoveIds = new HashSet<int>();
        int pokemonId = previous?.PokemonId ?? 0;
        int[] moveIds = previous is null ? new int[4] : (int[])previous.MoveIds.Clone();
        // 0: 포켓몬 선택, 1~4: 해당 번호의 기술 선택.
        int step = previous is null ? 0 : 4;
        Console.WriteLine($"팀 {teamSlot}/3: 포켓몬과 기술 4개를 선택하세요.");
        if (previous is not null)
        {
            learnableMoveIds = pokemonById[pokemonId].LearnableMoveIds.ToHashSet();
            MoveCatalogRenderer.Draw(pokemonById[pokemonId], learnableMoveIds.Order()
                .Select(id => movesById[id]).ToArray());
        }
        while (step <= moveIds.Length)
        {
            Console.Write(step == 0
                ? $"{teamSlot}번째 포켓몬 도감번호 (1~649, 이전: b, 종료: q): "
                : $"{step}번 기술 번호를 입력하세요 (위 목록의 번호, 이전: b, 종료: q): ");
            string? input = Console.ReadLine()?.Trim();
            if (input is null || input.Equals("q", StringComparison.OrdinalIgnoreCase))
                return null;
            if (input.Equals("b", StringComparison.OrdinalIgnoreCase))
            {
                if (step == 0)
                {
                    if (teamSlot > 1) return new PokemonSelection(0, []);
                    Console.WriteLine("첫 선택 단계입니다. 포켓몬 번호를 입력해 주세요.");
                    continue;
                }
                step--;
                if (step == 0)
                {
                    pokemonId = 0;
                    learnableMoveIds.Clear();
                    ClearSelectionScreen();
                    PokemonCatalogRenderer.Draw(catalog.Pokemon);
                    Console.WriteLine($"팀 {teamSlot}/3: 포켓몬과 기술 4개를 선택하세요.");
                }
                // 다시 고를 기술과 그 이후 기술은 취소하고, 앞선 선택만 유지한다.
                int clearFrom = Math.Max(0, step - 1);
                Array.Clear(moveIds, clearFrom, moveIds.Length - clearFrom);
                Console.WriteLine("이전 선택으로 돌아갑니다.");
                continue;
            }
            if (step == 0)
            {
                if (!int.TryParse(input, out int selectedPokemon) ||
                    selectedPokemon is < 1 or > 649 || !pokemonById.ContainsKey(selectedPokemon))
                {
                    Console.WriteLine("1~649 사이의 도감번호를 입력해 주세요.");
                    continue;
                }
                PokemonCatalogEntry pokemon = pokemonById[selectedPokemon];
                if (pokemon.LearnableMoveIds.Length < moveIds.Length)
                {
                    Console.WriteLine($"{pokemon.Name}의 레벨업 기술은 {pokemon.LearnableMoveIds.Length}개입니다. 현재는 서로 다른 기술 4개가 필요하므로 다른 포켓몬을 선택해 주세요.");
                    continue;
                }
                pokemonId = selectedPokemon;
                learnableMoveIds = pokemon.LearnableMoveIds.ToHashSet();
                ClearSelectionScreen();
                Console.WriteLine($"팀 {teamSlot}/3: 기술 4개를 선택하세요.");
                MoveCatalogRenderer.Draw(pokemon, pokemon.LearnableMoveIds.Order()
                    .Select(id => movesById[id]).ToArray());
            }
            else
            {
                if (!int.TryParse(input, out int moveId) || !learnableMoveIds.Contains(moveId))
                {
                    Console.WriteLine("선택한 포켓몬이 배울 수 있는 목록의 기술 번호를 입력해 주세요.");
                    continue;
                }
                if (moveIds.Take(step - 1).Contains(moveId))
                {
                    Console.WriteLine("이미 선택한 기술입니다. 다른 기술 번호를 입력해 주세요.");
                    continue;
                }
                moveIds[step - 1] = moveId;
            }
            step++;
        }
        return new PokemonSelection(pokemonId, moveIds);
    }

    public static TeamSelection? ReadTeam(PokemonCatalogResponse catalog)
    {
        var team = new PokemonSelection[3];
        int index = 0;
        bool revisit = false;
        while (index < team.Length)
        {
            ClearSelectionScreen();
            if (!revisit) PokemonCatalogRenderer.Draw(catalog.Pokemon);
            PokemonSelection? member = Read(catalog, index + 1, revisit ? team[index] : null);
            if (member is null) return null;
            if (member.PokemonId == 0)
            {
                index--;
                revisit = true;
                continue;
            }
            team[index++] = member;
            revisit = false;
        }
        return new TeamSelection(team);
    }

    private static void ClearSelectionScreen()
    {
        // 출력이 파일이나 파이프로 연결된 경우에는 콘솔 제어를 하지 않는다.
        if (!Console.IsOutputRedirected)
            Console.Clear();
    }
}
