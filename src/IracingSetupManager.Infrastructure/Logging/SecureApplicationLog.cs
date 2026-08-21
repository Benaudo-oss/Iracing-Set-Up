using Serilog;

namespace IracingSetupManager.Infrastructure.Logging;

public sealed class SecureApplicationLog(ILogger logger) : IApplicationLog
{
    public void Information(string message) => logger.Information("{Message}", SensitiveDataRedactor.Redact(message));
    public void Error(Exception exception, string message) =>
        logger.Error(
            "{Message}{NewLine}{Diagnostic}",
            SensitiveDataRedactor.Redact(message),
            Environment.NewLine,
            SensitiveDataRedactor.Redact(exception.ToString()));
}
