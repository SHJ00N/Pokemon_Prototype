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
        PokemonCatalogResponse catalog = JsonSerializer.Deserialize<PokemonCatalogResponse>(
            reader.ReadLine() ?? throw new IOException("서버에서 포켓몬 목록을 받지 못했습니다."))
            ?? throw new InvalidDataException("포켓몬 목록 응답을 해석할 수 없습니다.");
        TeamSelection? selection = PokemonSelectionReader.ReadTeam(catalog);
        if (selection is null) return;
        writer.WriteLine(JsonSerializer.Serialize(selection));

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
        BattleSceneRenderer.Run(response, choice => PlayTurn(reader, writer, choice));
    }

    private static PokemonResponse PlayTurn(StreamReader reader, StreamWriter writer, MoveChoice choice)
    {
        writer.WriteLine(JsonSerializer.Serialize(choice));
        string? update = reader.ReadLine();
        if (update is null)
            throw new IOException("전투 중 서버 연결이 종료되었습니다.");

        PokemonResponse response = JsonSerializer.Deserialize<PokemonResponse>(update)
            ?? throw new InvalidDataException("전투 응답을 해석할 수 없습니다.");
        return response;
    }
}
