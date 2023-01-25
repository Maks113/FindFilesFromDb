#nullable enable
using System.Text.RegularExpressions;
using CommandLine;
using ConfigurationManager;
using ConfigurationManager.Conf;
using Serilog;
using SqlCmdTools;

namespace FilesFinder
{
	// ReSharper disable once ClassNeverInstantiated.Global
	// ReSharper disable once ArrangeTypeModifiers
	class Program
	{
		private class RunOptions
		{
			[Option(
				's', "sourcePath",
				Required = true,
				HelpText = "Директория поиска файлов")
			]
			public string SourcePath { get; set; }

			[Option(
				't', "targetPath",
				Required = false,
				HelpText = "Директория для найденных файлов")
			]
			public string TargetPath { get; set; }

			[Option(
				"safe",
				Required = false,
				HelpText = "Использовать метод поиска с обработкой ошибок")
			]
			public bool IsSafe { get; set; }

			[Option(
				"test",
				Required = false,
				HelpText = "Тестовый запуск без копирования файлов")
			]
			public bool IsTest { get; set; }
		}

		private static RunOptions _runOptions;

		private static void Main(string[] args)
		{
			AppLogger.Init();
			Parser.Default
				.ParseArguments<RunOptions>(args)
				.WithParsed(options => { _runOptions = options; });
			try
			{
				Run().Wait();
			}
			catch (Exception e)
			{
				Log.Error(e, "Ошибка во время выполнения");
			}

			Console.Write("Press any key...");
			Console.ReadLine();
		}


		private static async Task Run()
		{
			Log.Information("==== Старт задач ====");

			var configuration = Configuration.GetFinderConfiguration();
			if (configuration?.Targets == null) return;

			await Parallel.ForEachAsync(configuration.Targets,
				(targetConfiguration, token) =>
				{
					if (token.IsCancellationRequested) return ValueTask.FromCanceled(token);
					ParallelConfig(targetConfiguration).Wait(token);
					return ValueTask.CompletedTask;
				});
			// 	(var targetConfiguration in configuration.Targets)
			// {
			// 	var missingFiles = await FindMissingFiles(targetConfiguration);
			// 	await SearchFilesInDirectory(missingFiles);
			// }
		}

		private static async Task ParallelConfig(FinderTargetConfiguration targetConfiguration)
		{
			var missingFiles = await FindMissingFiles(targetConfiguration);
			await SearchFilesInDirectory(missingFiles);
		}


		private static async Task SearchFilesInDirectory(
			IEnumerable<SelectMissingRowsResult> missingFiles
			)
		{
			foreach (var missingFile in missingFiles)
			{
				var fileName = Path.GetFileName(missingFile.Path);
				Log.Information("Поиск файла {FileName}",fileName);
				if (string.IsNullOrEmpty(fileName)) continue;
				var foundedFiles = GetFilesFrom(_runOptions.SourcePath, fileName, true);

				Log.Information("Поиск файла {FileName} найдено - {Length}",fileName, foundedFiles.Length);
				if (foundedFiles.Length == 0) continue;

				var targetPath = $@"{_runOptions.TargetPath}/"
				                 + $@"{ReplaceIllegalCharacters(missingFile.TableName)}-{ReplaceIllegalCharacters(missingFile.PathFieldName)}/"
				                 + $@"{missingFile.Id}";
				Directory.CreateDirectory(targetPath);
				
				File.WriteAllLines($@"${targetPath}/founded_in.txt", foundedFiles.ToArray());
				
				if (_runOptions.IsTest) continue;
				foreach (var (file, index) in foundedFiles.Select((file, index) => (file, index)))
				{
					var name = Path.GetFileNameWithoutExtension(file);
					var ext = Path.GetExtension(file);
					var targetName = $@"{targetPath}/{name}_{index}{ext}";
					Log.Information("Поиск файла {FileName} копирование {File} => {Target}",fileName, file, targetName);
					File.Copy(file, targetName, true);
				}
			}
		}
		
