using System.Drawing;
using AniiModLoader.Logging;
using Pastel;

namespace AniiModLoader.Bootstrap.Logging;

internal static class PastelExtensions
{
    internal static string Pastel(this string input, ColorARGB color)
    {
        return input.AsSpan().Pastel(color);
    }

    internal static string Pastel(in this ReadOnlySpan<char> input, ColorARGB color)
    {
        return input.Pastel(Color.FromArgb(color.R, color.G, color.B));
    }
}
