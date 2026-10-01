using System;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;


class Program
{
    static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        const string serverIp = "127.0.0.1";
        int serverPort = args.Length == 0 ? 5000 : int.Parse(args[0]);

        int pokemonId;
        while (true)
        {
            Console.Write("도감번호를 입력하세요 (1~649, 종료: q): ");
            string? input = Console.ReadLine();
            if (input is null || input.Trim().Equals("q", StringComparison.OrdinalIgnoreCase))
                return;
            if (int.TryParse(input, out pokemonId) && pokemonId is >= 1 and <= 649)
                break;
            Console.WriteLine("1~649 사이의 도감번호를 입력해 주세요.");
        }

        int[] moveIds = new int[4];
        var selectedMoveIds = new HashSet<int>();
        for (int i = 0; i < moveIds.Length;)
        {
            Console.Write($"{i + 1}번 기술 번호를 입력하세요 (1~559, 종료: q): ");
            string? input = Console.ReadLine();
            if (input is null || input.Trim().Equals("q", StringComparison.OrdinalIgnoreCase))
                return;
            if (!int.TryParse(input, out int moveId) || moveId is < 1 or > 559)
            {
                Console.WriteLine("1~559 사이의 기술 번호를 입력해 주세요.");
                continue;
            }
            if (!selectedMoveIds.Add(moveId))
            {
                Console.WriteLine("이미 선택한 기술입니다. 다른 기술 번호를 입력해 주세요.");
                continue;
            }
            moveIds[i++] = moveId;
        }

        Console.WriteLine("Connecting to server...");

        using TcpClient client = new TcpClient();

        client.Connect(
            serverIp,
            serverPort
        );

        Console.WriteLine("Connected.");

        using NetworkStream stream = client.GetStream();

        using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true)
        {
            AutoFlush = true
        };
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        writer.WriteLine(JsonSerializer.Serialize(new PokemonSelection(pokemonId, moveIds)));

        string? message = reader.ReadLine();
        PokemonResponse response = JsonSerializer.Deserialize<PokemonResponse>(message
            ?? throw new InvalidDataException("서버에서 응답을 받지 못했습니다."))
            ?? throw new InvalidDataException("서버 응답을 해석할 수 없습니다.");

        if (response.Error is not null)
        {
            Console.WriteLine(response.Error);
            return;
        }

        PokemonView pokemon = response.Pokemon
            ?? throw new InvalidDataException("서버 응답에 포켓몬 정보가 없습니다.");
        MoveView[] moves = response.Moves is { Length: 4 } selectedMoves
            ? selectedMoves
            : throw new InvalidDataException("서버 응답에 기술 4개의 정보가 없습니다.");

        Console.WriteLine(
            $"Received Pokemon ID: {pokemon.PokemonId}"
        );

        PokemonView opponent = response.Opponent
            ?? throw new InvalidDataException("서버 응답에 상대 포켓몬 정보가 없습니다.");
        BattleSceneRenderer.Run(pokemon, opponent, moves, slot => PlayTurn(reader, writer, slot));
    }

    private static PokemonResponse PlayTurn(StreamReader reader, StreamWriter writer, int slot)
    {
        writer.WriteLine(JsonSerializer.Serialize(new MoveChoice(slot)));
        string? update = reader.ReadLine();
        if (update is null)
            throw new IOException("전투 중 서버 연결이 종료되었습니다.");

        PokemonResponse response = JsonSerializer.Deserialize<PokemonResponse>(update)
            ?? throw new InvalidDataException("전투 응답을 해석할 수 없습니다.");
        return response;
    }
}
