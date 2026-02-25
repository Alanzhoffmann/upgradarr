using Huntarr.Net.Clients.Enums;

namespace Huntarr.Net.Clients.Interfaces;

public interface IHasSource
{
    public RecordSource Source { get; }
}
