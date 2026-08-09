using Serilog;

namespace IracingSetupManager.Infrastructure.Logging;

public sealed class SecureApplicationLog(ILogger logger) : IApplicationLog
{
    public void Information(string message) => logger.Information("{Message}", SensitiveDataRedactor.Redact(message));
    public void Error(Exception exception, string message) =>
        logger.Error("{Message}; erreur={ErrorType}", SensitiveDataRedactor.Redact(message), exception.GetType().Name);
}
