using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommandLine;
using FilesFinder.Conf;
using Serilog;

namespace FilesFinder
{
	// ReSharper disable once ClassNeverInstantiated.Global
	// ReSharper disable once ArrangeTypeModifiers
	class Program
	{
		private class RunOptions
		{
			[Option(
				'm', "mode",
				Required = false,
				Default = "find",
				HelpText = "find - только поиск\n " +
				           "rewrite - пересоздание таблиц файлов, очистка файловых полей и повторное заполнение")]
			public string Mode { get; set; }
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

			var checkConfigResult = await CheckConfig();
			if (!checkConfigResult) return;

			var prepareStorageResult = await prepareStorage();
			if (!prepareStorageResult) return;

			var prepareDatabaseResult = await PrepareDatabase();
			if (!prepareDatabaseResult) return;
			
			await FindAndFillDatabase();
		}

		private static async Task<bool> CheckConfig()
		{
			Log.Information("==== Старт задачи проверки конфигурации ====");
			var finderConfiguration = Configuration.GetFinderConfiguration();
			var filesetsConfiguration = Configuration.GetFilesetsConfiguration();
			var dbConfiguration = Configuration.GetDbConfiguration();

			Log.Information("---> Проверка целевых конфигураций");
			foreach (var target in finderConfiguration.Targets)
			{
				if (target.FilesetId == null)
				{
					Log.Error("Для целевой конфигурации не заполнен FilesetId: " +
					          "target.TableName: {TableName}, " +
					          "target.FilesetId: {FilesetId}",
						target.TableName, target.FilesetId);
					return false;
				}
				
				if (target.FilesetFieldName == null)
				{
					Log.Error("Для целевой конфигурации не заполнен FilesetFieldName: " +
					          "target.TableName: {TableName}, " +
					          "target.FilesetId: {FilesetId}",
						target.TableName, target.FilesetId);
					return false;
				}

				var searchResult = filesetsConfiguration.Filesets
					.FirstOrDefault(fileset => fileset.Id == target.FilesetId);

				if (searchResult == null)
				{
					Log.Error("Конфигурация целевой таблицы содержит неизвестный filesetId: " +
					          "target.TableName: {TableName}, " +
					          "target.FilesetId: {FilesetId}",
						target.TableName, target.FilesetId);
					return false;
				}
			}

			Log.Information("---> Проверка доступа до файловой директории");
			foreach (var fileset in filesetsConfiguration.Filesets)
			{
				var connectResult = new DirectoryInfo(fileset.Host);
				if (!connectResult.Exists)
				{
					Log.Error("Ошибка в конфигурации набора файлов. Ошибка доступа к директории " +
					          "fileset.Id: {Id}, " +
					          "fileset.Host: {Host}",
						fileset.Id, fileset.Host);
					return false;
				}
			}

			Log.Information("---> Проверка доступа до базы данных");

			var dbConnectionResult = await SqlCmdUtil.CheckConnection();
			if (!dbConnectionResult)
			{
				Log.Error("Ошибка в конфигурации базы данных. Ошибка подключения " +
				          "ServerName: {ServerName}, " +
				          "DatabaseName: {DatabaseName}, " +
				          "User: {User}, ",
					dbConfiguration.ServerName, dbConfiguration.DatabaseName, dbConfiguration.User);
				return false;
			}

			return true;
		}

		private static async Task<bool> PrepareDatabase()
		{
			Log.Information("==== Старт задачи подготовки базы данных ====");

			Log.Information("---> Подготовка таблиц файлсетов");
			var filesetsConfiguration = Configuration.GetFilesetsConfiguration();
			foreach (var fileset in filesetsConfiguration.Filesets)
			{
				try
				{
					Log.Information("		Создание таблицы {TableName}", fileset.TableName);
					var query = Queries.RecreateFilesetTable(fileset.TableName);
					await SqlCmdUtil.RunRawSql(query);
				}
				catch (Exception e)
				{
					Log.Error(e, "Ошибка создания таблицы");
					return false;
				}
			}

			Log.Information("---> Добавление файловых полей в целевые таблицы");
			var finderConfiguration = Configuration.GetFinderConfiguration();
			foreach (var target in finderConfiguration.Targets)
			{
				try
				{
					Log.Information(
						"		Добавление столбца файлсета {FilesetFieldName} в таблицу {TableName}",
						target.FilesetFieldName, target.TableName
					);
					var query = Queries.RecreateDocumentField(target.TableName, target.FilesetFieldName);
					await SqlCmdUtil.RunRawSql(query);
				}
				catch (Exception e)
				{
					Log.Error(e, "Ошибка добавления столбца в таблицу");
					return false;
				}
			}

			return true;
		}

