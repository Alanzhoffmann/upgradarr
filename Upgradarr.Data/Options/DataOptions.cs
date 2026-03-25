namespace Upgradarr.Data.Options;

public class DataOptions
{
    public const string SectionName = "Data";

    public string ConnectionString { get; set; } = "Data Source=/config/app.db;Cache=Shared";
}
