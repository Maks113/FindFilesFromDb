using System.Data;
using System.Text.RegularExpressions;
using CommandLine;
using ConfigurationManager;
using ConfigurationManager.Conf;
using Microsoft.VisualBasic.FileIO;
using Serilog;
using SqlCmdTools;

namespace FilesExtractor // Note: actual namespace depends on the project name.
{
	internal class Program
	{
		private class RunOptions
		{
			[Option(
				'i', "filesetIdField",
				Required = true,
				HelpText = "Имя поля, содержащее id файла в файловой таблице")
			]
			public string FilesetIdField { get; set; }

			[Option(
				'f', "dataFilePath",
				Required = true,
				HelpText = "Файл с выборкой")
			]
			public string DataFilePath { get; set; }

			[Option(
				'p', "targetPath",
				Required = true,
				HelpText =
					"Целевая директория для файлов. В пути можно применять названия колонок из выборки {FileName}")
			]
			public string TargetPath { get; set; }

			[Option(
				't', "TargetFilesetId",
				Required = true,
				HelpText = "Id файлсета из настроек для выбора файла в хранилище")
			]
			public string TargetFilesetId { get; set; }

			[Option(
				"test",
				Required = false,
				HelpText = "Тестовый запуск без копирования файлов")
			]
			public bool Test { get; set; }
		}

		private static RunOptions _runOptions;

		static async Task Main(string[] args)
		{
			AppLogger.Init();
			Parser.Default
				.ParseArguments<RunOptions>(args)
				.WithParsed(options => { _runOptions = options; });

			Log.Information("FilesetIdField: {FilesetIdField}", _runOptions.FilesetIdField);
			Log.Information("TargetPath: {TargetPath}", _runOptions.TargetPath);
			Log.Information("DataFilePath: {DataFilePath}", _runOptions.DataFilePath);
			Log.Information("TargetFilesetId: {TargetFilesetId}", _runOptions.TargetFilesetId);
			Log.Information("Test: {TargetFilesetId}", _runOptions.Test ? "Да" : "Нет");

			await ExtractFiles();
		}

		private static async Task ExtractFiles()
		{
			var filesetConfigs = Configuration.GetFilesetsConfiguration()?.Filesets;
			var filesetConf = filesetConfigs?.First(fs => fs.Id == _runOptions.TargetFilesetId);
			if (filesetConf == null)
			{
				Log.Error("Не найден конфиг для файлового хранилища {TargetFilesetId}", _runOptions.TargetFilesetId);
				throw new Exception($"Не найден конфиг для файлового хранилища {_runOptions.TargetFilesetId}");
			}

			if (!await SqlCmdUtil.CheckConnection())
			{
				Log.Error("Ошибка подключения к базе данных");
				throw new Exception("Ошибка подключения к базе данных");
			}

			using var parser = new TextFieldParser(_runOptions.DataFilePath);
			parser.TextFieldType = FieldType.Delimited;
			parser.SetDelimiters(";");
			
			var headers = parser.ReadFields();
			if (headers == null)
			{
				Log.Error("Отсутствуют данные в файле");
				throw new Exception("Отсутствуют данные в файле");
			}

			var filesetIdField = GetFieldValue(headers, headers, _runOptions.FilesetIdField);
			if (filesetIdField == null)
			{
				Log.Error("Не найдена колонка с filesetID, проверьте {FieldName}", _runOptions.FilesetIdField);
				throw new Exception($"Не найдена колонка с filesetID, проверьте {_runOptions.FilesetIdField}");
			}

			while (!parser.EndOfData)
			{
				var line = parser.ReadFields();
				if (line == null) continue;
				Log.Information("Загрузка по данным: {Fields}", string.Join(", ", line));

				var filesetId = GetFieldValue(headers, line, _runOptions.FilesetIdField);
				if (filesetId == null || filesetId.ToLower() == "null")
				{
					Log.Information("    >>> Пустой id файла в хранилище");
					continue;;
				}
				Log.Information("    >>> id файла в хранилище: {Id}", filesetId);

				var linePath = GetPathByTemplate(_runOptions.TargetPath, headers, line);
				Log.Information("    >>> Путь копирования: {Path}", linePath);

				var selectedFile = await getFileFromStorage(filesetConf, filesetId);
				var from = $"{filesetConf.Host}/{filesetConf.StaticPath}/{selectedFile?.Path}";
				var to = $"{linePath}/{selectedFile?.Name}";
				
				if (!_runOptions.Test)
				{
					Log.Information("    >>> Копирование: {From} -> {To}", from, to);
					CopyFile(from, to);
				}
			}
		}

		private static string? GetFieldValue(IEnumerable<string>? fields, IEnumerable<string>? line, string name)
		{
			var index = fields
				?.Select((f, i) => (f, i))
				.First(result => string.Equals(result.f, name, StringComparison.CurrentCultureIgnoreCase))
				.i;

			return index != null
				? line?.ToArray()[index ?? 0]
				: null;
		}

		private static string GetPathByTemplate(string template, IEnumerable<string>? fields, IEnumerable<string>? line)
		{
			var regex = new Regex("{([a-zA-z]+)}");
			var matches = regex.Matches(template);
			var path = template;

			foreach (Match match in matches)
			{
				var value = GetFieldValue(fields, line, match.Groups[1].Value);
				path = path.Replace($"{match.Value}", value);
			}

			return path;
		}

		private static async Task<FileInfoDto?> getFileFromStorage(Fileset filesetConf, string filesetId)
		{
			var query = Queries.GetFileInfo(filesetConf);
			var files = await SqlCmdUtil.RunRawSql(query, new Dictionary<string, object> { ["@ID"] = filesetId });

			Log.Information("    >>> Найдено файлов в хранилище: {Rows}", files.Rows.Count);
			var selectedFile = files.Rows
				.Cast<DataRow>()
				.Select(fileRow => new FileInfoDto
				{
					FilesetId = fileRow.ItemArray[1].ToString(),
					Path = fileRow.ItemArray[2].ToString(),
					FullPath = fileRow.ItemArray[3].ToString(),
					CreationDate = (DateTime)fileRow.ItemArray[4],
					UserId = int.Parse(fileRow.ItemArray[5].ToString()),
					Size = long.Parse(fileRow.ItemArray[6].ToString()),
					Name = fileRow.ItemArray[7].ToString(),
				})
				.ToList()
				.OrderByDescending(f => f.CreationDate)
				.FirstOrDefault();
				
			Log.Information("    >>> Выбран файл: {File} Date: {Date} Size: {Size}", selectedFile?.Path, selectedFile?.CreationDate, selectedFile?.Size);
			return selectedFile;
		}

		private static void CopyFile(string from, string to)
		{
			Directory.CreateDirectory(Path.GetDirectoryName(to));
			
			File.Copy(from, to, true);
			Log.Information("    >>> Успешно");
		}
	}
}