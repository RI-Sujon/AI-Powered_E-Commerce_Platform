using Pgvector;

namespace Project.Object.Entities
{
    /// <summary>
    /// The vector representation of a product's searchable text (name + category + description).
    /// One row per product; created/refreshed whenever the product changes or via the backfill
    /// endpoint. <see cref="SourceHash"/> lets us skip re-embedding text that hasn't changed.
    /// </summary>
    public class ProductEmbeddingEntity
    {
        /// <summary>PK and FK to Products.Id (cascade delete).</summary>
        public int ProductId { get; set; }

        /// <summary>1536-dimension embedding (text-embedding-3-small). Null until first generated.</summary>
        public Vector? Embedding { get; set; }

        /// <summary>SHA-256 (hex) of the exact text that was embedded.</summary>
        public string SourceHash { get; set; } = string.Empty;

        /// <summary>Embedding model/deployment used, for traceability.</summary>
        public string Model { get; set; } = string.Empty;

        public DateTime UpdatedAt { get; set; }
    }
}