		private static async Task SearchFileInDirectory(
			SelectMissingRowsResult missingFile
		)
		{
			var fileName = Path.GetFileName(missingFile.Path);
			Log.Information("Поиск файла {FileName}",fileName);
			if (string.IsNullOrEmpty(fileName)) return;
			var foundedFiles = GetFilesFrom(_runOptions.SourcePath, fileName, true);

			Log.Information("Поиск файла {FileName} найдено - {Length}",fileName, foundedFiles.Length);
			if (foundedFiles.Length == 0) return;

			var targetPath = $@"{_runOptions.TargetPath}/"
			                 + $@"{ReplaceIllegalCharacters(missingFile.TableName)}-{ReplaceIllegalCharacters(missingFile.PathFieldName)}/"
			                 + $@"{missingFile.Id}";
			Directory.CreateDirectory(targetPath);
			
			await File.WriteAllLinesAsync($@"${targetPath}/founded_in.txt", foundedFiles.ToArray());
			
			if (_runOptions.IsTest) return;
			foreach (var (file, index) in foundedFiles.Select((file, index) => (file, index)))
			{
				var name = Path.GetFileNameWithoutExtension(file);
				var ext = Path.GetExtension(file);
				var targetName = $@"{targetPath}/{name}_{index}{ext}";
				Log.Information("Поиск файла {FileName} копирование {File} => {Target}",fileName, file, targetName);
				File.Copy(file, targetName, true);
			}
		}

		private static async Task<IEnumerable<SelectMissingRowsResult>> FindMissingFiles(FinderTargetConfiguration targetConfiguration)
		{
			var filesetConfigs = Configuration.GetFilesetsConfiguration()?.Filesets;
			var filesetConf = filesetConfigs?.First(fs => fs.Id == targetConfiguration.FilesetId);

			if (filesetConf == null)
			{
				Log.Error("Не найден конфиг для файлового хранилища {TargetFilesetId}", targetConfiguration.FilesetId);
				throw new Exception($"Не найден конфиг для файлового хранилища {targetConfiguration.FilesetId}");
			}
			
			Log.Information("Поиск строк с не найденными файлами для таблицы {Table} и поля {Field}", targetConfiguration.TableName, targetConfiguration.PathField);

			var query = Queries.SelectMissingRowsQuery(
				targetConfiguration.TableName,
				targetConfiguration.PathField,
				targetConfiguration.IdField,
				targetConfiguration.FilesetFieldName
			);
			var result = new List<SelectMissingRowsResult>();
			var missingFiles = await SqlCmdUtil.RunRawSql(query);
			Log.Information("Найдено файлов для таблицы {Table} и поля {Field} - {Count}", targetConfiguration.TableName, targetConfiguration.PathField, missingFiles.Rows.Count);
			
			for (var index = 0; index < missingFiles.Rows.Count; index++)
			{
				var row = missingFiles.Rows[index];
				var id = row[0].ToString() ?? "";
				var path = row[1].ToString() ?? "";

				result.Add(new SelectMissingRowsResult
				{
					Id = id, 
					Path = path, 
					TableName = targetConfiguration.TableName,
					PathFieldName = targetConfiguration.PathField
				});
			}

			return result;
		}

		private static string ReplaceIllegalCharacters(string path)
		{
			var regexSearch = new string(Path.GetInvalidPathChars());
			var r = new Regex($"[{Regex.Escape(regexSearch)}\"]");
			return r.Replace(path, "");
		}
		
		private static string[] GetFilesFrom(string dir, string searchPattern, bool recursive)
		{
			var files = new List<string>();

			var tempFiles = Array.Empty<string>();

			try
			{
				tempFiles = Directory.GetFiles(dir, searchPattern, SearchOption.TopDirectoryOnly);
			}
			catch
			{
			}

			files.AddRange(tempFiles);

			if (recursive)
			{
				var tempDirs = Array.Empty<string>();

				try
				{
					tempDirs = Directory.GetDirectories(dir, "*", SearchOption.TopDirectoryOnly);
				}
				catch
				{
				}

				foreach (var childDir in tempDirs)
					files.AddRange(GetFilesFrom(childDir, searchPattern, recursive));
			}

			return files.ToArray();
		}
	}
}