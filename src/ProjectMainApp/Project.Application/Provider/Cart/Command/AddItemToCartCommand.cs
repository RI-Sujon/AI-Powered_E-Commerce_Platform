using Project.Core;
using Project.Object.Entities;
using Project.Object.Requests;
using Project.Object.Responses;
using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Project.Application.Provider.Cart.Command
{
    public class AddItemToCartCommand
    {
        private readonly IApplicationContext _applicationContext;
        private readonly AppDbContext _dbContext;

        public AddItemToCartCommand(IApplicationContext applicationContext, AppDbContext dbContext)
        {
            _applicationContext = applicationContext;
            _dbContext = dbContext;
        }

        public async Task<CartResponseModel> AddItemToCart(CartRequestModel request, int userId)
        {
            _applicationContext.Log.LogInformation("Going to execute - AddItemToCartCommand");

            // Get the product to verify it exists and get its price
            var product = await _dbContext.Products.FindAsync(request.ProductId);
            if (product == null)
            {
                throw new Exception($"Product with ID {request.ProductId} not found");
            }

            // Check if the item already exists in the cart
            var existingCartItem = await _dbContext.Carts
                .FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == request.ProductId);

            if (existingCartItem != null)
            {
                // Update quantity if item exists
                existingCartItem.Quantity += request.Quantity;
                existingCartItem.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                // Add new item if it doesn't exist
                var cartItemObj = new CartEntity
                {
                    UserId = userId,
                    ProductId = request.ProductId,
                    Quantity = request.Quantity,
                    CreatedAt = DateTime.UtcNow
                };
                _dbContext.Carts.Add(cartItemObj);
            }

            await _dbContext.SaveChangesAsync();

            // Return the updated cart item
            var cartItem = existingCartItem ?? await _dbContext.Carts
                .FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == request.ProductId);

            if (cartItem == null)
            {
                throw new Exception($"Cart item could not be found for ProductId: {request.ProductId}");
            }

            return new CartResponseModel
            {
                Id = cartItem.Id,
                UserId = cartItem.UserId,
                ProductId = cartItem.ProductId,
                ProductName = product.Name,
                ProductPrice = product.Price,
                Quantity = cartItem.Quantity,
                TotalPrice = product.Price * cartItem.Quantity,
                CreatedAt = cartItem.CreatedAt,
                UpdatedAt = cartItem.UpdatedAt
            };
        }
    }
} 