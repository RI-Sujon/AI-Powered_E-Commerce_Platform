namespace Project.Object.Responses
{
    /// <summary>
    /// A generated marketing-copy draft for a product. The caller reviews and edits it, then saves
    /// via the normal PUT /api/product/{id} - this endpoint never writes to the database.
    /// </summary>
    public class GenerateProductDescriptionResponse
    {
        public int ProductId { get; set; }

        /// <summary>The generated description. One paragraph, plain text.</summary>
        public required string Draft { get; set; }

        /// <summary>Model that produced it, e.g. "gpt-4.1-mini".</summary>
        public string? Model { get; set; }

        public long? TokensIn { get; set; }
        public long? TokensOut { get; set; }

        /// <summary>True when served from the short-lived in-memory cache (no token spend).</summary>
        public bool Cached { get; set; }
    }
}
