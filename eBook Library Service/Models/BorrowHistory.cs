using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace eBook_Library_Service.Models
{
    public class BorrowHistory
    {
        [Key] // Marks this property as the primary key
        public int BorrowId { get; set; }

        [ForeignKey("User")] // Marks this property as a foreign key to the User class
        public string UserId { get; set; }

        [ForeignKey("Book")] // Marks this property as a foreign key to the Book class
        public int BookId { get; set; }

        public DateTime BorrowDate { get; set; } // Date the book was borrowed
        public DateTime ReturnDate { get; set; } // Date the book is due to be returned
        public virtual Book Book { get; set; }

        public virtual Users User { get; set; }
        // Navigation properties

    }
}