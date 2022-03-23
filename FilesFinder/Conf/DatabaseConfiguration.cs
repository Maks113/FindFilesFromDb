namespace FilesFinder.Conf
{
	public class DatabaseConfiguration
	{
		public string ServerName { get; set; }
		public string User { get; set; }
		public string Password { get; set; }
		public string DatabaseName { get; set; }
		public int ConnectTimeout { get; set; }
	}
}