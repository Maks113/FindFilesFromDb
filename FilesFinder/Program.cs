using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FilesFinder.Conf;
using Serilog;
using Serilog.Exceptions;

namespace FilesFinder
{
	// ReSharper disable once ClassNeverInstantiated.Global
	// ReSharper disable once ArrangeTypeModifiers
	class Program
	{
		static void Main(string[] args)
		{
			LoggerInit();
			try
			{
				Run().Wait();
			}
			catch (Exception e)
			{
				Log.Error(e, "Ошибка во время выполнения");
			}
			Console.ReadLine();
		}

		private static async Task Run()
		{
			Log.Information("==== Старт задачи поиска файлов ====");
			var configuration = Configuration.GetFinderConfiguration();
			foreach (var target in configuration.Targets)
			{
				Log.Information("Поиск файлов в таблице {TableName} в поле {PathField}", target.TableName, target.PathField);

				var data = await GetRows(target);
				await CheckFiles(data, target);
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
			Log.Debug("Запрос - {Query}", query);
			var result = await SqlCmdUtil.RunRawSql(query);
			Log.Debug("Выбрано строк - {Count}", result.Rows.Count);
			return result;
		}


		private static async Task CheckFiles(DataTable data, FinderTargetConfiguration target)
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

			await SaveToTable(target.TableName, target.PathField, results);

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
				Log.Information("{StatLine}",statLine);
				await searchLog.WriteLineAsync(statLine);
				await foundLog.WriteLineAsync(statLine);
			}
		}

		private static async Task SaveToTable(string tableName, string fieldName, IEnumerable<SearchFileResult> results)
		{
			var regexp = new Regex("[][ ]");
			tableName = regexp.Replace(tableName, "");
			fieldName = regexp.Replace(fieldName, "");
			var name = $@"del_FileSearchResults_{tableName}_{fieldName}";
			var query = Queries.RecreateResultsTable(name);
			Log.Debug("Пересоздание таблицы - {Query}", query);

			await SqlCmdUtil.RunRawSql(query);


			var chunks = results
				.Select((x, i) => new { Index = i, Value = x })
				.GroupBy(x => x.Index / 500)
				.Select(x => x.Select(v => v.Value).ToList());

			foreach (var chunk in chunks)
			{
				query = Queries.InsertResults(name, chunk);
				Log.Debug("Вставка данных - {Query}", query);
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

		private static void LoggerInit()
		{
			Log.Logger = new LoggerConfiguration()
				.Enrich.WithExceptionDetails()
				.ReadFrom.Configuration(Configuration.GetAppConfiguration())
				.CreateLogger();
		}
	}
}