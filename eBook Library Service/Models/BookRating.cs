using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace eBook_Library_Service.Models
{
    public class BookRating
    {
        public int BookRatingId { get; set; }

        [ForeignKey("User")]
        public string UserId { get; set; } // User who submitted the rating

        [ForeignKey("Book")]
        public int BookId { get; set; } // Book being rated

        [Range(1, 5)]
        public int Rating { get; set; } // Rating from 1 to 5 stars

        public string Feedback { get; set; } // Optional feedback text

        public DateTime RatingDate { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual Users User { get; set; }
        public virtual Book Book { get; set; }
    }
}
