using System.Security.Cryptography;

namespace IracingSetupManager.Infrastructure.Files;

public sealed class Sha256Calculator
{
    public async Task<string> CalculateAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

