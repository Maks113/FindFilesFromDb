using System.Data;
using ConfigurationManager.Conf;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using Serilog;

namespace SqlCmdTools
{
	public static class SqlCmdUtil
	{

		public static async Task<bool> CheckConnection()
		{
			var connectionString = BuildRunRawSqlConnectionString(Configuration.GetDbConfiguration());
			await using (var connection = new SqlConnection(connectionString))
			{
				try
				{
					connection.Open();
					return true;
				}
				catch (SqlException)
				{
					return false;
				}
			}
		}
		
		public static async Task<DataTable> RunRawSql(
			string sql,
			IDictionary<string, object> parameters = null)
		{
			Log.Debug("    >>>> Запрос - {Query}", sql);
			var connectionString = BuildRunRawSqlConnectionString(Configuration.GetDbConfiguration());
			var dataTable = new DataTable();
			await using (var connection = new SqlConnection(connectionString))
			{
				await using (var cmd = connection.CreateCommand())
				{
					using (var sda = new SqlDataAdapter(cmd))
					{
						try
						{
							cmd.CommandText = sql;
							AddParameters(cmd, parameters);
							await connection.OpenAsync();
							sda.Fill(dataTable);
							return dataTable;
						}
						catch (Exception ex)
						{
							throw new Exception(
								$@"Exception: ${ex.Message} "
								+ $@"Error during sql query execution: {cmd.CommandText}. "
								+ $@" Parameters {JsonConvert.SerializeObject(parameters)}",
								ex
							);
						}
						finally
						{
							connection.Close();
						}
					}
				}
			}
		}

		private static void AddParameters(SqlCommand cmd, IDictionary<string, object> parameters)
		{
			var sqlParams = parameters?.Keys
				.Select(key => new SqlParameter(key, parameters[key]))
				.ToArray();
			if (sqlParams == null) return;
			cmd.Parameters.AddRange(sqlParams);
		}

		private static string BuildRunRawSqlConnectionString(DatabaseConfiguration? connectionSettings)
		{
			if (connectionSettings != null)
			{
				var builder = new SqlConnectionStringBuilder
				{
					DataSource = string.IsNullOrEmpty(connectionSettings.ServerName)
						? "."
						: connectionSettings.ServerName,
					UserID = connectionSettings.User,
					ConnectTimeout = connectionSettings?.ConnectTimeout ?? 300,
					Password = connectionSettings?.Password,
					InitialCatalog = connectionSettings?.DatabaseName,
					TrustServerCertificate = true
				};
				return builder.ConnectionString;
			}

			throw new Exception("Отсутствуют настройки подключения к БД");
		}
	}
}