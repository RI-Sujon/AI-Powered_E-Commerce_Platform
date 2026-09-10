namespace Project.Core;

/// <summary>
/// Thrown when an AI-backed feature is invoked but Azure OpenAI is not configured
/// (no <c>AzureOpenAI:Endpoint</c>). The API's ExceptionMiddleware maps this to HTTP 503.
/// </summary>
public class AiUnavailableException : Exception
{
    public AiUnavailableException(string message) : base(message) { }
}
