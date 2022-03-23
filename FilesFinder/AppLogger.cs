using FilesFinder.Conf;
using Serilog;
using Serilog.Exceptions;

namespace FilesFinder;

public static class AppLogger
{
	public static void Init()
	{
		Log.Logger = new LoggerConfiguration()
			.Enrich.WithExceptionDetails()
			.ReadFrom.Configuration(Configuration.GetAppConfiguration())
			.CreateLogger();
	}
}