using IracingSetupManager.Core.Setups;

namespace IracingSetupManager.Infrastructure.Database.Entities;

using System.ComponentModel.DataAnnotations.Schema;

public sealed class SetupChangeHistoryEntity
{
    public long Id { get; set; }

    public Guid SetupId { get; set; }

    public required string OriginalFileName { get; set; }

    public required string ChangeType { get; set; }

    public SetupStatus? PreviousStatus { get; set; }

    public SetupStatus? NewStatus { get; set; }

    public int? PreviousRating { get; set; }

    public int? NewRating { get; set; }

    public string? PreviousComment { get; set; }

    public string? NewComment { get; set; }

    public DateTimeOffset ChangedAtUtc { get; set; }

    [NotMapped]
    public string StatusTransitionDisplay => PreviousStatus is null && NewStatus is null
        ? string.Empty
        : $"{PreviousStatus} → {NewStatus}";
}
