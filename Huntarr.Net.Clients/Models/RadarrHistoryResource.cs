namespace Huntarr.Net.Clients.Models;

public class RadarrHistoryResource
{
    public int Id { get; set; }
    public int MovieId { get; set; }
    public string? SourceTitle { get; set; }
    public IList<Language>? Languages { get; set; }
    public QualityModel? Quality { get; set; }
    public IList<CustomFormatResource>? CustomFormats { get; set; }
    public int CustomFormatScore { get; set; }
    public bool QualityCutoffNotMet { get; set; }
    public DateTime Date { get; set; }
    public string? DownloadId { get; set; }
    public string? EventType { get; set; }
    public Dictionary<string, string?>? Data { get; set; }
    public MovieResource? Movie { get; set; }
}
