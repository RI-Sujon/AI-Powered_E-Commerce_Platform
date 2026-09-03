using Project.Core;
using Project.Object.Requests;
using Project.Object.Responses;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Project.Application.Provider.Cart.Command;
using Project.Application.Provider.Cart.Query;

namespace Project.Application.Provider.Cart
{
    public class CartProvider : ICartProvider
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IApplicationContext _applicationContext;

        public CartProvider(IApplicationContext applicationContext, IServiceProvider serviceProvider)
        {
            _applicationContext = applicationContext;
            _serviceProvider = serviceProvider;
        }

        public async Task<CartResponseModel> AddItemToCart(CartRequestModel request, int userId)
        {
            var addItemToCartCommand = _serviceProvider.GetRequiredService<AddItemToCartCommand>();

            _applicationContext.Log.LogInformation($"Going to execute AddItemToCart for ProductId: {request.ProductId}");
            var result = await addItemToCartCommand.AddItemToCart(request, userId);
            _applicationContext.Log.LogInformation($"Completed AddItemToCart for ProductId: {request.ProductId}");

            return result;
        }

        public async Task<List<CartResponseModel>> GetCart(int userId)
        {
            var getCartQuery = _serviceProvider.GetRequiredService<GetCartQuery>();

            _applicationContext.Log.LogInformation("Going to execute GetCart in provider");
            var response = await getCartQuery.GetCart(userId);
            _applicationContext.Log.LogInformation("Completed GetCart in provider");
            return response;
        }

        public async Task<bool> RemoveItemFromCart(int cartItemId, int userId)
        {
            var removeItemFromCartCommand = _serviceProvider.GetRequiredService<RemoveItemFromCartCommand>();

            _applicationContext.Log.LogInformation($"Going to execute RemoveItemFromCart for CartItemId: {cartItemId}");
            var result = await removeItemFromCartCommand.RemoveItemFromCart(cartItemId, userId);
            _applicationContext.Log.LogInformation($"Completed RemoveItemFromCart for CartItemId: {cartItemId}");

            return result;
        }

        public async Task<CartResponseModel?> DecreaseItemQuantity(CartRequestModel request, int userId)
        {
            var decreaseItemQuantityCommand = _serviceProvider.GetRequiredService<DecreaseItemQuantityCommand>();

            _applicationContext.Log.LogInformation($"Going to execute DecreaseItemQuantity for ProductId: {request.ProductId}");
            var result = await decreaseItemQuantityCommand.DecreaseItemQuantity(request, userId);
            _applicationContext.Log.LogInformation($"Completed DecreaseItemQuantity for ProductId: {request.ProductId}");

            return result;
        }
    }
}
