using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using Project.Application.Service.Defination;
using Project.Core;
using Project.Object.Entities;
using Project.Object.Responses;

namespace Project.Application.Service
{
    /// <summary>
    /// Maintains one <see cref="ProductEmbeddingEntity"/> per product and answers semantic
    /// search queries with a pgvector cosine-distance ordering.
    /// </summary>
    public class ProductEmbeddingService : IProductEmbeddingService
    {
        private readonly AppDbContext _db;
        private readonly IApplicationContext _ctx;
        private readonly string _model;
        private readonly IEmbeddingGenerator<string, Embedding<float>>? _embeddings;

        public ProductEmbeddingService(
            AppDbContext db,
            IApplicationContext ctx,
            IConfiguration config,
            IServiceProvider serviceProvider)
        {
            _db = db;
            _ctx = ctx;
            _model = config["AzureOpenAI:EmbeddingDeployment"] ?? "embeddings";
            _embeddings = serviceProvider.GetService<IEmbeddingGenerator<string, Embedding<float>>>();
        }

        public async Task<bool> UpsertAsync(int productId, CancellationToken cancellationToken = default)
        {
            if (_embeddings is null) return false;

            // Two simple queries rather than composing a filter onto the left-join projection -
            // EF Core can't always translate `groupjoin.DefaultIfEmpty().Where(...).First()`.
            var product = await _db.Products
                .FirstOrDefaultAsync(x => x.Id == productId, cancellationToken);
            if (product is null) return false;

            var categoryName = product.CategoryId is int categoryId
                ? await _db.Categories.Where(c => c.Id == categoryId).Select(c => c.Name)
                    .FirstOrDefaultAsync(cancellationToken)
                : null;

            var row = new ProductTextRow(product.Id, product.Name, product.Description, categoryName);
            var text = BuildText(row);
            var hash = Hash(text);

            var existing = await _db.ProductEmbeddings.FindAsync(new object[] { productId }, cancellationToken);
            if (existing is { Embedding: not null } && existing.SourceHash == hash)
                return true; // text unchanged - nothing to spend tokens on

            var vector = new Vector(await _embeddings.GenerateVectorAsync(text, cancellationToken: cancellationToken));
            Apply(existing, productId, vector, hash);
            await _db.SaveChangesAsync(cancellationToken);

            _ctx.Log.LogInformation($"ProductEmbedding: upserted product {productId}");
            return true;
        }

        public async Task<int> BackfillAsync(CancellationToken cancellationToken = default)
        {
            if (_embeddings is null) return 0;

            var rows = await ProductTextQuery().ToListAsync(cancellationToken);
            var currentHashes = await _db.ProductEmbeddings
                .ToDictionaryAsync(e => e.ProductId, e => e.SourceHash, cancellationToken);

            var stale = rows
                .Select(r => (row: r, text: BuildText(r)))
                .Where(x => !currentHashes.TryGetValue(x.row.Id, out var h) || h != Hash(x.text))
                .ToList();

            if (stale.Count == 0)
            {
                _ctx.Log.LogInformation("ProductEmbedding: backfill - nothing stale");
                return 0;
            }

            var generated = await _embeddings.GenerateAsync(stale.Select(x => x.text), cancellationToken: cancellationToken);

            for (var i = 0; i < stale.Count; i++)
            {
                var (row, text) = stale[i];
                var existing = await _db.ProductEmbeddings.FindAsync(new object[] { row.Id }, cancellationToken);
                Apply(existing, row.Id, new Vector(generated[i].Vector), Hash(text));
            }

            await _db.SaveChangesAsync(cancellationToken);
            _ctx.Log.LogInformation($"ProductEmbedding: backfill embedded {stale.Count} product(s)");
            return stale.Count;
        }

        public async Task<ProductSearchResponse> SearchAsync(string query, int take, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentException("Search query must not be empty.");
            if (_embeddings is null)
                throw new AiUnavailableException("Semantic search is unavailable: Azure OpenAI is not configured.");

            take = Math.Clamp(take, 1, 50);
            var queryVector = new Vector(await _embeddings.GenerateVectorAsync(query, cancellationToken: cancellationToken));

            var hits = await (
                    from e in _db.ProductEmbeddings
                    join p in _db.Products on e.ProductId equals p.Id
                    where p.IsActive && e.Embedding != null
                    orderby e.Embedding!.CosineDistance(queryVector)
                    select new
                    {
                        p.Id,
                        p.Name,
                        p.Slug,
                        p.Price,
                        p.Stock,
                        p.CategoryId,
                        p.DiscountStartDate,
                        p.DiscountEndDate,
                        Distance = e.Embedding!.CosineDistance(queryVector)
                    })
                .Take(take)
                .ToListAsync(cancellationToken);

            return new ProductSearchResponse
            {
                Query = query,
                Count = hits.Count,
                // Same shape as ProductResponseModel (+ Score) so the storefront can render these
                // hits through the existing ItemCard component unchanged.
                Results = hits.Select(h => new ProductSearchItem
                {
                    Id = h.Id,
                    Name = h.Name,
                    Slug = h.Slug,
                    Price = h.Price,
                    Stock = h.Stock,
                    CategoryId = h.CategoryId,
                    DiscountStartDate = h.DiscountStartDate,
                    DiscountEndDate = h.DiscountEndDate,
                    Score = Math.Round(1.0 - h.Distance, 4) // cosine distance -> similarity
                }).ToList()
            };
        }

        // --- helpers ------------------------------------------------------------------------

        private record ProductTextRow(int Id, string Name, string Description, string? CategoryName);

        private IQueryable<ProductTextRow> ProductTextQuery() =>
            from p in _db.Products
            join c in _db.Categories on p.CategoryId equals c.Id into cj
            from c in cj.DefaultIfEmpty()
            select new ProductTextRow(p.Id, p.Name, p.Description, c != null ? c.Name : null);

        private static string BuildText(ProductTextRow r) =>
            $"{r.Name}\nCategory: {r.CategoryName ?? "none"}\n{r.Description}";

        private void Apply(ProductEmbeddingEntity? existing, int productId, Vector vector, string hash)
        {
            if (existing is null)
            {
                _db.ProductEmbeddings.Add(new ProductEmbeddingEntity
                {
                    ProductId = productId,
                    Embedding = vector,
                    SourceHash = hash,
                    Model = _model,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                existing.Embedding = vector;
                existing.SourceHash = hash;
                existing.Model = _model;
                existing.UpdatedAt = DateTime.UtcNow;
            }
        }

        private static string Hash(string text) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }
}
