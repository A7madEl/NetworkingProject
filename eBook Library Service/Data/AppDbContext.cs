using eBook_Library_Service.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace eBook_Library_Service.Data
{
    public class AppDbContext : IdentityDbContext<Users>
    {
        public AppDbContext(DbContextOptions options) : base(options) { }
        public DbSet<Book> Books { get; set; }
        public DbSet<Author> Authors { get; set; }
        public DbSet<BookAuthor> BookAuthors { get; set; }
        public DbSet<BorrowRequest> BorrowRequests { get; set; }
        public DbSet<Contact> Contacts { get; set; }
        public DbSet<ShoppingCart> ShoppingCarts { get; set; }
        public DbSet<ShoppingCartItem> ShoppingCartItems { get; set; }
        public DbSet<PurchaseHistory> PurchaseHistories { get; set; }
        public DbSet<BorrowHistory> BorrowHistory { get; set; }
        public DbSet<WaitingList> WaitingLists { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<BookAuthor>().HasKey(ba => new { ba.BookId, ba.AuthorId });
            modelBuilder.Entity<BookAuthor>().HasOne(ba => ba.Book)
                .WithMany(b => b.BookAuthors)
                .HasForeignKey(ba => ba.BookId);
            modelBuilder.Entity<BookAuthor>().HasOne(ba => ba.Author)
                .WithMany(a => a.BookAuthors)
                .HasForeignKey(ba => ba.AuthorId);
            modelBuilder.Entity<BorrowHistory>()
            .HasOne(bh => bh.User)
            .WithMany()
            .HasForeignKey(bh => bh.UserId); // Foreign key is UserId

            // Configure the BorrowHistory to Book relationship
            modelBuilder.Entity<BorrowHistory>()
                 .HasOne(bh => bh.Book) // BorrowHistory has one Book
                .WithMany()     // Book can have many BorrowHistory records
                .HasForeignKey(bh => bh.BookId); // Foreign key is BookId
            modelBuilder.Entity<WaitingList>()
            .HasOne<Users>() // WaitingList has one User
            .WithMany()     // User can have many WaitingList entries
            .HasForeignKey(wl => wl.UserId); // Foreign key is UserId

            // Configure the WaitingList to Book relationship
            modelBuilder.Entity<WaitingList>()
                .HasOne<Book>() // WaitingList has one Book
                .WithMany()     // Book can have many WaitingList entries
                .HasForeignKey(wl => wl.BookId);

            modelBuilder.Entity<BorrowHistory>(entity =>
            {
                entity.Property(bh => bh.BorrowDate)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("GETUTCDATE()"); // Ensure BorrowDate is set to UTC time

                entity.Property(bh => bh.ReturnDate)
                    .HasColumnType("datetime"); // Ensure ReturnDate is stored as datetime
            });
        


        modelBuilder.Entity<Author>().HasData(new Author { AuthorId = 1, Name = "Author1" });
            modelBuilder.Entity<Author>().HasData(new Author { AuthorId = 2, Name = "Author2" });
            modelBuilder.Entity<Book>().HasData(new Book
            {
                BookId = 1,
                Title = "The Great Gatsby",
                Publisher = "Scribner",
                Description = "A novel written by American author F. Scott Fitzgerald. It is a critique of the American Dream in the 1920s.",
                YearPublished = 1925,
                BorrowPrice = 5.99m,
                BuyPrice = 15.99m,
                DiscountPrice = 12.99m,
                DiscountEndDate = new DateTime(2025, 12, 31),
                Stock = 3,
                AgeLimit = "18+",
                Category = "Fiction"
            },
             new Book
             {
                 BookId = 2,
                 Title = "1984",
                 Publisher = "Secker & Warburg",
                 Description = "A dystopian social science fiction novel and cautionary tale, written by the English writer George Orwell.",
                 YearPublished = 1949,
                 BorrowPrice = 4.99m,
                 BuyPrice = 12.99m,
                 DiscountPrice = 10.99m,
                 DiscountEndDate = new DateTime(2025, 11, 30),
                 Stock = 3,
                 AgeLimit = "16+",
                 Category = "Science Fiction"
             },
             new Book
             {
                 BookId = 3,
                 Title = "To Kill a Mockingbird",
                 Publisher = "J.B. Lippincott & Co.",
                 Description = "A novel by Harper Lee published in 1960. It was immediately successful, winning the Pulitzer Prize for Fiction in 1961.",
                 YearPublished = 1960,
                 BorrowPrice = 6.99m,
                 BuyPrice = 14.99m,
                 DiscountPrice = 11.99m,
                 DiscountEndDate = new DateTime(2025, 10, 31),
                 Stock = 3,
                 AgeLimit = "12+",
                 Category = "Classic"
             }
         );
            modelBuilder.Entity<BookAuthor>().HasData(new BookAuthor { BookId = 1, AuthorId = 1 });
            modelBuilder.Entity<BookAuthor>().HasData(new BookAuthor { BookId = 1, AuthorId = 2 });
            modelBuilder.Entity<BookAuthor>().HasData(new BookAuthor { BookId = 2, AuthorId = 2 });
            modelBuilder.Entity<BookAuthor>().HasData(new BookAuthor { BookId = 3, AuthorId = 1 });

            modelBuilder.Entity<ShoppingCart>()
           .HasOne(sc => sc.User)
           .WithMany()
           .HasForeignKey(sc => sc.UserId);

            modelBuilder.Entity<ShoppingCartItem>()
                .HasOne(sci => sci.Book)
                .WithMany()
                .HasForeignKey(sci => sci.BookId);

            modelBuilder.Entity<ShoppingCartItem>()
                .HasOne(sci => sci.ShoppingCart)
                .WithMany(sc => sc.Items)
                .HasForeignKey(sci => sci.ShoppingCartId);
        }
    }
}
