using System;
using AniiModLoader.Logging;
using AniiModLoader.Utils;

namespace AniiModLoader
{
    public static class AniiModDebug
    {
        public static void Msg(object obj)
        {
            if (!IsEnabled())
                return;
            AniiModLogger.PassLogMsg(AniiModLogger.DefaultTextColor, obj.ToString(), ColorARGB.CornflowerBlue, "DEBUG");
            MsgCallbackHandler?.Invoke(LoggerUtils.DrawingColorToConsoleColor(AniiModLogger.DefaultTextColor), obj.ToString());
        }

        public static void Msg(string txt)
        {
            if (!IsEnabled())
                return;
            AniiModLogger.PassLogMsg(AniiModLogger.DefaultTextColor, txt, ColorARGB.CornflowerBlue, "DEBUG");
            MsgCallbackHandler?.Invoke(LoggerUtils.DrawingColorToConsoleColor(AniiModLogger.DefaultTextColor), txt);
        }

        public static void Msg(string txt, params object[] args)
        {
            if (!IsEnabled())
                return;
            AniiModLogger.PassLogMsg(AniiModLogger.DefaultTextColor, string.Format(txt, args), ColorARGB.CornflowerBlue, "DEBUG");
            MsgCallbackHandler?.Invoke(LoggerUtils.DrawingColorToConsoleColor(AniiModLogger.DefaultTextColor), string.Format(txt, args));
        }

        public static void Error(string txt)
        {
            if (!IsEnabled())
                return;
            AniiModLogger.PassLogError(txt, "DEBUG", false);
            ErrorCallbackHandler?.Invoke(txt);
        }

        public static event Action<ConsoleColor, string> MsgCallbackHandler;

        public static event Action<string> ErrorCallbackHandler;

        public static bool IsEnabled()
            => LoaderConfig.Current.Loader.DebugMode;
    }
}