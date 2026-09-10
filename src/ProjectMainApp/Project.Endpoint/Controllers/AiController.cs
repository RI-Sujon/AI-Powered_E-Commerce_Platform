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
        private readonly IApplicationContext _ctx;

        public AiController(IProductCopyService productCopy, IApplicationContext ctx)
        {
            _productCopy = productCopy;
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
    }
}
