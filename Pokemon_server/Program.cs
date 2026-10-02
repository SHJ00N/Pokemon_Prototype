using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

class Program
{
    static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        int port = args.Length == 0 ? 5000 : int.Parse(args[0]);
        string databasePath = Path.Combine(AppContext.BaseDirectory, "DB", "Pokemon.db");
        GameData data = GameData.Load(databasePath);
        Console.WriteLine(
            $"Loaded {data.PokemonById.Count} Pokemon, {data.TypesById.Count} types, " +
            $"{data.EffectivenessByType.Count} type matchups, and {data.MovesById.Count} moves, {data.LearnableMovesByPokemonId.Values.Sum(ids => ids.Count)} learnset entries from {databasePath}");

        TcpListener server = new TcpListener(
            IPAddress.Any,
            port
        );

        server.Start();

        Console.WriteLine($"Server started. Port: {port}");
        while (true)
        {
            Console.WriteLine("Waiting for client...");
            using TcpClient client = server.AcceptTcpClient();
            // 플레이어가 기술을 고르는 동안 연결을 유지한다.
            client.ReceiveTimeout = 0;
            client.SendTimeout = 5000;

            try
            {
                HandleClient(client, data);
            }
            catch (IOException exception)
            {
                Console.WriteLine($"Client connection failed: {exception.Message}");
            }
        }
    }

    private static void HandleClient(TcpClient client, GameData data)
    {
        using NetworkStream stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true)
        {
            AutoFlush = true
        };

        writer.WriteLine(JsonSerializer.Serialize(PokemonCatalog.Create(data)));
        string? selectionMessage = reader.ReadLine();
        if (selectionMessage is null) return;

        TeamSelection? selection;
        try { selection = JsonSerializer.Deserialize<TeamSelection>(selectionMessage); }
        catch (JsonException) { selection = null; }
        void Reject(string error) => writer.WriteLine(JsonSerializer.Serialize(new PokemonResponse(null, error)));
        if (selection?.Pokemon is not { Length: 3 } team)
        {
            Reject("포켓몬 3마리를 선택해 주세요.");
            return;
        }

        var members = new List<(PokemonInfo Pokemon, PokemonMoveInfo[] Moves)>();
        foreach (PokemonSelection? member in team)
        {
            if (member is null || member.MoveIds is not { Length: 4 } ids || ids.Distinct().Count() != 4)
            {
                Reject("각 포켓몬에 서로 다른 기술 4개를 선택해 주세요.");
                return;
            }
            if (!data.PokemonById.TryGetValue(member.PokemonId, out PokemonInfo? pokemon))
            {
                Reject($"도감번호 {member.PokemonId}의 포켓몬을 찾을 수 없습니다.");
                return;
            }
            var moves = new List<PokemonMoveInfo>();
            foreach (int id in ids)
            {
                if (!data.MovesById.TryGetValue(id, out PokemonMoveInfo? move) ||
                    !data.LearnableMovesByPokemonId[pokemon.Id].Contains(id))
                {
                    Reject($"{pokemon.Name}이(가) 레벨업으로 배울 수 없는 기술 번호입니다: {id}");
                    return;
                }
                moves.Add(move);
            }
            members.Add((pokemon, moves.ToArray()));
        }
        var battle = new Battle(data, members.ToArray());
        writer.WriteLine(JsonSerializer.Serialize(battle.Start()));
        Console.WriteLine($"Battle started: {members[0].Pokemon.Name} (3 Pokemon) vs Pikachu");
        while (true)
        {
            string? request = reader.ReadLine();
            if (request is null) return;
            MoveChoice? choice;
            try { choice = JsonSerializer.Deserialize<MoveChoice>(request); }
            catch (JsonException) { choice = null; }
            PokemonResponse response = choice is null
                ? new PokemonResponse(null, "기술 선택 요청 형식이 올바르지 않습니다.")
                : battle.Play(choice);
            writer.WriteLine(JsonSerializer.Serialize(response));
            if (response.Winner is not null)
            {
                Console.WriteLine($"Battle ended. Winner: {response.Winner}");
                return;
            }
        }
    }
}
