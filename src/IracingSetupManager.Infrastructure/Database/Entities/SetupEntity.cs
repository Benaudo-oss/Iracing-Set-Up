using IracingSetupManager.Core.Setups;
using System.ComponentModel.DataAnnotations.Schema;

namespace IracingSetupManager.Infrastructure.Database.Entities;

public sealed class SetupEntity
{
    public Guid Id { get; set; }

    public required string OriginalFileName { get; set; }

    public required string Provider { get; set; }

    public required string Category { get; set; }

    public required string Car { get; set; }

    public required string Track { get; set; }

    public string? TrackConfiguration { get; set; }

    public string? Season { get; set; }

    public int? Week { get; set; }

    public SetupWeekKind WeekKind { get; set; } = SetupWeekKind.Unknown;

    [NotMapped]
    public string WeekDisplay => SetupWeekPresentation.Display(Week, WeekKind);

    public required string SetupType { get; set; }

    public long SizeInBytes { get; set; }

    public required string Sha256 { get; set; }

    public required string ArchivePath { get; set; }

    public SetupSourceKind SourceKind { get; set; } = SetupSourceKind.Unknown;

    public string? SourcePath { get; set; }

    public SetupStatus Status { get; set; } = SetupStatus.AVerifier;

    [NotMapped]
    public string StatusDisplay => Status switch
    {
        SetupStatus.AVerifier => "À vérifier",
        SetupStatus.FichierManquant => "Fichier manquant",
        _ => Status.ToString()
    };

    public int? PersonalRating { get; set; }

    public string? Comment { get; set; }

    public DateTimeOffset DownloadedAtUtc { get; set; }

    public long DownloadedAtUtcSortKey { get; set; }

    public DateTimeOffset? LastCopiedToIracingAtUtc { get; set; }

    public int IracingCopyCount { get; set; }

    public DateTimeOffset? LastCopiedToIracingTeamAtUtc { get; set; }

    public int IracingTeamCopyCount { get; set; }

}
