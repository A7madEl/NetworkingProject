﻿using eBook_Library_Service.Data;
using eBook_Library_Service.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace eBook_Library_Service.Controllers
{
    public class BookController : Controller
    {
        private readonly Repository<Book> _books;
        private readonly Repository<Author> _authors;
        private readonly IWebHostEnvironment _webHostingEnvironment;

        public BookController(AppDbContext context, IWebHostEnvironment webHostingEnvironment)
        {
            _books = new Repository<Book>(context);
            _authors = new Repository<Author>(context);
            _webHostingEnvironment = webHostingEnvironment;
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
    }
}