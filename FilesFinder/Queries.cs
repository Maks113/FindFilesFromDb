using System.Collections.Generic;
using System.Linq;

namespace FilesFinder
{
	public static class Queries
	{
		public static string SelectTargetRowsQuery(
			string tableName,
			string pathField,
			string idField,
			IEnumerable<string> infoFields)
		{
			var fields = new List<string> { idField, pathField };
			fields.AddRange(infoFields);

			return $@"
				SELECT {string.Join(", ", fields)}
				FROM {tableName};";
		}

		public static string RecreateResultsTable(string tableName)
		{
			return $@"
				IF OBJECT_ID(N'dbo.{tableName}', N'U') IS NOT NULL  
				   DROP TABLE [dbo].[{tableName}];  
				
				CREATE TABLE dbo.{tableName} (
					id int NOT NULL IDENTITY (1, 1),
					external_id int NOT NULL,
					is_founded bit NOT NULL
					) ON [PRIMARY]
				
				ALTER TABLE dbo.{tableName} ADD CONSTRAINT
					PK_{tableName} PRIMARY KEY CLUSTERED 
					( id ) WITH ( 
						STATISTICS_NORECOMPUTE = OFF, 
						IGNORE_DUP_KEY = OFF, 
						ALLOW_ROW_LOCKS = ON, 
						ALLOW_PAGE_LOCKS = ON
					) ON [PRIMARY]
				
				ALTER TABLE dbo.{tableName} SET (LOCK_ESCALATION = TABLE)
				
            ";
		}

		public static string InsertResults(string tableName, IEnumerable<SearchFileResult> results)
		{
			var resultStrings = results
				.ToList()
				.Select(result => $@"('{result.Id}', '{(result.IsFounded ? "1" : "0")}')");
			return $@"
				INSERT INTO {tableName} (external_id, is_founded)
				VALUES {string.Join(", ", resultStrings)}
			";
		}
	}
}