using System.Runtime.CompilerServices;
using System.Security.Claims;
using eBook_Library_Service.Data;
using eBook_Library_Service.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace eBook_Library_Service.Controllers
{
    public class BookController : Controller
    {
        private readonly AppDbContext _context;
        private readonly Repository<Book> _books;
        private readonly Repository<Author> _authors;
        private readonly ILogger<BookController> _logger;
        private readonly IWebHostEnvironment _webHostingEnvironment;

        public BookController(AppDbContext context, IWebHostEnvironment webHostingEnvironment, ILogger<BookController> logger)
        {
            _books = new Repository<Book>(context);
            _authors = new Repository<Author>(context);
            _webHostingEnvironment = webHostingEnvironment;
            _logger = logger;
            _context = context;
        }

        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Index(string category = null)
        {
            var bookList = await _books.GetAllsync();

            if (!string.IsNullOrEmpty(category))
            {
                bookList = bookList.Where(b => b.Category == category);
            }

            ViewBag.Categories = await GetCategories();
            ViewBag.SelectedCategory = category;

            return View(bookList);
        }

        [Authorize]
        public async Task<IActionResult> UserIndex(string category = null, string searchQuery = null)
        {
            var bookList = await _books.GetAllsync();

            // Apply discounts
            foreach (var book in bookList)
            {
                if (book.DiscountPrice.HasValue && book.DiscountPrice.Value > 0 && book.BuyPrice > 0)
                {
                    var discountAmount = book.BuyPrice * (book.DiscountPrice.Value / 100);
                    book.BuyPrice -= discountAmount;
                }

                var bookWithAuthors = await _books.GetByIdAsync(book.BookId, new QueryOptions<Book>
                {
                    Includes = "BookAuthors.Author"
                });
                book.BookAuthors = bookWithAuthors.BookAuthors;
            }

            // Apply filters
            if (!string.IsNullOrEmpty(category))
            {
                bookList = bookList.Where(b => b.Category == category);
            }
            if (!string.IsNullOrEmpty(searchQuery))
            {
                bookList = bookList.Where(b =>
                    (!string.IsNullOrEmpty(b.Title) && b.Title.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(b.Description) && b.Description.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(b.Category) && b.Category.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)));
            }

            ViewBag.Categories = await GetCategories();
            ViewBag.SelectedCategory = category;
            ViewBag.SearchQuery = searchQuery;

            // Return partial view for AJAX requests
            if (IsAjaxRequest(Request))
            {
                return PartialView("_BookList", bookList);
            }

            return View(bookList);
        }
        [Authorize(Policy = "AdminOnly")]
        [HttpGet]
        public async Task<IActionResult> AddEdit(int id)
        {
            ViewBag.Authors = await _authors.GetAllsync();

            if (id == 0)
            {
                ViewBag.Operation = "Add";
                return View(new Book());
            }

            var book = await _books.GetByIdAsync(id, new QueryOptions<Book>
            {
                Includes = "BookAuthors.Author",
                Where = b => b.BookId == id
            });

            if (book == null)
            {
                return NotFound();
            }

            ViewBag.Operation = "Edit";
            return View(book);
        }
        [Authorize(Policy = "AdminOnly")]
        [HttpPost]
        public async Task<IActionResult> AddEdit(Book book, int[] authorIds)
        {
            if (!ModelState.IsValid)
            {
                // Log or inspect ModelState errors
                var errors = ModelState.Values.SelectMany(v => v.Errors);
                foreach (var error in errors)
                {
                    Console.WriteLine(error.ErrorMessage); // Log to console
                }

                ViewBag.Authors = await _authors.GetAllsync();
                ViewBag.Operation = book.BookId == 0 ? "Add" : "Edit";
                return View(book);
            }

            // Save image file
            if (book.ImageFile != null)
            {
                book.ImageUrl = await SaveImage(book.ImageFile);
                Console.WriteLine($"Image saved: {book.ImageUrl}"); // Log the image URL
            }

            // Save eBook files to the server
            book.EpubFilePath = book.EpubFile != null ? await SaveFile(book.EpubFile, "epub") : null;
            book.F2bFilePath = book.F2bFile != null ? await SaveFile(book.F2bFile, "f2b") : null;
            book.MobiFilePath = book.MobiFile != null ? await SaveFile(book.MobiFile, "mobi") : null;
            book.PdfFilePath = book.PdfFile != null ? await SaveFile(book.PdfFile, "pdf") : null;

            // Save the book to the database
            if (book.BookId == 0)
            {
                foreach (int id in authorIds)
                {
                    book.BookAuthors.Add(new BookAuthor { AuthorId = id });
                }

                await _books.AddAsync(book);
            }
            else
            {
                var existingBook = await _books.GetByIdAsync(book.BookId, new QueryOptions<Book> { Includes = "BookAuthors" });

                if (existingBook == null)
                {
                    return NotFound();
                }

                // Update properties
                existingBook.Title = book.Title;
                existingBook.Publisher = book.Publisher;
                existingBook.Description = book.Description;
                existingBook.YearPublished = book.YearPublished;
                existingBook.BuyPrice = book.BuyPrice;
                existingBook.BorrowPrice = book.BorrowPrice;
                existingBook.DiscountPrice = book.DiscountPrice;
                existingBook.DiscountEndDate = book.DiscountEndDate;
                existingBook.Stock = book.Stock;
                existingBook.AgeLimit = book.AgeLimit;
                existingBook.Category = book.Category;
                existingBook.Formats = book.Formats;

                // Update file paths if changed
                if (book.EpubFilePath != null)
                {
                    existingBook.EpubFilePath = book.EpubFilePath;
                }
                if (book.F2bFilePath != null)
                {
                    existingBook.F2bFilePath = book.F2bFilePath;
                }
                if (book.MobiFilePath != null)
                {
                    existingBook.MobiFilePath = book.MobiFilePath;
                }
                if (book.PdfFilePath != null)
                {
                    existingBook.PdfFilePath = book.PdfFilePath;
                }

                // Update image URL if changed
                if (!string.IsNullOrEmpty(book.ImageUrl))
                {
                    existingBook.ImageUrl = book.ImageUrl;
                }

                // Update authors
                existingBook.BookAuthors.Clear();
                foreach (int id in authorIds)
                {
                    existingBook.BookAuthors.Add(new BookAuthor { AuthorId = id });
                }

                await _books.UpdateAsync(existingBook);
            }

            return RedirectToAction("Index");
        }

        [Authorize(Policy = "AdminOnly")]

        private async Task<string> SaveImage(IFormFile imageFile)
        {
            try
            {
                // Define the folder where images will be saved
                var uploadsFolder = Path.Combine(_webHostingEnvironment.WebRootPath, "images");

                // Create the folder if it doesn't exist
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                // Generate a unique file name to avoid conflicts
                var uniqueFileName = Guid.NewGuid().ToString() + "_" + imageFile.FileName;

                // Define the full file path
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                // Save the file to the server
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(fileStream);
                }

                // Return the file name (or relative path) to be stored in the database
                return uniqueFileName;
            }
            catch (Exception ex)
            {
                // Log the error for debugging
                Console.WriteLine($"Error saving image: {ex.Message}");
                throw; // Re-throw the exception to see it in the debugger
            }
        }

        [Authorize(Policy = "AdminOnly")]


        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _books.DeleteAsync(id);
                return RedirectToAction("Index");
            }
            catch
            {
                ModelState.AddModelError("", "Book not found");
                return RedirectToAction("Index");
            }
        }

        // Book Details
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var book = await _books.GetByIdAsync(id, new QueryOptions<Book>
            {
                Includes = "BookAuthors.Author",
                Where = b => b.BookId == id
            });

            if (book == null)
            {
                return NotFound();
            }

            return View(book);
        }

        // Helper Methods
        private async Task<IEnumerable<string>> GetCategories()
        {
            var categories = await _books.GetAllsync();
            return categories.Select(b => b.Category).Distinct().ToList();
        }

        private bool IsAjaxRequest(HttpRequest request)
        {
            return request.Headers["X-Requested-With"] == "XMLHttpRequest";
        }
        private async Task<string> SaveFile(IFormFile file, string format)
        {
            try
            {
                var uploadsFolder = Path.Combine(_webHostingEnvironment.WebRootPath, "uploads", format);
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(fileStream);
                }

                return Path.Combine("uploads", format, uniqueFileName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving file: {ex.Message}");
                throw; // Re-throw the exception to see it in the debugger
            }
        }
        public async Task<IActionResult> BorrowHistory()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
            {
                return Unauthorized(); // User is not logged in
            }

            // Fetch the user's borrow history with book details
            var borrowHistory = await _context.BorrowHistory
                .Include(bh => bh.Book) // Include book details
                .Where(bh => bh.UserId == userId)
                .OrderByDescending(bh => bh.BorrowDate)
                .ToListAsync();

            // Create a dictionary to store waiting list positions
            var waitingListPositions = new Dictionary<int, int>();

            foreach (var history in borrowHistory)
            {
                if (history.ReturnDate == DateTime.MinValue) // Check if it's a waiting list entry
                {
                    var position = await _context.WaitingLists
                        .Where(wl => wl.UserId == history.UserId && wl.BookId == history.BookId)
                        .Select(wl => wl.Position)
                        .FirstOrDefaultAsync();

                    waitingListPositions[history.BorrowId] = position; // Store the position in the dictionary
                }
            }

            // Pass the data to the view
            ViewBag.WaitingListPositions = waitingListPositions;
            return View(borrowHistory); // Pass List<BorrowHistory> to the view
        }
        public async Task<int> AddToWaitingListAsync(string userId, int bookId)
        {
            // Calculate the user's position in the waiting list
            var position = await _context.WaitingLists
                .CountAsync(wl => wl.BookId == bookId) + 1;

            // Add the user to the waiting list
            var waitingListEntry = new WaitingList
            {
                UserId = userId,
                BookId = bookId,
                JoinDate = DateTime.UtcNow,
                Position = position
            };

            _context.WaitingLists.Add(waitingListEntry);
            await _context.SaveChangesAsync();

            return position; // Return the user's position in the waiting list
        }
        public async Task<IActionResult> BorrowBook(int bookId)
        {
            // Get the current user's ID
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
            {
                return Unauthorized(); // User is not logged in
            }
            var WaitingCounts = await _context.WaitingLists
                .CountAsync(bh => bh.UserId == userId);


            // Check if the user has already borrowed this book
            var isAlreadyBought = await _context.BorrowHistory
                .AnyAsync(wl => wl.UserId == userId && wl.BookId == bookId);

            if (isAlreadyBought)
            {
                TempData["ErrorMessage"] = "You have already borrowed this book.";
                return RedirectToAction("UserIndex", "Book");
            }


            // Check if the user has already borrowed 3 books
            var borrowedBooksCount = await _context.BorrowHistory
                .CountAsync(bh => bh.UserId == userId && bh.ReturnDate > DateTime.UtcNow);

            if (borrowedBooksCount >= 3)
            {
                TempData["ErrorMessage"] = "You have reached the maximum borrowing limit of 3 books.";
                return RedirectToAction("UserIndex", "Book");
            }
            if (borrowedBooksCount + WaitingCounts > 3)
            {
                TempData["ErrorMessage"] = "You have reached the maximum borrowing and waiting limit of 3 books.";
                return RedirectToAction("UserIndex", "Book");
            }

            // Check if the book exists
            var book = await _context.Books.FindAsync(bookId);
            if (book == null)
            {
                return NotFound(); // Book does not exist
            }

            // Check if the user is on the waiting list for any books
            
            if (book.Stock > 0)
            {
               

                // Book is available - proceed to payment
                TempData["BookId"] = bookId;
                TempData["IsBorrow"] = true; // Indicate that this is a borrow request
                return RedirectToAction("BorrowNow", "Payment", new { bookId = bookId });
            }
            else
            {
                // Book is not available - ask the user if they want to join the waiting list
                TempData["ShowWaitingListPopup"] = true;
                TempData["BookId"] = bookId;
                TempData["BookTitle"] = book.Title;
                return RedirectToAction("UserIndex", "Book");
            }
        }

        public async Task<IActionResult> JoinWaitingList(int bookId)
        {
            // Get the current user's ID
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
            {
                return Unauthorized(); // User is not logged in
            }

            // Check if the user is already in the waiting list for this book
            var isAlreadyInWaitingList = await _context.WaitingLists
                .AnyAsync(wl => wl.UserId == userId && wl.BookId == bookId);

            if (isAlreadyInWaitingList)
            {
                TempData["ErrorMessage"] = "You are already in the waiting list for this book.";
                return RedirectToAction("UserIndex", "Book");
            }

            // Proceed to payment before adding to the waiting list
            TempData["BookId"] = bookId;
            TempData["IsBorrow"] = false; // Indicate that this is a waiting list request
            return RedirectToAction("BorrowNow", "Payment", new { bookId = bookId });
        }

        public async Task<IActionResult> BorrowedRequests()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
            {
                return Unauthorized(); // User is not logged in
            }

            // Fetch the user's waiting list entries
            var waitingListEntries = await _context.WaitingLists
                .Where(wl => wl.UserId == userId)
                .OrderBy(wl => wl.Position)
                .ToListAsync();

            // Fetch book details for each waiting list entry
            var bookDetails = new Dictionary<int, Book>(); // Key: BookId, Value: Book

            foreach (var entry in waitingListEntries)
            {
                var book = await _context.Books
                    .FirstOrDefaultAsync(b => b.BookId == entry.BookId);

                if (book != null)
                {
                    bookDetails[entry.BookId] = book; // Store book details in a dictionary
                }
            }

            // Pass the data to the view
            ViewBag.WaitingListEntries = waitingListEntries;
            ViewBag.BookDetails = bookDetails;

            return View(); // No model is passed to the view
        }
        private async Task BorrowBookAsync(string userId, int bookId)
        {
            // Reduce the book's stock
            var book = await _context.Books.FindAsync(bookId);
            if (book.Stock > 0)
            {
                book.Stock -= 1;

                // Add a record to the BorrowHistory table
                var borrowHistory = new BorrowHistory
                {
                    UserId = userId,
                    BookId = bookId,
                    BorrowDate = DateTime.UtcNow,
                    ReturnDate = DateTime.UtcNow.AddDays(30) // 30-day borrowing period
                };

                _context.BorrowHistory.Add(borrowHistory);
                await _context.SaveChangesAsync();
            }
            else
            {
                throw new InvalidOperationException("The book is out of stock.");
            }
        }
        public async Task<List<WaitingList>> GetWaitingListForBookAsync(int bookId)
        {
            var waitingList = await _context.WaitingLists
                .Where(wl => wl.BookId == bookId)
                .OrderBy(wl => wl.Position)
                .ToListAsync();

            return waitingList;
        }
        public async Task NotifyNextUserAsync(int bookId)
        {
            // Get the next user in the waiting list
            var nextUser = await _context.WaitingLists
                .Where(wl => wl.BookId == bookId)
                .OrderBy(wl => wl.Position)
                .FirstOrDefaultAsync();

            if (nextUser != null)
            {



                // Remove the user from the waiting list
                _context.WaitingLists.Remove(nextUser);
                await _context.SaveChangesAsync();

                // Update positions of remaining users
                await UpdatePositionsAsync(bookId, nextUser.Position);
            }
        }

        private async Task UpdatePositionsAsync(int bookId, int removedPosition)
        {
            var waitingListEntries = await _context.WaitingLists
                .Where(wl => wl.BookId == bookId && wl.Position > removedPosition)
                .ToListAsync();

            foreach (var entry in waitingListEntries)
            {
                entry.Position -= 1; // Move users up in the queue
            }

            await _context.SaveChangesAsync();
        }
        public async Task RemoveFromWaitingListAsync(string userId, int bookId)
        {
            var waitingListEntry = await _context.WaitingLists
                .FirstOrDefaultAsync(wl => wl.UserId == userId && wl.BookId == bookId);

            if (waitingListEntry != null)
            {
                _context.WaitingLists.Remove(waitingListEntry);
                await _context.SaveChangesAsync();

                // Update positions of remaining users
                await UpdatePositionsAsync(bookId, waitingListEntry.Position);
            }
        }
        public async Task<int> GetUserPositionAsync(string userId, int bookId)
        {
            var position = await _context.WaitingLists
                .Where(wl => wl.UserId == userId && wl.BookId == bookId)
                .Select(wl => wl.Position)
                .FirstOrDefaultAsync();

            return position;
        }
        public async Task<IActionResult> ReturnBook(int borrowId)
        {
            var borrowRecord = await _context.BorrowHistory
                .Include(bh => bh.Book)
                .FirstOrDefaultAsync(bh => bh.BorrowId == borrowId);

            if (borrowRecord == null)
            {
                return NotFound(); // Borrow record not found
            }

            // Increase the book's stock
            borrowRecord.Book.Stock += 1;

            // Remove the borrow record
            _context.BorrowHistory.Remove(borrowRecord);
            await _context.SaveChangesAsync();

            // Notify the next user in the waiting list
            await NotifyNextUserAsync(borrowRecord.BookId);

            TempData["Message"] = $"You have successfully returned {borrowRecord.Book.Title}.";
            return RedirectToAction("BorrowHistory", "Book");
        }
       
    }
}
