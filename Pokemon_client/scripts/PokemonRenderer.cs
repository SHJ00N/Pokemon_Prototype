using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

public static class PokemonRenderer
{
    // ANSI Escape Sequence
    // 예: \x1b[38;2;255;255;0m
    private static readonly Regex AnsiRegex =
        new(@"\x1B\[[0-?]*[ -/]*[@-~]",
            RegexOptions.Compiled);

    /// <summary>움직이는 그림을 위해 색상 문자열을 한 번만 읽고 화면 높이에 맞춥니다.</summary>
    public static string[] LoadAnimationLines(string path, int maxRows, int maxColumns, bool flipX)
    {
        if (maxRows <= 0 || maxColumns <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxRows), "그림을 표시할 공간이 없습니다.");

        string[] source = File.ReadAllLines(path, Encoding.UTF8);
        if (source.Length == 0)
            throw new InvalidDataException($"포켓몬 그림 파일이 비어 있습니다: {path}");

        int sourceRows = source.Length;
        while (sourceRows > 1 && string.IsNullOrWhiteSpace(AnsiRegex.Replace(source[sourceRows - 1], "")))
            sourceRows--;

        int visibleRows = Math.Min(sourceRows, maxRows);
        string[] lines = new string[visibleRows];
        for (int row = 0; row < visibleRows; row++)
        {
            // 화면이 작은 경우 첫 줄과 마지막 줄을 포함해 고르게 줄인다.
            int sourceRow = visibleRows == 1 ? 0 :
                (int)Math.Round((double)row * (sourceRows - 1) / (visibleRows - 1));
            string line = flipX ? FlipAnsiLine(source[sourceRow]) : source[sourceRow];
            lines[row] = ClipAnsiLine(line, maxColumns);
        }
        return lines;
    }

    /// <summary>그림 영역만 지우고 위/아래 위치 중 하나에 다시 그립니다.</summary>
    public static void DrawAnimationFrame(string[] lines, int x, int y, bool visible = true)
    {
        var frame = new StringBuilder();
        for (int row = 0; row <= lines.Length; row++)
            frame.Append($"\x1b[{row + 1};1H\x1b[2K");
        if (visible)
        {
            for (int row = 0; row < lines.Length; row++)
                frame.Append($"\x1b[{y + row + 1};{x + 1}H").Append(lines[row]).Append("\x1b[0m");
        }
        Console.Write(frame.ToString());
    }

    /// <summary>그림 전체를 아래로 옮기고 원래 바닥 아래로 간 부분은 그리지 않습니다.</summary>
    public static void DrawSinkingFrame(string[] lines, int x, int startY, int offset)
    {
        int floorY = startY + lines.Length - 1;
        var frame = new StringBuilder();
        for (int row = 0; row <= lines.Length; row++)
            frame.Append($"\x1b[{row + 1};1H\x1b[2K");

        for (int row = 0; row < lines.Length; row++)
        {
            int drawY = startY + offset + row;
            if (drawY > floorY)
                break;
            frame.Append($"\x1b[{drawY + 1};{x + 1}H").Append(lines[row]).Append("\x1b[0m");
        }

        Console.Write(frame.ToString());
    }

    private static string ClipAnsiLine(string line, int maxColumns)
    {
        List<AnsiCell> cells = ParseAnsiCells(line);
        if (cells.Count <= maxColumns)
            return line;

        var clipped = new StringBuilder();
        for (int i = 0; i < maxColumns; i++)
            clipped.Append(cells[i].Ansi).Append(cells[i].Character);
        return clipped.Append("\x1b[0m").ToString();
    }

    /// <summary>
    /// Pokemon colorscript를 터미널에 출력합니다.
    /// </summary>
    public static void Draw(
        string path,
        int x,
        int y,
        bool flipX = false)
    {
        if (!File.Exists(path))
        {
            Console.SetCursorPosition(0, 0);
            Console.WriteLine($"Pokemon 파일을 찾을 수 없습니다.");
            Console.WriteLine(Path.GetFullPath(path));
            return;
        }

        Console.OutputEncoding = Encoding.UTF8;

        string[] lines = File.ReadAllLines(path, Encoding.UTF8);

        for (int i = 0; i < lines.Length; i++)
        {
            int drawY = y + i;

            if (drawY < 0)
                continue;

            // 그림이 화면보다 크면 한 줄씩 스크롤해 아래쪽까지 그린다.
            if (drawY >= Console.BufferHeight)
            {
                Console.SetCursorPosition(0, Console.BufferHeight - 1);
                Console.WriteLine();
                drawY = Console.BufferHeight - 1;
            }

            Console.SetCursorPosition(x, drawY);

            string line = lines[i];

            if (flipX)
                line = FlipAnsiLine(line);

            Console.Write(line);

            // 다음 출력에 색상이 넘어가지 않도록 초기화
            Console.Write("\x1b[0m");
        }
    }

    /// <summary>
    /// ANSI 색상 정보를 유지하면서 한 줄을 좌우 반전합니다.
    /// </summary>
    private static string FlipAnsiLine(string line)
    {
        List<AnsiCell> cells = ParseAnsiCells(line);

        cells.Reverse();

        StringBuilder result = new();

        foreach (AnsiCell cell in cells)
        {
            result.Append(cell.Ansi);
            result.Append(MirrorCharacter(cell.Character));
        }

        result.Append("\x1b[0m");

        return result.ToString();
    }

    /// <summary>
    /// ANSI 문자열을 화면에 표시되는 문자 단위로 분리합니다.
    /// </summary>
    private static List<AnsiCell> ParseAnsiCells(string line)
    {
        List<AnsiCell> cells = new();

        string currentAnsi = "";
        int index = 0;

        while (index < line.Length)
        {
            // ANSI Escape 시작
            if (line[index] == '\x1b')
            {
                Match match = AnsiRegex.Match(line, index);

                if (match.Success && match.Index == index)
                {
                    currentAnsi = match.Value;
                    index += match.Length;
                    continue;
                }
            }

            char c = line[index];

            cells.Add(new AnsiCell
            {
                Ansi = currentAnsi,
                Character = c
            });

            index++;
        }

        return cells;
    }

    /// <summary>
    /// 좌우 반전 시 방향성이 있는 Unicode 문자를 교체합니다.
    /// </summary>
    private static char MirrorCharacter(char c)
    {
        return c switch
        {
            '▌' => '▐',
            '▐' => '▌',

            '▖' => '▗',
            '▗' => '▖',

            '▘' => '▝',
            '▝' => '▘',

            '◢' => '◣',
            '◣' => '◢',

            '◤' => '◥',
            '◥' => '◤',

            '<' => '>',
            '>' => '<',

            '(' => ')',
            ')' => '(',

            '[' => ']',
            ']' => '[',

            '{' => '}',
            '}' => '{',

            _ => c
        };
    }

    private class AnsiCell
    {
        public string Ansi { get; set; } = "";
        public char Character { get; set; }
    }
}
