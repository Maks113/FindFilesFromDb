namespace FilesFinder;

public class SearchResultDTO
{
	public string ResultString { get; set; }
	public IEnumerable<string> FoundFiles { get; set; }
	
}