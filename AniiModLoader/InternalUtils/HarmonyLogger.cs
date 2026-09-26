using HarmonyLib.Tools;

namespace AniiModLoader.InternalUtils;

internal static class HarmonyLogger
{
    private static readonly Logger.LogChannel[] OrderedLogChannels =
    [
        Logger.LogChannel.Error,
        Logger.LogChannel.Warn,
        Logger.LogChannel.Info,
        Logger.LogChannel.Debug
    ];

    internal static void Setup()
    {
        Logger.ChannelFilter = DetermineChannelFilter(LoaderConfig.Current.Loader.HarmonyLogLevel);
        Logger.MessageReceived += LoggerOnMessageReceived;
    }

    private static void LoggerOnMessageReceived(object sender, Logger.LogEventArgs e)
    {
        switch (e.LogChannel)
        {
            case Logger.LogChannel.Info:
            case Logger.LogChannel.IL:
            case Logger.LogChannel.Debug:
                AniiModLogger.Msg(e.Message);
                break;
            case Logger.LogChannel.Warn:
                AniiModLogger.Warning(e.Message);
                break;
            case Logger.LogChannel.Error:
                AniiModLogger.Error(e.Message);
                break;
        }
    }

    private static Logger.LogChannel DetermineChannelFilter(LoaderConfig.CoreConfig.HarmonyLogVerbosity channel)
    {
        if (channel == LoaderConfig.CoreConfig.HarmonyLogVerbosity.IL)
            return Logger.LogChannel.All;
        if (channel == LoaderConfig.CoreConfig.HarmonyLogVerbosity.None)
            return Logger.LogChannel.None;

        Logger.LogChannel channelFilter = Logger.LogChannel.None;
        foreach (var logChannel in OrderedLogChannels)
        {
            channelFilter |= logChannel;
            Logger.LogChannel newChannel = channel switch
            {
                LoaderConfig.CoreConfig.HarmonyLogVerbosity.Error => Logger.LogChannel.Error,
                LoaderConfig.CoreConfig.HarmonyLogVerbosity.Warn => Logger.LogChannel.Warn,
                LoaderConfig.CoreConfig.HarmonyLogVerbosity.Info => Logger.LogChannel.Info,
                LoaderConfig.CoreConfig.HarmonyLogVerbosity.Debug => Logger.LogChannel.Debug,
                _ => Logger.LogChannel.None
            };
            if (logChannel == newChannel)
                break;
        }

        return channelFilter;
    }
}