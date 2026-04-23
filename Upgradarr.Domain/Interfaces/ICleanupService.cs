namespace Upgradarr.Domain.Interfaces;

public interface ICleanupService
{
    Task PerformCleanupAsync(CancellationToken cancellationToken = default);
}
