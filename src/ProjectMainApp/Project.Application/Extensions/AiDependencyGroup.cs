using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Project.Application.Extensions;

/// <summary>
/// Registers the Azure OpenAI-backed <see cref="IChatClient"/> and
/// <see cref="IEmbeddingGenerator{TInput,TEmbedding}"/> that the AI features build on
/// (Phase 4 product copy, Phase 5 semantic search, Phase 6 shopping assistant).
///
/// <para>
/// Auth is keyless. <see cref="DefaultAzureCredential"/> uses the API container app's
/// user-assigned managed identity in Azure (selected via the <c>AZURE_CLIENT_ID</c> env var that
/// Terraform sets) and transparently falls back to <c>az login</c> when running locally.
/// </para>
///
/// <para>Configuration (Terraform injects these as <c>AzureOpenAI__*</c> env vars - see
/// <c>terraform/environments/*/main.tf</c>):</para>
/// <list type="bullet">
///   <item><c>AzureOpenAI:Endpoint</c> - https://&lt;name&gt;.openai.azure.com/ (required to enable AI)</item>
///   <item><c>AzureOpenAI:ChatDeployment</c> - deployment name, default <c>chat</c></item>
///   <item><c>AzureOpenAI:EmbeddingDeployment</c> - deployment name, default <c>embeddings</c></item>
/// </list>
/// </summary>
public static class AiDependencyGroup
{
    public static IServiceCollection AddAiDependencyGroup(this IServiceCollection services, IConfiguration config)
    {
        var endpoint = config["AzureOpenAI:Endpoint"];

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            // AI is optional: if the endpoint isn't configured (e.g. a bare local run) the app
            // still boots. Features that need AI report it - see GET /ai/selftest.
            Console.WriteLine("i  AzureOpenAI:Endpoint not configured - AI features disabled.");
            return services;
        }

        var chatDeployment = config["AzureOpenAI:ChatDeployment"] ?? "chat";
        var embeddingDeployment = config["AzureOpenAI:EmbeddingDeployment"] ?? "embeddings";

        // AzureOpenAIClient is thread-safe; register the derived clients as singletons.
        var azureOpenAi = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential());

        services.AddChatClient(azureOpenAi.GetChatClient(chatDeployment).AsIChatClient())
                .UseLogging();

        services.AddEmbeddingGenerator(
            azureOpenAi.GetEmbeddingClient(embeddingDeployment).AsIEmbeddingGenerator());

        Console.WriteLine($"AI: Azure OpenAI wired - {endpoint} (chat={chatDeployment}, embeddings={embeddingDeployment})");
        return services;
    }
}
