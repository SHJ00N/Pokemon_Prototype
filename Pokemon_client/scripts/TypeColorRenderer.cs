using System.Globalization;

internal static class TypeColorRenderer
{
    public static void WriteMarker(string colorRgb)
    {
        if (colorRgb.Length != 7 || colorRgb[0] != '#' ||
            !uint.TryParse(colorRgb.AsSpan(1), NumberStyles.HexNumber,
                CultureInfo.InvariantCulture, out uint rgb))
            throw new InvalidDataException($"타입 색상값이 올바르지 않습니다: {colorRgb}");

        int red = (int)(rgb >> 16);
        int green = (int)((rgb >> 8) & 0xff);
        int blue = (int)(rgb & 0xff);
        Console.Write($"\x1b[38;2;{red};{green};{blue}m■\x1b[0m");
    }
}
