using System.Collections.Generic;

namespace FilesFinder.Conf;

public class FilesetsConfiguration
{
	public IEnumerable<Fileset> Filesets { get; set; }
}

// ReSharper disable once ClassNeverInstantiated.Global
public class Fileset
{
	public string Id { get; set; }
	public string TableName { get; set; }
	public string Host { get; set; } // должен существовать
	public string StaticPath { get; set; } // Создаст сам
	// public string Type; // ftp/filepath
}
