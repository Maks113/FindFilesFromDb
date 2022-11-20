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
				.Build();
		}

        public static DatabaseConfiguration GetDbConfiguration()
		{
			return GetAppConfiguration()
				.GetSection(nameof(DatabaseConfiguration))
				.Get<DatabaseConfiguration>();
		}
		
		public static FinderConfiguration GetFinderConfiguration()
		{
			return GetAppConfiguration()
				.GetSection(nameof(FinderConfiguration))
				.Get<FinderConfiguration>();
		}	
		
		public static FilesetsConfiguration GetFilesetsConfiguration()
		{
			return GetAppConfiguration()
				.GetSection(nameof(FilesetsConfiguration))
				.Get<FilesetsConfiguration>();
		}		
		public static UserConfiguration GetUserConfiguration()
		{
			return GetAppConfiguration()
				.GetSection(nameof(UserConfiguration))
				.Get<UserConfiguration>();
		}
		
		public static PathMapConfiguration GetPathMappingConfig()
		{
			return GetAppConfiguration()
				.GetSection(nameof(PathMapConfiguration))
				.Get<PathMapConfiguration>();
		}
	}
}