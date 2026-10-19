using System.Text.Json;
using Boooks.Net.Endpoint.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project.Application.Service.Defination;
using Project.Core;
using Project.Object;
using Project.Object.Requests;
using Project.Object.Responses;

namespace Project.Endpoint.Controllers
{
    /// <summary>
    /// Admin-only AI helpers. These endpoints call Azure OpenAI and never write to the database -
    /// the admin reviews the output and saves it through the normal product endpoints.
    /// </summary>
    [ApiController]
    [Route("api/ai")]
    [Authorize(Roles = "Admin")]
    public class AiController : BaseController
    {
        private readonly IProductCopyService _productCopy;
        private readonly IProductEmbeddingService _productEmbeddings;
        private readonly IAssistantService _assistant;
        private readonly IApplicationContext _ctx;

        public AiController(
            IProductCopyService productCopy,
            IProductEmbeddingService productEmbeddings,
            IAssistantService assistant,
            IApplicationContext ctx)
        {
            _productCopy = productCopy;
            _productEmbeddings = productEmbeddings;
            _assistant = assistant;
            _ctx = ctx;
        }

        /// <summary>
        /// Generate a marketing-copy draft for an existing product. Returns the draft only;
        /// the admin edits it and saves via PUT /api/product/{id}.
        /// </summary>
        [HttpPost("products/{id:int}/generate-description")]
        public async Task<ActionResult> GenerateDescription(
            int id,
            [FromBody] GenerateProductDescriptionRequest? request,
            CancellationToken cancellationToken)
        {
            _ctx.Log.LogInformation($"AI generate-description requested for product {id}");
            var result = await _productCopy.GenerateDescriptionAsync(id, request ?? new(), cancellationToken);
            return Ok(new ResponseModel<GenerateProductDescriptionResponse> { IsSuccess = true, Data = result });
        }

        /// <summary>
        /// Semantic product search. Anonymous - this is a storefront feature. The query is
        /// embedded and products are ranked by cosine similarity to it.
        /// </summary>
        [AllowAnonymous]
        [HttpGet("products/search")]
        public async Task<ActionResult> SearchProducts(
            [FromQuery] string q,
            [FromQuery] int take = 10,
            CancellationToken cancellationToken = default)
        {
            _ctx.Log.LogInformation($"AI product search: '{q}' (take {take})");
            var result = await _productEmbeddings.SearchAsync(q, take, cancellationToken);
            return Ok(new ResponseModel<ProductSearchResponse> { IsSuccess = true, Data = result });
        }

        /// <summary>
        /// (Re)build embeddings for every product whose text changed since it was last embedded.
        /// Product create/update already refresh their own embedding; this is the catch-up path
        /// (e.g. after importing a catalog, or after an Azure OpenAI outage).
        /// </summary>
        [HttpPost("products/embeddings/backfill")]
        public async Task<ActionResult> BackfillEmbeddings(CancellationToken cancellationToken)
        {
            var updated = await _productEmbeddings.BackfillAsync(cancellationToken);
            return Ok(new ResponseModel<object> { IsSuccess = true, Data = new { updated } });
        }

        /// <summary>
        /// Shopping-assistant chat. Anonymous, catalog-grounded product discovery. Streams the
        /// reply as Server-Sent Events: one <c>data: "&lt;text delta&gt;"</c> frame per chunk, then
        /// <c>data: [DONE]</c>. Errors arrive as an <c>event: error</c> frame.
        /// </summary>
        [AllowAnonymous]
        [HttpPost("assistant/chat")]
        public async Task ChatAssistant([FromBody] AssistantChatRequest request, CancellationToken cancellationToken)
        {
            Response.Headers.CacheControl = "no-cache";
            Response.Headers["X-Accel-Buffering"] = "no";
            Response.ContentType = "text/event-stream";

            try
            {
                await foreach (var delta in _assistant.StreamReplyAsync(request?.Messages ?? new(), cancellationToken))
                {
                    await Response.WriteAsync($"data: {JsonSerializer.Serialize(delta)}\n\n", cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // client disconnected - nothing to do
            }
            catch (Exception ex)
            {
                _ctx.Log.LogError(ex, "Assistant chat stream failed");
                var message = ex is AiUnavailableException or ArgumentException
                    ? ex.Message
                    : "The assistant is unavailable right now.";
                await Response.WriteAsync($"event: error\ndata: {JsonSerializer.Serialize(message)}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }

            await Response.WriteAsync("data: [DONE]\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }
}
