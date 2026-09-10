using Project.Application.Service.Defination;
using Project.Object;
using Project.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Project.Object.Responses;
using Project.Object.Requests;
using Project.Application.Provider.Product;

namespace Project.Application.Service
{
    public class ProductService : IProductService
    {
        private readonly IProductProvider _productProvider;
        private readonly IApplicationContext _applicationContext;
        private readonly IProductEmbeddingService _embeddings;

        public ProductService(
            IApplicationContext applicationContext,
            IProductProvider productProvider,
            IProductEmbeddingService embeddings)
        {
            _productProvider = productProvider;
            _applicationContext = applicationContext;
            _embeddings = embeddings;
        }

        public async Task<ProductResponseModel> AddProduct(ProductRequestModel product)
        {
            _applicationContext.Log.LogInformation($"Going to execute _productProvider.AddProduct({product.Name})");
            var result = await _productProvider.AddProduct(product);
            _applicationContext.Log.LogInformation($"Completed _productProvider.AddProduct({product.Name})");
            await RefreshEmbeddingBestEffort(result.Id);
            return result;
        }

        public async Task<GetProductListResponse> GetProductList(GetProductListRequest request)
        {
            _applicationContext.Log.LogInformation("GetProductList in service");
            return await _productProvider.GetProductList(request);
        }

        public async Task<ProductResponseModel> GetProductById(int id)
        {
            _applicationContext.Log.LogInformation($"GetProductById: {id}");
            return await _productProvider.GetProductById(id);
        }

        public async Task<ProductResponseModel> UpdateProduct(int id, ProductRequestModel product)
        {
            _applicationContext.Log.LogInformation($"UpdateProduct: {id}");
            var result = await _productProvider.UpdateProduct(id, product);
            await RefreshEmbeddingBestEffort(id);
            return result;
        }

        public async Task DeleteProduct(int id)
        {
            _applicationContext.Log.LogInformation($"DeleteProduct: {id}");
            await _productProvider.DeleteProduct(id);
            // The ProductEmbeddings row is removed by the cascade FK - nothing to do here.
        }

        // Semantic-search embeddings must never block a catalog edit: if Azure OpenAI is down or
        // unconfigured, log and move on. The backfill endpoint is the catch-up path.
        private async Task RefreshEmbeddingBestEffort(int productId)
        {
            try
            {
                await _embeddings.UpsertAsync(productId);
            }
            catch (Exception ex)
            {
                _applicationContext.Log.LogError(ex, $"Embedding refresh failed for product {productId}");
            }
        }
    }
}
