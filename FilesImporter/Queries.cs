using System.Collections.Generic;
using System.Linq;
using ConfigurationManager.Conf;

namespace FilesImporter
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
		
		public static string SelectTargetRowsForFixQuery(
			string tableName,
			string pathField,
			string idField,
			IEnumerable<string> infoFields)
		{
			var fields = new List<string> { idField, pathField };
			fields.AddRange(infoFields);

			return $@"
				SELECT {string.Join(", ", fields)}
				FROM {tableName}
				WHERE ISNULL({pathField}, '') <> '';";
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
		
		public static string RecreateFilesetTable(string tableName)
		{
			return $@"
				IF OBJECT_ID(N'dbo.{tableName}', N'U') IS NOT NULL  
				   DROP TABLE [dbo].[{tableName}];

				CREATE TABLE dbo.{tableName} (
				  id INT IDENTITY
				 ,fileset_id UNIQUEIDENTIFIER NOT NULL
				 ,path TEXT NULL
				 ,full_path TEXT NULL
				 ,creation_date DATETIME NOT NULL
				 ,user_id INT NOT NULL
				 ,size DECIMAL NOT NULL
				 ,name VARCHAR(255) NOT NULL
				 ,CONSTRAINT PK_{tableName}_id PRIMARY KEY CLUSTERED (id)
				)
            ";
		}

		public static string RecreateDocumentField(string tableName, string columnName)
		{
			return $@"
				IF OBJECT_ID(N'dbo.{tableName}', N'U') IS NOT NULL 
				BEGIN
					IF COL_LENGTH('dbo.{tableName}', '{columnName}') IS NOT NULL
						ALTER TABLE dbo.{tableName}
  							DROP COLUMN {columnName};

					ALTER TABLE dbo.{tableName}
  						ADD {columnName} UNIQUEIDENTIFIER NULL;
				END
			";
		}

		public static string InsertFileInfo (
			FinderTargetConfiguration target, 
			string targetRowId,
			string filesetTableName
			// FileInfoDto fileInfoDto
			)
		{
			// var date = fileInfoDto.CreationDate.ToString("yyyy-MM-dd HH:mm:ss:fff");
			return $@"
				UPDATE dbo.{target.TableName}
				SET {target.FilesetFieldName} = @FilesetId
				WHERE dbo.{target.TableName}.{target.IdField} = {targetRowId};

				INSERT INTO {filesetTableName} (fileset_id, full_path, path, creation_date, user_id, size, name)
				VALUES (@FilesetId, @FullPath, @Path, @Date, @UserId, @Size, @Name);
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

		public static string GetFileInfo(FinderTargetConfiguration filesConf, Fileset filesetConf)
		{
			return $@"
				SELECT fs.id, fs.fileset_id, fs.path, fs.full_path, fs.creation_date, fs.user_id, fs.size, fs.name
				FROM {filesConf.TableName} tn
				INNER JOIN {filesetConf.TableName} fs on fs.fileset_id = tn.{filesConf.FilesetFieldName}
				WHERE tn.{filesConf.IdField} = @ID;
			";
		}
	}
}