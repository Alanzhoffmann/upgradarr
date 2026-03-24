using Upgradarr.Domain.Enums;

namespace Upgradarr.Domain.Interfaces;

public interface IHasSource
{
    public RecordSource Source { get; }
}
