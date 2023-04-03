#nullable enable
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CommandLine;
using ConfigurationManager;
using ConfigurationManager.Conf;
using Microsoft.VisualBasic.FileIO;
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
				'f', "dataFilePath",
				Required = true,
				HelpText = "Файл с выборкой")
			]
			public string DataFilePath { get; set; }

			[Option(
				'i', "filePathField",
				Required = true,
				HelpText = "Имя поля, содержащее путь к файлу")
			]
			public string FilePathField { get; set; }

			[Option(
				's', "sourcePath",
				Required = true,
				HelpText = "Директория поиска файлов")
			]
			public string SourcePath { get; set; }

			[Option(
				't', "targetPath",
				Required = false,
				HelpText = "Директория для найденных файлов. " +
				           "В пути можно применять названия колонок из выборки {FileName}." +
				           " Если не указана, копирование файлов не выполняется")
			]
			public string TargetPath { get; set; }

			[Option(
				'r', "resultString",
				Required = false,
				HelpText = "Строчка, которая будет формироваться для каждого файла в выборке. " +
				           "В пути можно применять названия колонок из выборки {FileName}." +
				           " Если не указана, файлы с результатами не будут формироваться")
			]
			public string ResultStringTemplate { get; set; }

			[Option(
				"sync",
				Required = false,
				HelpText = "Запустить синхронно")
			]
			public bool Sync { get; set; }
		}

		private static RunOptions _runOptions;
		private static FilePathDTO[] _allFiles;

		private static void Main(string[] args)
		{
			AppLogger.Init();
			Parser.Default
				.ParseArguments<RunOptions>(args)
				.WithParsed(options => { _runOptions = options; });

			Log.Information("==== Запуск ====");

			Log.Information("DataFilePath: {DataFilePath}", _runOptions.DataFilePath);
			Log.Information("FilePathField: {FilePathField}", _runOptions.FilePathField);
			Log.Information("SourcePath: {SourcePath}", _runOptions.SourcePath);
			Log.Information("TargetPath: {TargetPath}", _runOptions.TargetPath);
			Log.Information("ResultStringTemplate: {ResultStringTemplate}", _runOptions.ResultStringTemplate);
			Log.Information("Sync: {Sync}", _runOptions.Sync);

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

			Log.Information("Индексация фалов в директории поиска...");

			_allFiles = GetAllFilesFrom(_runOptions.SourcePath)
				.Select(filePath => new FilePathDTO { Name = Path.GetFileName(filePath), Path = filePath })
				.ToArray();

			Log.Information("Индексация завершена. Поиск среди {AllFiles} файлов", _allFiles.Length);

			var missingFiles = GetDataFromFile(_runOptions.DataFilePath);
			var founded = await SearchFilesInDirectory(missingFiles);

			var headers = missingFiles.FirstOrDefault()?.Headers ?? Array.Empty<string>();
			await File.WriteAllLinesAsync($@"founded.csv", new []{ $@"""{string.Join("\";\"", headers)}""" }, Encoding.Unicode);
			await File.AppendAllLinesAsync($@"founded.csv", founded.Select(f => f.ResultString), Encoding.Unicode);

			PrintStatistic(founded);
		}

		private static IEnumerable<SelectMissingRowsResult> GetDataFromFile(string dataFilePath)
		{
			Log.Information("Получение строк из файла...");
			using var parser = new TextFieldParser(dataFilePath);
			parser.TextFieldType = FieldType.Delimited;
			parser.SetDelimiters(";");

			var headers = parser.ReadFields();
			if (headers == null)
			{
				Log.Error("Отсутствуют данные в файле");
				throw new Exception("Отсутствуют данные в файле");
			}

			var index = 1;
			var result = new List<SelectMissingRowsResult>();

			while (!parser.EndOfData)
			{
				index += 1;
				var line = parser.ReadFields();
				if (line == null) continue;
				Log.Information("Строка: {Fields}", string.Join(", ", line));

				var filePath = GetFieldValue(headers, line, _runOptions.FilePathField);
				if (filePath == null || filePath.ToLower() == "null")
				{
					Log.Information("    >>> Пустой путь к файлу в строке");
					continue;
				}

				result.Add(new SelectMissingRowsResult
				{
					Index = index,
					Path = filePath,
					// TODO вынести из объекта результата
					Headers = headers,
					Line = line,
				});
			}

			return result;
		}

		private static async Task<IEnumerable<SearchResultDTO>> SearchFilesInDirectory(
			IEnumerable<SelectMissingRowsResult> missingFiles
		)
		{
			var result = new List<SearchResultDTO>();
			if (_runOptions.Sync)
			{
				foreach (var missingFile in missingFiles)
				{
					result.Add(await SearchFileInDirectory(missingFile));
				}
			}
			else
			{
				await Parallel.ForEachAsync(missingFiles, (missingFile, token) =>
				{
					if (token.IsCancellationRequested) return ValueTask.FromCanceled(token);
					var task = SearchFileInDirectory(missingFile);
					task.Wait(token);
					result.Add(task.Result);
					return ValueTask.CompletedTask;
				});
			}

			return result;
		}

		private static async Task<SearchResultDTO> SearchFileInDirectory(
			SelectMissingRowsResult missingFile
		)
		{
			var fileName = Path.GetFileName(missingFile.Path);
			Log.Information("Поиск файла {FileName}", fileName);
			var baseMessage = InsertFieldValuesToTemplate(
				_runOptions.ResultStringTemplate,
				missingFile.Headers,
				missingFile.Line);
			if (string.IsNullOrEmpty(fileName))
				return new SearchResultDTO
				{
					FoundFiles = Array.Empty<string>(),
					ResultString = @$"{baseMessage};Ошибка Путь к файлу пустой",
				};
			// var foundedFiles = GetFilesFrom(_runOptions.SourcePath, fileName, true);
			var foundedFiles = FindFiles(fileName);

			Log.Information("Поиск файла {FileName} найдено - {Length}", fileName, foundedFiles.Length);
			if (foundedFiles.Length == 0)
				return new SearchResultDTO
				{
					FoundFiles = Array.Empty<string>(),
					ResultString = @$"{baseMessage};Файлы не найдены",
				};

			if (!string.IsNullOrEmpty(_runOptions.TargetPath))
			{
				var basePath = InsertFieldValuesToTemplate(
					_runOptions.ResultStringTemplate,
					missingFile.Headers,
					missingFile.Line);
				var targetPath = $@"{ReplaceIllegalCharacters(basePath)}/";
				Directory.CreateDirectory(targetPath);

				await File.WriteAllLinesAsync($@"{targetPath}/founded_in.txt", new[] { $@"{baseMessage}" }, Encoding.Unicode);
				await File.AppendAllLinesAsync($@"{targetPath}/founded_in.txt", foundedFiles.ToArray(), Encoding.Unicode);

				foreach (var (file, index) in foundedFiles.Select((file, index) => (file, index)))
				{
					var name = Path.GetFileNameWithoutExtension(file);
					var ext = Path.GetExtension(file);
					var targetName = $@"{targetPath}/{name}_{index}{ext}";
					Log.Information("    >>>Поиск файла {FileName} копирование {File} => {Target}", fileName, file,
						targetName);
					// if (_runOptions.IsTest) continue;
					File.Copy(file, targetName, true);
				}
			}

			return new SearchResultDTO
			{
				ResultString = @$"{baseMessage};{string.Join(';', foundedFiles)}",
				FoundFiles = foundedFiles,
			};
		}

		private static string InsertFieldValuesToTemplate(string template, IEnumerable<string>? fields,
			IEnumerable<string>? line)
		{
			var regex = new Regex("{([a-zA-Z #-]+)}");
			var matches = regex.Matches(template);
			var path = template;

			foreach (Match match in matches)
			{
				var value = GetFieldValue(fields, line, match.Groups[1].Value);
				if (value == null) continue;
				path = path.Replace($"{match.Value}", value);
			}

			return path;
		}
		
		private static string? GetFieldValue(IEnumerable<string>? fields, IEnumerable<string>? line, string name)
		{
			var index = fields
				?.Select((f, i) => (f, i: (int?)i))
				.FirstOrDefault(
					result => string.Equals(result.f, name, StringComparison.CurrentCultureIgnoreCase),
					("", null)
				).i;

			if (index == null)
			{
				Log.Warning("Не найдено поле {Name} среди полей даных", name);
				return null;
			}

			return line?.ToArray()[index ?? 0];
		}
		
		private static void PrintStatistic(IEnumerable<SearchResultDTO> results)
		{
			var foundedFiles = results.Count(s => s.FoundFiles.Any());
			var foundedAllFiles = results.Select(s => s.FoundFiles.Count()).Sum();
			var notFoundedFiles = results.Count(s => !s.FoundFiles.Any());
			var founded1File = results.Count(s => s.FoundFiles.Count() == 1);
			var founded2File = results.Count(s => s.FoundFiles.Count() == 2);
			var foundedMoreThan2File = results.Count(s => s.FoundFiles.Count() > 2);

			Log.Information("");
			Log.Information("");
			Log.Information("Всего совпадений {FoundedAllFiles}", foundedAllFiles);
			Log.Information("Всего найдено файлов {FoundedFiles}", foundedFiles);
			Log.Information("Всего не найдено файлов {NotFoundedFiles}", notFoundedFiles);
			Log.Information("Результатов с 1 совпадением {Founded1File}", founded1File);
			Log.Information("Результатов с 2 совпадениями {Founded2File}", founded2File);
			Log.Information("Результатов с 3 и более совпадениями {FoundedMoreThan2File}", foundedMoreThan2File);
		}

		private static string ReplaceIllegalCharacters(string path)
		{
			var regexSearch = new string(Path.GetInvalidPathChars());
			var r = new Regex($"[{Regex.Escape(regexSearch)}\"]");
			return r.Replace(path, "");
		}

		private static string[] FindFiles(string name)
		{
			return _allFiles
				.Where(file => file.Name == name)
				.Select(file => file.Path)
				.ToArray();
		}

		private static string[] GetAllFilesFrom(string dir)
		{
			var files = new List<string>();

			var tempFiles = Array.Empty<string>();

			try
			{
				tempFiles = Directory.GetFiles(dir, "*", System.IO.SearchOption.TopDirectoryOnly);
			}
			catch
			{
			}

			files.AddRange(tempFiles);

			var tempDirs = Array.Empty<string>();

			try
			{
				tempDirs = Directory.GetDirectories(dir, "*", System.IO.SearchOption.TopDirectoryOnly);
			}
			catch
			{
			}

			foreach (var childDir in tempDirs)
				files.AddRange(GetAllFilesFrom(childDir));

			return files.ToArray();
		}
	}
}