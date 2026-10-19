using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Project.Application.Service.Defination;
using Project.Core;
using Project.Object.Requests;
using Project.Object.Responses;

namespace Project.Application.Service
{
    /// <summary>
    /// Turns a product's facts into a short marketing description via <see cref="IChatClient"/>.
    /// Results are cached for an hour keyed by the product's current fields + tone + length, so
    /// repeated clicks on the same unchanged product don't re-spend tokens.
    /// </summary>
    public class ProductCopyService : IProductCopyService
    {
        private const int DefaultMaxWords = 80;
        private static readonly TimeSpan CacheFor = TimeSpan.FromHours(1);

        private readonly IApplicationContext _ctx;
        private readonly IProductService _products;
        private readonly IMemoryCache _cache;
        private readonly IChatClient? _chat;

        public ProductCopyService(
            IApplicationContext ctx,
            IProductService products,
            IMemoryCache cache,
            IServiceProvider serviceProvider)
        {
            _ctx = ctx;
            _products = products;
            _cache = cache; // singleton IMemoryCache - shared across requests (unlike ICacheProvider)
            // Optional dependency: null when AzureOpenAI:Endpoint isn't configured (see AiDependencyGroup).
            _chat = serviceProvider.GetService<IChatClient>();
        }

        public async Task<GenerateProductDescriptionResponse> GenerateDescriptionAsync(
            int productId,
            GenerateProductDescriptionRequest request,
            CancellationToken cancellationToken = default)
        {
            if (_chat is null)
                throw new AiUnavailableException(
                    "Azure OpenAI is not configured for this environment, so descriptions can't be generated.");

            // Throws KeyNotFoundException (-> 404) if the product doesn't exist.
            var product = await _products.GetProductById(productId);

            var tone = string.IsNullOrWhiteSpace(request.Tone) ? "friendly and professional" : request.Tone.Trim();
            var maxWords = Math.Clamp(request.MaxWords ?? DefaultMaxWords, 20, 200);

            var cacheKey = BuildCacheKey(product, tone, maxWords);
            if (_cache.TryGetValue(cacheKey, out GenerateProductDescriptionResponse? hit) && hit is not null)
            {
                _ctx.Log.LogInformation($"ProductCopy: cache hit for product {productId}");
                return Copy(hit, cached: true);
            }

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System,
                    "You are a senior e-commerce copywriter for an online store. " +
                    "Write clear, honest, benefit-focused product descriptions.\n" +
                    "Rules:\n" +
                    "- Use ONLY the facts provided. Never invent specifications, materials, dimensions, " +
                    "certifications, brand claims, or origin.\n" +
                    "- Do not mention price, discounts, shipping, or returns.\n" +
                    $"- One paragraph, {maxWords} words maximum. No headings, no bullet lists, no markdown.\n" +
                    $"- Tone: {tone}.\n" +
                    "- Write in the store's voice; do not overuse \"you\"."),
                new(ChatRole.User,
                    $"Product name: {product.Name}\n" +
                    $"Category: {product.CategoryName ?? "not specified"}\n" +
                    $"In stock: {(product.Stock > 0 ? "yes" : "no")}\n" +
                    "Current description (may be empty or rough, treat as a hint only):\n" +
                    $"\"\"\"\n{product.Description}\n\"\"\"\n\n" +
                    "Write a fresh description.")
            };

            var options = new ChatOptions
            {
                MaxOutputTokens = maxWords * 3, // ~1.3 tokens/word + headroom
                Temperature = 0.7f
            };

            _ctx.Log.LogInformation($"ProductCopy: calling model for product {productId} (tone='{tone}', maxWords={maxWords})");

            ChatResponse response;
            try
            {
                response = await _chat.GetResponseAsync(messages, options, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (LooksLikeContentFilter(ex))
            {
                _ctx.Log.LogError(ex, "ProductCopy: blocked by content filter");
                throw new ArgumentException(
                    "The content safety filter blocked this request. Adjust the product details and try again.");
            }

            var draft = response.Text?.Trim() ?? string.Empty;
            if (draft.Length == 0)
                throw new InvalidOperationException("The model returned an empty description.");

            var result = new GenerateProductDescriptionResponse
            {
                ProductId = productId,
                Draft = draft,
                Model = response.ModelId,
                TokensIn = response.Usage?.InputTokenCount,
                TokensOut = response.Usage?.OutputTokenCount,
                Cached = false
            };

            _cache.Set(cacheKey, result, CacheFor);
            return result;
        }

        private static GenerateProductDescriptionResponse Copy(GenerateProductDescriptionResponse s, bool cached) => new()
        {
            ProductId = s.ProductId,
            Draft = s.Draft,
            Model = s.Model,
            TokensIn = s.TokensIn,
            TokensOut = s.TokensOut,
            Cached = cached
        };

        private static bool LooksLikeContentFilter(Exception ex)
        {
            var m = ex.Message;
            return m.Contains("content_filter", StringComparison.OrdinalIgnoreCase)
                || m.Contains("content management policy", StringComparison.OrdinalIgnoreCase)
                || m.Contains("ResponsibleAIPolicy", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildCacheKey(ProductResponseModel p, string tone, int maxWords)
        {
            var raw = string.Join("|",
                "prodcopy-v1", p.Id, p.Name, p.Description, p.Price, p.Stock,
                p.CategoryName ?? "", tone, maxWords);
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
            return $"prodcopy:{p.Id}:{hash[..16]}";
        }
    }
}
