using Project.Object.Requests;
using Project.Object.Responses;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Project.Application.Provider.Cart
{
    public interface ICartProvider
    {
        Task<CartResponseModel> AddItemToCart(CartRequestModel request, int userId);
        Task<List<CartResponseModel>> GetCart(int userId);
        Task<bool> RemoveItemFromCart(int cartItemId, int userId);
        Task<CartResponseModel?> DecreaseItemQuantity(CartRequestModel request, int userId);
    }
}
