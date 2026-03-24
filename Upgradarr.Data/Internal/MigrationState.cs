using Upgradarr.Data.Interfaces;

namespace Upgradarr.Data.Internal;

public class MigrationState : IMigrationState
{
    public bool IsDone { get; set; }
}
