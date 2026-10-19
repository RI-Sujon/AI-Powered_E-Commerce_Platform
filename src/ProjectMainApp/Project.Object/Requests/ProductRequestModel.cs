using System.ComponentModel.DataAnnotations;

namespace Project.Object.Requests
{
    public class ProductRequestModel
    {
        [Required]
        [MaxLength(200)]
        public required string Name { get; set; }

        [MaxLength(2000)]
        public string? Description { get; set; }

        [Required]
        [MaxLength(200)]
        public required string Slug { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0.")]
        public decimal Price { get; set; }

        [Range(0, int.MaxValue)]
        public int? Stock { get; set; }

        public bool? IsActive { get; set; }

        public int? CategoryId { get; set; }

        public DateTime? DiscountStartDate { get; set; }
        public DateTime? DiscountEndDate { get; set; }
    }
} 