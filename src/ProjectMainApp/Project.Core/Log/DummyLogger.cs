namespace Project.Core.Log;

public class DummyLogger : ILogProvider
{
    public void LogInformation(string whatToLog)
    {
        Console.WriteLine(whatToLog);
    }

    public void LogError(Exception ex, string message)
    {
        Console.WriteLine($"Exception occurred - {message} - {ex.Message} - {ex.StackTrace}");
    }
}