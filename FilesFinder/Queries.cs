namespace FilesFinder
{
	public static class Queries
	{
		public static string SelectMissingRowsQuery(
			string tableName,
			string pathField,
			string idField,
			string filesetFieldName
		)
		{
			var fields = new List<string> { idField, pathField };

			return $@"
				SELECT {string.Join(", ", fields)}
				FROM {tableName}
				WHERE {filesetFieldName} IS NULL
				  AND {pathField} <> '';";
		}
	}
}