using System.Collections.Generic;

namespace ConfigurationManager.Conf;

public class PathMapConfiguration
{
	public IEnumerable<PathMapRule> Rules { get; set; }
}

public class PathMapRule
{
	public string From { get; set; }
	public string To { get; set; }
}