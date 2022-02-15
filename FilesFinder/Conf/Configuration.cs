using System;
using Microsoft.Extensions.Configuration;

namespace FilesFinder.Conf
{
	public class Configuration
	{
		public static IConfiguration GetAppConfiguration()
		{
			return new ConfigurationBuilder()
				.SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
				.AddJsonFile("appsettings.json", false, true)
				.AddJsonFile("appsettings.local.json", true, true)
				.Build();
		}

		public static DatabaseConfig GetDbConfiguration()
		{
			return GetAppConfiguration()
				.GetSection(nameof(DatabaseConfig))
				.Get<DatabaseConfig>();
		}
		
		public static FinderConfiguration GetFinderConfiguration()
		{
			return GetAppConfiguration()
				.GetSection(nameof(FinderConfiguration))
				.Get<FinderConfiguration>();
		}
	}
}