using Project.Object.Requests;

namespace Project.Application.Service.Defination
{
    /// <summary>
    /// The storefront shopping assistant (Phase 6). Anonymous, catalog-grounded product discovery:
    /// the model is given a <c>search_products</c> tool backed by semantic search and may only
    /// recommend items it returns.
    /// </summary>
    public interface IAssistantService
    {
        /// <summary>
        /// Stream the assistant's reply to the given conversation as plain-text deltas.
        /// Throws before the first item if the request is invalid or AI is not configured.
        /// </summary>
        IAsyncEnumerable<string> StreamReplyAsync(
            IReadOnlyList<AssistantMessage> messages,
            CancellationToken cancellationToken = default);
    }
}
