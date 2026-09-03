using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Project.Object.Requests;
using Project.Object.Responses;

namespace Project.Application.Service.Defination
{
    public interface ICartService
    {
        Task<CartResponseModel> AddItemToCart(CartRequestModel request, int userId);
        Task<List<CartResponseModel>> GetCart(int userId);
        Task<bool> RemoveItemFromCart(int cartItemId, int userId);
        Task<CartResponseModel?> DecreaseItemQuantity(CartRequestModel request, int userId);
    }
}