		private static async Task<bool> prepareStorage()
		{
			Log.Information("==== Старт задачи подготовки хранилища ====");
			var filesetsConfiguration = Configuration.GetFilesetsConfiguration();
			foreach (var fileset in filesetsConfiguration.Filesets)
			{
				try
				{
					Log.Information(
						"		Создание директории {Host}{StaticPath} для файлсета {FilesetId}",
						fileset.Host, fileset.StaticPath, fileset.Id
						);
					Directory.CreateDirectory($@"{fileset.Host}{fileset.StaticPath}");
				}
				catch (Exception e)
				{
					Log.Error(e, "Ошибка создания таблицы");
					return false;
				}
			}
			return true;
		}
		
		private static async Task FindAndFillDatabase()
		{
			Log.Information(
				"==== Старт задачи поиска файлов и заполнения таблицы в режиме {Mode} ====",
				_runOptions.Mode == "rewrite"
					? "поиск, копирование и заполнение таблиц"
					: "только поиск файлов"
			);

			var configuration = Configuration.GetFinderConfiguration();
			var filesetsConfiguration = Configuration.GetFilesetsConfiguration();
			foreach (var target in configuration.Targets)
			{
				Log.Information("Поиск файлов в таблице {TableName} в поле {PathField}", target.TableName,
					target.PathField);

				var fileset = filesetsConfiguration.Filesets.Single(fileset => fileset.Id == target.FilesetId);

				var data = await GetRows(target);
				await CheckFiles(data, target, fileset);
			}
		}

		private static async Task<DataTable> GetRows(FinderTargetConfiguration target)
		{
			var query = Queries.SelectTargetRowsQuery(
				target.TableName,
				target.PathField,
				target.IdField ?? "ID",
				target.InfoFields?.Select(value => value.FieldName) ?? new List<string>()
			);
			Log.Debug("Получение строк");
			var result = await SqlCmdUtil.RunRawSql(query);
			Log.Debug("Выбрано строк - {Count}", result.Rows.Count);
			return result;
		}


		private static async Task CheckFiles(DataTable data, FinderTargetConfiguration target, Fileset fileset)
		{
			var suffix = target.TableName;
			Directory.CreateDirectory("Results");
			long sizeSum = 0;
			var foundedFiles = 0;
			var emptyLines = 0;
			var notFoundedFiles = 0;

			await using StreamWriter searchLog = new($@"Results\SearchLog_{suffix}.txt");
			await using StreamWriter notFoundLog = new($@"Results\NotFound_{suffix}.txt");
			await using StreamWriter foundLog = new($@"Results\Found_{suffix}.txt");

			await using StreamWriter notFoundCsv = new(
				File.Open($@"Results\NotFound_{suffix}.csv", FileMode.Create),
				Encoding.UTF8
			);
			await notFoundCsv.WriteLineAsync(
				$@"{target.IdField ?? "ID"};"
				+ $@"{target.PathField};"
				+ $@"{string.Join(
					";",
					target.InfoFields?.Select(value => value.FieldName) ?? new List<string>()
				)}");

