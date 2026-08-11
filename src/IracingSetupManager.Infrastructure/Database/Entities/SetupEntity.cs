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

    public required string SetupType { get; set; }

    public long SizeInBytes { get; set; }

    public required string Sha256 { get; set; }

    public required string ArchivePath { get; set; }

    public SetupSourceKind SourceKind { get; set; } = SetupSourceKind.Unknown;

    public string? SourcePath { get; set; }

    public bool IsPrivate { get; set; }

    public bool Garage61ExportApproved { get; set; }

    public SetupStatus Status { get; set; } = SetupStatus.Nouveau;

    [NotMapped]
    public string StatusDisplay => Status switch
    {
        SetupStatus.AVerifier => "À vérifier",
        SetupStatus.ACorriger => "À corriger",
        SetupStatus.EnvoyeVersGarage61 => "Envoyé vers Garage61",
        SetupStatus.ErreurEnvoi => "Erreur d’envoi",
        SetupStatus.FichierManquant => "Fichier manquant",
        _ => Status.ToString()
    };

    public int? PersonalRating { get; set; }

    public string? Comment { get; set; }

    public DateTimeOffset DownloadedAtUtc { get; set; }

    public DateTimeOffset? SentToGarage61AtUtc { get; set; }

    public DateTimeOffset? LastCopiedToIracingAtUtc { get; set; }

    public int IracingCopyCount { get; set; }

    public DateTimeOffset? LastCopiedToIracingTeamAtUtc { get; set; }

    public int IracingTeamCopyCount { get; set; }

    public bool? Garage61Succeeded { get; set; }

    public string? Garage61Result { get; set; }

    public string? Garage61SetupId { get; set; }

    public string? Garage61SetupUrl { get; set; }
}
