using Project.Core;
using Project.Object.Entities;
using Project.Object.Requests;
using Project.Object.Responses;
using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Project.Application.Provider.Cart.Command
{
    public class DecreaseItemQuantityCommand
    {
        private readonly IApplicationContext _applicationContext;
        private readonly AppDbContext _dbContext;

        public DecreaseItemQuantityCommand(IApplicationContext applicationContext, AppDbContext dbContext)
        {
            _applicationContext = applicationContext;
            _dbContext = dbContext;
        }

        public async Task<CartResponseModel?> DecreaseItemQuantity(CartRequestModel request, int userId)
        {
            _applicationContext.Log.LogInformation("Going to execute - DecreaseItemQuantityCommand");

            // Get the product to verify it exists and get its price
            var product = await _dbContext.Products.FindAsync(request.ProductId);
            if (product == null)
            {
                throw new Exception($"Product with ID {request.ProductId} not found");
            }

            // Get the cart item
            var cartItem = await _dbContext.Carts
                .FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == request.ProductId);

            if (cartItem == null)
            {
                throw new Exception($"Cart item not found for ProductId: {request.ProductId}");
            }

            // Decrease quantity
            cartItem.Quantity = Math.Max(0, cartItem.Quantity - request.Quantity);
            cartItem.UpdatedAt = DateTime.UtcNow;

            // If quantity becomes 0, remove the item
            if (cartItem.Quantity == 0)
            {
                _dbContext.Carts.Remove(cartItem);
            }

            await _dbContext.SaveChangesAsync();

            // If item was removed, return null
            if (cartItem.Quantity == 0)
            {
                return null;
            }

            // Return the updated cart item
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