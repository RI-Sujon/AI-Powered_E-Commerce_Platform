using Project.Application.Provider.Cart;
using Project.Application.Service.Defination;
using Project.Core;
using Project.Object.Requests;
using Project.Object.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Application.Service
{
    public class CartService : ICartService
    {
        private readonly ICartProvider _cartProvider;
        private readonly IApplicationContext _applicationContext;

        public CartService(IApplicationContext applicationContext, ICartProvider cartProvider)
        {
            _cartProvider = cartProvider;
            _applicationContext = applicationContext;
        }

        public async Task<CartResponseModel> AddItemToCart(CartRequestModel request, int userId)
        {
            _applicationContext.Log.LogInformation($"Going to execute AddItemToCart for ProductId: {request.ProductId}");
            var result = await _cartProvider.AddItemToCart(request, userId);
            _applicationContext.Log.LogInformation($"Completed AddItemToCart for ProductId: {request.ProductId}");
            return result;
        }

        public async Task<List<CartResponseModel>> GetCart(int userId)
        {
            _applicationContext.Log.LogInformation("Going to execute GetCart");
            var result = await _cartProvider.GetCart(userId);
            _applicationContext.Log.LogInformation("Completed GetCart");
            return result;
        }

        public async Task<bool> RemoveItemFromCart(int cartItemId, int userId)
        {
            _applicationContext.Log.LogInformation($"Going to execute RemoveItemFromCart for CartItemId: {cartItemId}");
            var result = await _cartProvider.RemoveItemFromCart(cartItemId, userId);
            _applicationContext.Log.LogInformation($"Completed RemoveItemFromCart for CartItemId: {cartItemId}");
            return result;
        }

        public async Task<CartResponseModel?> DecreaseItemQuantity(CartRequestModel request, int userId)
        {
            _applicationContext.Log.LogInformation($"Going to execute DecreaseItemQuantity for ProductId: {request.ProductId}");
            var result = await _cartProvider.DecreaseItemQuantity(request, userId);
            _applicationContext.Log.LogInformation($"Completed DecreaseItemQuantity for ProductId: {request.ProductId}");
            return result;
        }
    }
}