			var results = new List<SearchFileResult>();
			foreach (DataRow row in data.Rows)
			{
				var id = row[0].ToString();
				var path = row[1].ToString();
				if (string.IsNullOrEmpty(path))
				{
					emptyLines += 1;
					continue;
				}

				var founded = CheckFileSize(path);

				var additionalDataValues = row.ItemArray
					.Skip(2)
					.Select(value =>
						value is DBNull
						|| string.IsNullOrEmpty(value.ToString()?.TrimEnd())
							? "-"
							: value.ToString())
					.ToList();
				var additionalDataStrings = additionalDataValues.Zip(
					target.InfoFields?.Select(v => v.Description) ?? new List<string>(),
					(value, description) => $@"{description}: {value}");

				var result = $@"Файл {path} "
				             + $@"дополнительные сведения: {string.Join(", ", additionalDataStrings)} "
				             + $@"{(founded != null ? "найден" : "не найден")} "
				             + $@"{(founded != null ? $@"Размер {founded}" : "")}";

				await searchLog.WriteLineAsync(result);
				results.Add(new SearchFileResult { Id = id, IsFounded = founded != null });

				if (founded != null)
				{
					sizeSum += (long)founded;
					foundedFiles += 1;
					await foundLog.WriteLineAsync(result);

					if (_runOptions.Mode == "rewrite")
					{
						await CopyFile(path, id, fileset, target);
					}
				}
				else
				{
					Log.Warning("   >>> Файл не найден: {Path}", path);
					notFoundedFiles += 1;
					await notFoundLog.WriteLineAsync(result);
					await notFoundCsv.WriteLineAsync(
						$@"{id};"
						+ $@"{path};"
						+ $@"{string.Join(";", additionalDataValues)}");
				}
			}

			if (_runOptions.Mode == "find")
			{
				await SaveToTable(target.TableName, target.PathField, results);
			}


			var statLines = new List<string>
			{
				" ",
				$@" ==== Статистика по таблице {target.TableName} по полю {target.PathField} ==== ",
				$@"Выбрано строк: {data.Rows.Count}",
				$@"Пустых строк: {emptyLines}",
				$@"Не найдено файлов: {notFoundedFiles}",
				$@"Найдено файлов: {foundedFiles}",
				$@"Общий размер найденных файлов: {sizeSum / 1024 / 1024}Мб",
			};

			foreach (var statLine in statLines)
			{
				Log.Information("{StatLine}", statLine);
				await searchLog.WriteLineAsync(statLine);
				await foundLog.WriteLineAsync(statLine);
			}
		}

		private static async Task CopyFile(string path, string id, Fileset fileset, FinderTargetConfiguration target)
		{
			var guid = Guid.NewGuid().ToString();
			var userConfig = Configuration.GetUserConfiguration();
			var basePath = $@"{fileset.Host}{fileset.StaticPath}\";
			
			var fi = new FileInfo(path);
			var date = DateTime.Now.ToString("yyyy.MM.dd_hh.mm.ss.fff");
			var newFilename = $@"{Path.GetFileNameWithoutExtension(fi.Name)}_{date}{fi.Extension}";
			var filePath = $@"{guid}\{newFilename}";
			var newPath = $@"{basePath}{filePath}";
			Directory.CreateDirectory(Path.GetDirectoryName(newPath));
			Log.Information("Копирование файла из {Path} в {NewPath}", path, newPath);
			File.Copy(path, newPath, true);
			
			Log.Information("Добавление данных о файле в БД {Path} ", path);
			var fileInfo = new FileInfoDto
			{
				Name = fi.Name,
				FullPath = newPath,
				Path = filePath,
				Size = fi.Length,
				CreationDate = DateTime.Now,
				FilesetId = guid,
				UserId = userConfig.Id,
			};
			
			var query = Queries.InsertFileInfo(target, id, fileset.TableName, fileInfo);
			await SqlCmdUtil.RunRawSql(query);
		}

		private static async Task SaveToTable(string tableName, string fieldName, IEnumerable<SearchFileResult> results)
		{
			var regexp = new Regex("[][ ]");
			tableName = regexp.Replace(tableName, "");
			fieldName = regexp.Replace(fieldName, "");
			var name = $@"del_FileSearchResults_{tableName}_{fieldName}";
			var query = Queries.RecreateResultsTable(name);
			Log.Debug("Пересоздание таблицы");

			await SqlCmdUtil.RunRawSql(query);


			var chunks = results
				.Select((x, i) => new { Index = i, Value = x })
				.GroupBy(x => x.Index / 500)
				.Select(x => x.Select(v => v.Value).ToList());

			foreach (var chunk in chunks)
			{
				query = Queries.InsertResults(name, chunk);
				Log.Debug("Вставка данных");
				await SqlCmdUtil.RunRawSql(query);
			}
		}

		private static long? CheckFileSize(string path)
		{
			Log.Debug("Проверка файла: {Path}", path);
			var fi = new FileInfo(path);
			if (fi.Exists)
			{
				return fi.Length;
			}

			return null;
		}
	}
}