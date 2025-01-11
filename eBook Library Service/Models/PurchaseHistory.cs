using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eBook_Library_Service.Models
{
    public class PurchaseHistory
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } // Link to the user who made the purchase

        [Required]
        public int BookId { get; set; } // Link to the purchased book

        [Required]
        public string BookTitle { get; set; }
        [Required]
        public string Publisher { get; set; }// Store the book title for quick reference

        public string Description { get; set; } // Store the book description

        [Required]
        public int YearPublished { get; set; } // Store the year published

        [Required]
        public decimal Price { get; set; } // Store the price at the time of purchase

        [Required]
        public DateTime PurchaseDate { get; set; } = DateTime.UtcNow;
        [ForeignKey("BookId")]
        public string ImageUrl { get; set; }
        public virtual Book Book { get; set; }
    }
}
