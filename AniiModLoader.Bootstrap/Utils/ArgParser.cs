using AniiModLoader.Compatibility;
namespace AniiModLoader.Bootstrap.Utils;
internal static class ArgParser
{
    public static bool IsDefined(string name) => LaunchArgumentReader.Process.IsDefined(name);
    public static string? GetValue(string name) => LaunchArgumentReader.Process.GetValue(name);
}
