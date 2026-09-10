using Project.Object.Responses;

namespace Project.Application.Service.Defination
{
    /// <summary>
    /// Keeps a vector embedding per product and runs semantic search over them (Phase 5).
    /// </summary>
    public interface IProductEmbeddingService
    {
        /// <summary>
        /// Create or refresh the embedding for one product. Best-effort: returns false (does not
        /// throw) when AI is disabled, so it's safe to call from the product save path.
        /// </summary>
        Task<bool> UpsertAsync(int productId, CancellationToken cancellationToken = default);

        /// <summary>Embed every product whose text changed since it was last embedded. Returns the count updated.</summary>
        Task<int> BackfillAsync(CancellationToken cancellationToken = default);

        /// <summary>Embed the query and return the closest active products by cosine similarity.</summary>
        Task<ProductSearchResponse> SearchAsync(string query, int take, CancellationToken cancellationToken = default);
    }
}
