using Microsoft.Extensions.Logging;

namespace Project.Core.Log;

public class SerilogProvider : ILogProvider
{
    private readonly ILogger<SerilogProvider> _logger;

    public SerilogProvider(ILogger<SerilogProvider> logger)
    {
        _logger = logger;
    }

    public void LogInformation(string whatToLog)
        => _logger.LogInformation("{Message}", whatToLog);

    public void LogError(Exception ex, string message)
        => _logger.LogError(ex, "{Message}", message);
}
