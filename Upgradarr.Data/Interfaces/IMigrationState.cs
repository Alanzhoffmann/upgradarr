namespace Upgradarr.Data.Interfaces;

public interface IMigrationState
{
    bool IsDone { get; set; }
}
