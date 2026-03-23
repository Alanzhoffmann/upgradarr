using Huntarr.Net.Clients.Models;
using Upgradarr.Apps.Enums;

namespace Upgradarr.Apps.Interfaces;

public interface IQueueResource : IHasSource
{
    int Id { get; }
    string? DownloadId { get; }
    string? Title { get; }
    DateTimeOffset? Added { get; }
    DateTimeOffset? EstimatedCompletionTime { get; }
    string? OutputPath { get; }
    string? ErrorMessage { get; }
    int CustomFormatScore { get; }
    QueueStatus Status { get; }
    public IEnumerable<TrackedDownloadStatusMessage>? StatusMessages { get; init; }

    public bool HasErrorMessage(string message)
    {
        if (!string.IsNullOrWhiteSpace(ErrorMessage) && ErrorMessage.Contains(message, StringComparison.InvariantCultureIgnoreCase))
        {
            return true;
        }

        if (StatusMessages is null || !StatusMessages.Any())
        {
            return false;
        }

        foreach (var statusMessage in StatusMessages)
        {
            if (statusMessage.Messages is null || statusMessage.Messages.Length == 0)
            {
                continue;
            }

            foreach (var msg in statusMessage.Messages)
            {
                if (msg is not null && msg.Contains(message, StringComparison.InvariantCultureIgnoreCase))
                {
                    return true;
                }
            }
        }
        return false;
    }
}
