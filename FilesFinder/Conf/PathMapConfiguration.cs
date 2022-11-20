using System.Collections.Generic;

namespace FilesFinder.Conf;

public class PathMapConfiguration
{
	public IEnumerable<PathMapRule> Rules { get; set; }
}

public class PathMapRule
{
	public string From { get; set; }
	public string To { get; set; }
}