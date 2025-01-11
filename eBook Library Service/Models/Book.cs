using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eBook_Library_Service.Models
{
    public class Book
    {
        public Book()
        {
            Stock = 3;
            BookAuthors = new List<BookAuthor>();
            EpubFilePath = null;
            F2bFilePath = null;
            MobiFilePath = null;
            PdfFilePath = null;
        }

        public int BookId { get; set; }

        [Required(ErrorMessage = "Title is required.")]
        [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters.")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Publisher is required.")]
        [StringLength(200, ErrorMessage = "Publisher cannot exceed 200 characters.")]
        public string Publisher { get; set; }

        public string Description { get; set; }

        [Required(ErrorMessage = "Year published is required.")]
        [Range(1000, 9999, ErrorMessage = "Year published must be a valid year.")]
        public int YearPublished { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Borrow price must be a positive value.")]
        public decimal BorrowPrice { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Buy price must be a positive value.")]
        public decimal BuyPrice { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Discount price must be a positive value.")]
        public decimal? DiscountPrice { get; set; }

        public DateTime? DiscountEndDate { get; set; }

        [Range(0, 3, ErrorMessage = "Only 3 copies can be borrowed.")]
        public int Stock { get; set; }

        [StringLength(50, ErrorMessage = "Age limit cannot exceed 50 characters.")]
        public string AgeLimit { get; set; }

        [StringLength(100, ErrorMessage = "Category cannot exceed 100 characters.")]
        public string Category { get; set; }

        [NotMapped]
        public IFormFile ImageFile { get; set; }

        public string? ImageUrl { get; set; } = "images/BookDefault.png";

        public string? EpubFilePath { get; set; }
        public string? F2bFilePath { get; set; }
        public string? MobiFilePath { get; set; }
        public string? PdfFilePath { get; set; }

        [NotMapped]
        public IFormFile EpubFile { get; set; }

        [NotMapped]
        public IFormFile F2bFile { get; set; }

        [NotMapped]
        public IFormFile MobiFile { get; set; }

        [NotMapped]
        public IFormFile PdfFile { get; set; }

        public string Formats { get; set; } = "epub,f2b,mobi,PDF";

        public virtual ICollection<BookAuthor> BookAuthors { get; set; }
        public bool IsBuyOnly { get; set; } = false; // Default to false
    }
}
