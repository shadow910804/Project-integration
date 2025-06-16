using ECommercePlatform.Controllers;
using ECommercePlatform.Models;

namespace ECommercePlatform.Models.ViewModels
{
    public class CartItemListViewModel
    {
        public List<CartItem> CartItem { get; set; } = new();
        public List<CartItemWithStatus> CartItemWithStatus { get; set; } = new();
    }
}