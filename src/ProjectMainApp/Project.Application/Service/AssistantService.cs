using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Project.Application.Service.Defination;
using Project.Core;
using Project.Object.Requests;

namespace Project.Application.Service
{
    /// <summary>
    /// Storefront shopping assistant. Streams a reply, and gives the model a
    /// <c>SearchProducts</c> tool (backed by <see cref="IProductEmbeddingService"/>) so every
    /// recommendation is grounded in a real catalog lookup.
    /// </summary>
    public class AssistantService : IAssistantService
    {
        private const int MaxHistoryTurns = 12;
        private const int MaxContentChars = 4000;

        private const string SystemPrompt =
            "You are the shopping assistant for an online store. Your only job is to help shoppers " +
            "find products in THIS store's catalog.\n" +
            "Rules:\n" +
            "- For anything about products, categories, needs, budgets or gifts: call SearchProducts " +
            "first and recommend ONLY items it returns.\n" +
            "- Never invent products, prices, specifications, stock, shipping or returns. If nothing " +
            "suitable comes back, say so and offer to search more broadly.\n" +
            "- Keep replies short: one or two sentences, then a brief list of up to 5 items as " +
            "\"Name - $Price\".\n" +
            "- Plain text only: no markdown headings or tables.\n" +
            "- If asked about anything other than shopping in this store, briefly decline and steer " +
            "back to products.";

        private readonly IApplicationContext _ctx;
        private readonly IProductEmbeddingService _search;
        private readonly IChatClient? _chat;

        public AssistantService(
            IApplicationContext ctx,
            IProductEmbeddingService search,
            IServiceProvider serviceProvider)
        {
            _ctx = ctx;
            _search = search;
            _chat = serviceProvider.GetService<IChatClient>();
        }

        public IAsyncEnumerable<string> StreamReplyAsync(
            IReadOnlyList<AssistantMessage> messages,
            CancellationToken cancellationToken = default)
        {
            // Validate eagerly - before the caller starts writing the response stream.
            if (_chat is null)
                throw new AiUnavailableException("The assistant is unavailable: Azure OpenAI is not configured.");
            if (messages is null || messages.Count == 0)
                throw new ArgumentException("At least one message is required.");
            if (!string.Equals(messages[^1].Role, "user", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("The last message must be from the user.");

            return StreamCore(messages, cancellationToken);
        }

        private async IAsyncEnumerable<string> StreamCore(
            IReadOnlyList<AssistantMessage> messages,
            [EnumeratorCancellation] CancellationToken ct)
        {
            // Tool the model can call. Captures `ct` and the search service.
            [Description("Search this store's product catalog by natural-language need. Call this " +
                         "for ANY question about products, categories, use-cases, budgets or gift " +
                         "ideas before answering.")]
            async Task<IReadOnlyList<object>> SearchProducts(
                [Description("What the shopper wants, in natural language")] string query,
                [Description("How many results to return, 1-8")] int count = 5)
            {
                var result = await _search.SearchAsync(query, Math.Clamp(count, 1, 8), ct);
                _ctx.Log.LogInformation($"Assistant: SearchProducts('{query}') -> {result.Count} hit(s)");
                return result.Results
                    .Select(r => (object)new { r.Id, r.Name, r.Slug, price = r.Price, r.Score })
                    .ToList();
            }

            var chat = new List<ChatMessage> { new(ChatRole.System, SystemPrompt) };
            foreach (var m in messages.TakeLast(MaxHistoryTurns))
            {
                var content = (m.Content ?? string.Empty).Trim();
                if (content.Length == 0) continue;
                if (content.Length > MaxContentChars) content = content[..MaxContentChars];

                var role = string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                    ? ChatRole.Assistant
                    : ChatRole.User;
                chat.Add(new ChatMessage(role, content));
            }

            var options = new ChatOptions
            {
                Tools = [AIFunctionFactory.Create(SearchProducts)],
                ToolMode = ChatToolMode.Auto,
                MaxOutputTokens = 500,
                Temperature = 0.4f
            };

            _ctx.Log.LogInformation($"Assistant: streaming reply ({chat.Count - 1} history msg(s))");

            await foreach (var update in _chat!.GetStreamingResponseAsync(chat, options, ct))
            {
                if (!string.IsNullOrEmpty(update.Text))
                    yield return update.Text;
            }
        }
    }
}
