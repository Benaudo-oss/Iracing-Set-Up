namespace IracingSetupManager.Infrastructure.Logging;

public interface IApplicationLog
{
    void Information(string message);

    void Error(Exception exception, string message);
}

