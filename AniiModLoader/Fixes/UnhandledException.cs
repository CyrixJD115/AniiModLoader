using System;

namespace AniiModLoader.Fixes
{
	internal static class UnhandledException
	{
		internal static void Install(AppDomain domain) =>
			domain.UnhandledException +=
				(sender, args) =>
					AniiModLogger.Error((args.ExceptionObject as Exception).ToString());
	}
}