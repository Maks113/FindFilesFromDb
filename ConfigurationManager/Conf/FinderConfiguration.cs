namespace ConfigurationManager.Conf
{
	public class FinderConfiguration
	{
		public FinderTargetConfiguration[] Targets { get; set; }
	}

	public class FinderTargetConfiguration
	{
		public string TableName { get; set; }
		public string PathField { get; set; }
		public string IdField { get; set; }
		public string FilesetId { get; set; }
		public string FilesetFieldName { get; set; }
		public FinderTargetField[] InfoFields { get; set; }
	}

	public class FinderTargetField
	{
		public string FieldName { get; set; }
		public string Description { get; set; }
	}
}

