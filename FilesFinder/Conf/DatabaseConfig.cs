namespace FilesFinder.Conf
{
	public class DatabaseConfig
	{
		public string ServerName { get; set; }
		public string User { get; set; }
		public string Password { get; set; }
		public string DatabaseName { get; set; }
		public int ConnectTimeout { get; set; }
	}
}