namespace Project.Object.Responses
{
    /// <summary>Result of a semantic product search (Phase 5).</summary>
    public class ProductSearchResponse
    {
        public required string Query { get; set; }
        public int Count { get; set; }
        public List<ProductSearchItem> Results { get; set; } = new();
    }

    public class ProductSearchItem
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Slug { get; set; }
        public decimal Price { get; set; }
        public int? CategoryId { get; set; }

        /// <summary>Cosine similarity 0..1 (1 = closest). Derived from the pgvector cosine distance.</summary>
        public double Score { get; set; }
    }
}
