using Project.Core;
using Project.Core.Caching;
using Project.Object.Entities;
using Project.Object.Responses;
using Project.Object.Requests;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Project.Application.Provider.Product.Query
{
    public class GetProductListQuery
    {
        private readonly IApplicationContext _applicationContext;
        private readonly AppDbContext _dbContext;
        private readonly ICacheProvider _cache;

        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        public GetProductListQuery(IApplicationContext applicationContext, AppDbContext dbContext, ICacheProvider cache)
        {
            _applicationContext = applicationContext;
            _dbContext = dbContext;
            _cache = cache;
        }

        public async Task<GetProductListResponse> GetProductList(GetProductListRequest request)
        {
            // Cache key encodes all filter params so different searches get separate entries
            var cacheKey = $"product-list:{request.SearchText}:{request.MinPrice}:{request.MaxPrice}:{request.IsActive}:{request.CategoryId}:{request.SortBy}:{request.Page}:{request.PageSize}";

            var cached = _cache.Get(cacheKey);
            if (cached is GetProductListResponse cachedResponse)
                return cachedResponse;

            var query = _dbContext.Products.AsQueryable();

            if (!string.IsNullOrWhiteSpace(request.SearchText))
            {
                query = query.Where(p => p.Name.Contains(request.SearchText) ||
                                        p.Description.Contains(request.SearchText) ||
                                        p.Slug.Contains(request.SearchText));
            }
            if (request.MinPrice.HasValue)
                query = query.Where(p => p.Price >= request.MinPrice.Value);

            if (request.MaxPrice.HasValue)
                query = query.Where(p => p.Price <= request.MaxPrice.Value);

            if (request.IsActive.HasValue)
                query = query.Where(p => p.IsActive == request.IsActive.Value);

            if (request.CategoryId.HasValue)
                query = query.Where(p => p.CategoryId == request.CategoryId.Value);

            IQueryable<ProductEntity> sortedQuery = request.SortBy switch
            {
                "price_asc"  => query.OrderBy(p => p.Price),
                "price_desc" => query.OrderByDescending(p => p.Price),
                "name_asc"   => query.OrderBy(p => p.Name),
                "name_desc"  => query.OrderByDescending(p => p.Name),
                _            => query.OrderByDescending(p => p.Id)
            };

            var response = new GetProductListResponse
            {
                Page = request.Page,
                PageSize = request.PageSize,
                TotalCount = await query.CountAsync()
            };

            var page = request.Page ?? 1;
            var pageSize = request.PageSize ?? 10;

            response.Products = await sortedQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ProductResponseModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    Slug = p.Slug,
                    Price = p.Price,
                    Stock = p.Stock,
                    IsActive = p.IsActive,
                    CategoryId = p.CategoryId,
                    DiscountStartDate = p.DiscountStartDate,
                    DiscountEndDate = p.DiscountEndDate
                })
                .ToListAsync();

            _cache.Set(cacheKey, response, CacheDuration);
            return response;
        }
    }
}