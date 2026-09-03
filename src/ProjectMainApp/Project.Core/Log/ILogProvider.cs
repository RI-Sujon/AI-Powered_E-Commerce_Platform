namespace Project.Core.Log;

public interface ILogProvider
{
    void LogInformation(string whatToLog);
    void LogError(Exception ex, string message);
}