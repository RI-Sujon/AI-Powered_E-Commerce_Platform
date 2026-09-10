using Project.Object.Requests;
using Project.Object.Responses;

namespace Project.Application.Service.Defination
{
    /// <summary>
    /// Generates marketing copy for a product using Azure OpenAI. Read-only: it never persists
    /// anything - the admin reviews the draft and saves it through the normal product update.
    /// </summary>
    public interface IProductCopyService
    {
        Task<GenerateProductDescriptionResponse> GenerateDescriptionAsync(
            int productId,
            GenerateProductDescriptionRequest request,
            CancellationToken cancellationToken = default);
    }
}
