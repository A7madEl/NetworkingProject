using eBook_Library_Service.Models;
using eBook_Library_Service.Services;
using eBook_Library_Service.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace eBook_Library_Service.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<Users> _signInManager;
        private readonly UserManager<Users> _userManager;
        private readonly EmailService _emailService;
        private readonly ILogger<PaymentController> _logger;

        public AccountController(SignInManager<Users> signInManager, UserManager<Users> userManager,
    EmailService emailService, ILogger<PaymentController> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _emailService = emailService;
            _logger = logger;
        }

        public IActionResult AccessDenied(string returnUrl = null)
        {
            TempData["ErrorMessage"] = "You do not have permission to access this page.";
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, false);

                if (result.Succeeded)
                {
                    if (model.Email.EndsWith("@orig.il", StringComparison.OrdinalIgnoreCase))
                    {
                        return RedirectToAction("Index", "Book");
                    }
                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    ModelState.AddModelError("", "Email or password is incorrect.");
                    return View(model);
                }
            }
            return View(model);
        }

        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Create the user
                var user = new Users
                {
                    FullName = model.Name,
                    Email = model.Email,
                    UserName = model.Email
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    // Send a welcome email
                    try
                    {
                        await _emailService.SendEmailAsync(
                            user.Email,
                            "Welcome to eBook Library Service",
                            $"Hello {user.FullName}, thank you for registering with eBook Library Service!"
                        );

                        _logger.LogInformation($"Welcome email sent to {user.Email}.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Failed to send welcome email to {user.Email}: {ex.Message}");
                    }

                    // Automatically log the user in after registration
                    await _signInManager.SignInAsync(user, isPersistent: false);

                    // Redirect to the home page
                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    // Handle errors
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError("", error.Description);
                    }
                    return View(model);
                }
            }

            // If the form is not valid, return to the registration page with validation errors
            return View(model);
        }
        public IActionResult VerifyEmail()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> VerifyEmail(VerifyEmailViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByNameAsync(model.Email);

                if (user == null)
                {
                    ModelState.AddModelError("", "Something is wrong!");
                    return View(model);
                }
                else
                {
                    return RedirectToAction("ChangePassword", "Account", new { username = user.UserName });
                }
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult ChangePassword(string userId, string token)
        {
            if (userId == null || token == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var model = new ChangePasswordViewModel { UserId = userId, Token = token };
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
            {
                // Don't reveal that the user does not exist
                return RedirectToAction("ChangePasswordConfirmation");
            }

            // Reset the password using the token
            var result = await _userManager.ResetPasswordAsync(user, model.Token, model.NewPassword);
            if (result.Succeeded)
            {
                // Optionally, send a confirmation email
                try
                {
                    await _emailService.SendEmailAsync(
                        user.Email,
                        "Password Changed Successfully",
                        "Your password has been changed successfully."
                    );

                    _logger.LogInformation($"Password change confirmation email sent to {user.Email}.");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to send password change confirmation email to {user.Email}: {ex.Message}");
                }

                return RedirectToAction("ChangePasswordConfirmation");
            }

            // Handle errors
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }
            return View(model);
        }

        public IActionResult ChangePasswordConfirmation()
        {
            return View();
        }
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }
        [HttpGet]
        public IActionResult ChangePasswordRequest()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ChangePasswordRequest(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                // Don't reveal that the user does not exist
                return RedirectToAction("ChangePasswordRequestConfirmation");
            }

            // Generate a password reset token
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            // Create the verification link
            var callbackUrl = Url.Action("ChangePassword", "Account", new { userId = user.Id, token = token }, protocol: HttpContext.Request.Scheme);

            // Send the verification email
            try
            {
                await _emailService.SendEmailAsync(
                    user.Email,
                    "Verify Your Password Change Request",
                    $"Please verify your password change request by clicking <a href='{callbackUrl}'>here</a>."
                );

                _logger.LogInformation($"Verification email sent to {user.Email}.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to send verification email to {user.Email}: {ex.Message}");
            }

            return RedirectToAction("ChangePasswordRequestConfirmation");
        }

        public IActionResult ChangePasswordRequestConfirmation()
        {
            return View();
        }
    }
}