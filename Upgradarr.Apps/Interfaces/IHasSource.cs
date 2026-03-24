using Upgradarr.Apps.Enums;

namespace Upgradarr.Apps.Interfaces;

public interface IHasSource
{
    public RecordSource Source { get; }
}
