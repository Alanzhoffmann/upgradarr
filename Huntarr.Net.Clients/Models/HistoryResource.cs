namespace Huntarr.Net.Clients.Models;

public class HistoryResource
{
    public int Id { get; set; }
    public int EpisodeId { get; set; }
    public int SeriesId { get; set; }
    public string? SourceTitle { get; set; }
    public IList<Language>? Languages { get; set; }
    public QualityModel? Quality { get; set; }
    public int CustomFormatScore { get; set; }
    public bool QualityCutoffNotMet { get; set; }
    public DateTime Date { get; set; }
    public string? DownloadId { get; set; }
    public string? EventType { get; set; }
    public Dictionary<string, string?>? Data { get; set; }
    public EpisodeResource? Episode { get; set; }
    public SeriesResource? Series { get; set; }
}
