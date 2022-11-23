using System;

namespace ConfigurationManager;

public class FileInfoDto
{
	public string FilesetId { get; set; }
	public string Path { get; set; }
	public string FullPath { get; set; }
	public DateTime CreationDate { get; set; }
	public int UserId { get; set; }
	public long Size { get; set; }
	public string Name { get; set; }
}