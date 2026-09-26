using AniiModLoader.Bootstrap.Logging;
using AniiModLoader.Logging;

namespace AniiModLoader.Bootstrap;

internal class InternalLogger(ColorARGB sectionColor, string sectionName)
{
    public void Msg(string msg)
    {
        AniiModLogger.Log(ColorARGB.LightGray, msg, sectionColor, sectionName, msg);
    }

    public void Error(string msg)
    {
        AniiModLogger.LogError(msg, sectionName);
    }

    public void Warning(string msg)
    {
        AniiModLogger.LogWarning(msg, sectionName);
    }
}