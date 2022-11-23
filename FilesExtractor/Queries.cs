using ConfigurationManager.Conf;

namespace FilesExtractor
{
	public static class Queries
	{
		public static string GetFileInfo(Fileset filesetConf)
		{
			return $@"
				SELECT fs.id, fs.fileset_id, fs.path, fs.full_path, fs.creation_date, fs.user_id, fs.size, fs.name
				FROM {filesetConf.TableName} fs
				WHERE fs.fileset_id = @ID;
			";
		}
	}
}