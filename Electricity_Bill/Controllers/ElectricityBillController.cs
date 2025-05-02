using Electricity_Bill.Data;
using Electricity_Bill.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rotativa.AspNetCore;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Electricity_Bill.Controllers
{
    public class ElectricityBillController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly ILogger<ElectricityBillController> _logger;

        public ElectricityBillController(ApplicationDbContext context, UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager, ILogger<ElectricityBillController> logger)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
        }
        
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
            {
                var userDetail = await _context.UserDetails.FirstOrDefaultAsync(u => u.UserId == userId);
                ViewData["FirstName"] = userDetail?.FirstName ?? "User";
            }
            else
            {
                ViewData["FirstName"] = "User";
            }

            ViewData["Title"] = "Dashboard";
            return View();
        }
        
        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            ViewData["Title"] = "Dashboard";
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            _logger.LogInformation("Accessed by User ID: {UserId}", userId);

            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("User ID not found, assuming user not logged in.");
                ViewData["FirstName"] = "Guest";
                return View();
            }

            var userDetail = await _context.UserDetails.FirstOrDefaultAsync(u => u.UserId == userId);
            if (userDetail != null)
            {
                ViewData["FirstName"] = userDetail.FirstName;
                _logger.LogInformation("Found UserDetails, setting FirstName: {FirstName}", userDetail.FirstName);
            }
            else
            {
                _logger.LogWarning("UserDetails not found for User ID: {UserId}", userId);
                ViewData["FirstName"] = "Guest";
            }

            return View();
        }

        public async Task<IActionResult> GenerateBillReport(BillCalculationViewModel viewModel)
        {
            if (!User.Identity.IsAuthenticated)
            {
                TempData["Error"] = "You must be logged in to generate reports.";
                return RedirectToAction("Login");
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                TempData["Error"] = "Unable to determine user identity.";
                return RedirectToAction("Index");
            }

            var query = _context.ElectricityBills.AsQueryable();

            // Assuming your UI sets startDate and endDate to the same day for "today's" report
            DateTime endDate = viewModel.EndDate.AddDays(1); // Extend end date to include all of today

            query = query.Where(b => b.UserId == userId &&
                                     b.Date >= viewModel.StartDate &&
                                     b.Date <= endDate); // Note the use of '< endDate'

            if (!string.IsNullOrEmpty(viewModel.ReferenceNumber))
            {
                query = query.Where(b => b.ReferenceNumber == viewModel.ReferenceNumber);
            }

            viewModel.Bills = await query.ToListAsync();

            if (!viewModel.Bills.Any())
            {
                TempData["Error"] = "No records found for the specified date range.";
                return View("Calculate", viewModel);
            }

            var pdfResult = new ViewAsPdf("PDFBillReport", viewModel)
            {
                FileName = $"BillReport_{viewModel.StartDate.ToString("yyyy/MM/dd")}_to_{viewModel.EndDate.ToString("yyyy/MM/dd")}.pdf"
            };

            return pdfResult;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBill(BillCalculationViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Redirect to the login page or show an appropriate error if userId is null
            if (string.IsNullOrEmpty(userId))
            {
                // Optionally, add a flash message or log that user is not authenticated
                TempData["Error"] = "You must be logged in to create a bill.";
                return RedirectToAction("Login"); // Assume 'Login' is your login action
            }

            if (ModelState.IsValid)
            {
                var newBill = new ElectricityBill
                {
                    SiteName = model.SiteName,
                    ReferenceNumber = model.ReferenceNumber,
                    PreviousReading = model.PreviousReading,
                    CurrentReading = model.CurrentReading,
                    Date = DateTime.Now,
                    Month = model.Month,
                    Year = model.Year,
                    UserId = userId, // Safe to use after null check
                    NetReading = model.CurrentReading - model.PreviousReading
                };

                _context.ElectricityBills.Add(newBill);
                await _context.SaveChangesAsync();
                return RedirectToAction("CalculateForm");
            }

            return View("Calculate", model);
        }


        public async Task<IActionResult> CalculateForm()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Ensure userId is not null to avoid operating on a potentially unauthorized user
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login"); // Redirect user to login page or handle appropriately
            }

            // Retrieve bills associated with the user
            var bills = await _context.ElectricityBills
                .Where(b => b.UserId == userId)
                .OrderByDescending(b => b.Date)
                .ToListAsync();

            // Retrieve sites associated with the user
            var sites = await _context.Sites
                .Where(s => s.UserId == userId)
                .ToListAsync();

            // Retrieve reference numbers, filtering out any nulls
            var referenceNumbers = await _context.ReferenceNumbers
                .Where(r => r.UserId == userId && r.ReferenceNumber != null)
                .Select(r => r.ReferenceNumber)
                .ToListAsync();

            // Populate the view model
            var viewModel = new BillCalculationViewModel
            {
                Bills = bills,
                Sites = sites,
                ReferenceNumbers = referenceNumbers.Cast<string>().ToList() // Ensuring all are non-null
            };

            return View("Calculate", viewModel);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CalculateBill(BillCalculationViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                // Redirect to login page or show an error because userId should not be null at this point
                ModelState.AddModelError("", "User must be logged in.");
                return View("Calculate", model); // Consider redirecting to a Login page instead
            }

            if (ModelState.IsValid)
            {
                var siteName = await _context.Sites
                    .Where(s => s.Id == model.SelectedSiteId && s.UserId == userId)
                    .Select(s => s.SiteName)
                    .FirstOrDefaultAsync();

                if (string.IsNullOrEmpty(siteName))
                {
                    ModelState.AddModelError("", "Invalid site selected.");
                    return View("Calculate", model);
                }

                var newBill = new ElectricityBill
                {
                    UserId = userId, 
                    SiteName = siteName, 
                    ReferenceNumber = model.SelectedReferenceNumber,
                    PreviousReading = model.PreviousReading,
                    CurrentReading = model.CurrentReading,
                    NetReading = model.CurrentReading - model.PreviousReading,
                    Date = DateTime.Now,
                    Month = model.Month,
                    Year = model.Year
                };

                _context.ElectricityBills.Add(newBill);
                await _context.SaveChangesAsync();

                return RedirectToAction("CalculateForm");
            }

            // If ModelState is not valid, return to the Calculate view with the model.
            return View("Calculate", model);
        }


        [AllowAnonymous]
        public IActionResult Login()
        {
            return View(new LoginViewModel());
        }
        
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl ?? Url.Action("Index", "Home"); 
            if (ModelState.IsValid)
            {
                var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);
                if (result.Succeeded)
                {
                    // Redirect to a page where user selects a site or to the returnUrl if it's valid
                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return Redirect(returnUrl);
                    }
                    return RedirectToAction("AddSite");
                }
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            }
            return View(model);
        }




        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login");
        }

        [AllowAnonymous]
        public IActionResult Register()
        {
            return View(new RegisterViewModel());
        }
       
        [HttpGet]
        public async Task<IActionResult> AddSite()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "ElectricityBill");
            }

            var sites = await _context.Sites.Where(s => s.UserId == userId).ToListAsync();
            var referenceNumbers = await _context.ReferenceNumbers
                                                  .Where(r => r.UserId == userId)
                                                  .GroupBy(r => r.SiteId)
                                                  .ToDictionaryAsync(g => g.Key, g => g.Select(x => x.ReferenceNumber).ToList());

            ViewBag.Sites = sites;
            ViewBag.ReferenceNumbers = referenceNumbers; // This is now a dictionary mapped by SiteId

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddSite(Site model, List<string> ReferenceNumbers)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return RedirectToAction("Login", "ElectricityBill");
            }

            if (string.IsNullOrWhiteSpace(model.SiteName))
            {
                ModelState.AddModelError("SiteName", "Site Name is required.");
                // Re-load data for re-display
                ViewBag.Sites = await _context.Sites.Where(s => s.UserId == userId).ToListAsync();
                ViewBag.ReferenceNumbers = await _context.ReferenceNumbers
                                                        .Where(r => r.UserId == userId)
                                                        .GroupBy(r => r.SiteId)
                                                        .ToDictionaryAsync(g => g.Key, g => g.Select(x => x.ReferenceNumber).ToList());
                return View(model);
            }

            if (ModelState.IsValid)
            {
                model.UserId = userId;
                _context.Sites.Add(model);
                await _context.SaveChangesAsync();

                // After saving the site, you now have a SiteId
                var siteId = model.Id;

                // Add reference numbers associated with the site
                if (ReferenceNumbers != null && ReferenceNumbers.Any())
                {
                    foreach (var refNum in ReferenceNumbers)
                    {
                        var reference = new ReferenceNum
                        {
                            ReferenceNumber = refNum,
                            SiteId = siteId,
                            UserId = userId
                        };
                        _context.ReferenceNumbers.Add(reference);
                    }
                    await _context.SaveChangesAsync();
                }

                return RedirectToAction(nameof(AddSite)); // Refresh the page or redirect as needed
            }

            // Reload necessary data for the page if model state is not valid
            ViewBag.Sites = await _context.Sites.Where(s => s.UserId == userId).ToListAsync();
            ViewBag.ReferenceNumbers = new Dictionary<int, List<string>>(); // Optionally clear or re-load reference numbers
            return View(model);
        }



        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new IdentityUser { UserName = model.Email, Email = model.Email };
                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    var userDetails = new UserDetail { UserId = user.Id, FirstName = model.FirstName, LastName = model.LastName };
                    _context.UserDetails.Add(userDetails);
                    await _context.SaveChangesAsync();
                    await _signInManager.SignInAsync(user, isPersistent: false);

                    // Redirect to AddSite page
                    return RedirectToAction("AddSite", new { userId = user.Id });
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                    TempData["Error"] += error.Description + " "; // Storing error messages in TempData to show in the view
                }
            }
            // Collect all error messages from ModelState to display in the view if necessary
            TempData["ModelStateErrors"] = string.Join("; ", ModelState.Values
                                                                .SelectMany(v => v.Errors)
                                                                .Select(e => e.ErrorMessage));
            return View(model);
        }


        [HttpGet]
        [Authorize]
        public async Task<IActionResult> AddReferenceNumbers()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Fetch reference numbers and ensure non-null by filtering or providing a default value
            var referenceNumbers = await _context.ReferenceNumbers
                .Where(r => r.UserId == userId && r.ReferenceNumber != null) // Filtering out potential nulls
                .Select(r => r.ReferenceNumber ?? "") // Alternatively, provide a default value if null
                .ToListAsync();

            var model = new AddReferenceNumbersViewModel
            {
                UserId = userId,
                ReferenceNumbers = referenceNumbers // Now guaranteed to be non-null
            };

            return View(model);
        }


        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddReferenceNumbers(AddReferenceNumbersViewModel model)
        {
            if (ModelState.IsValid)
            {
                foreach (var referenceNumber in model.ReferenceNumbers)
                {
                    _context.ReferenceNumbers.Add(new ReferenceNum
                    {
                        ReferenceNumber = referenceNumber,
                        UserId = model.UserId
                    });
                }

                await _context.SaveChangesAsync();
                return RedirectToAction("AddReferenceNumbers");
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> EditBill(int id)
        {
            var bill = await _context.ElectricityBills.FindAsync(id);
            if (bill == null) return NotFound();

            var model = new BillCalculationViewModel
            {
                Id = bill.Id,
                PreviousReading = bill.PreviousReading,
                CurrentReading = bill.CurrentReading,
                Month = bill.Month,
                Year = bill.Year,
                SelectedReferenceNumber = bill.ReferenceNumber ?? "" // Safe handling by using an empty string if null
            };

            return View(model);
        }


        [HttpPost]
        public async Task<IActionResult> EditBill(BillCalculationViewModel model)
        {
            if (ModelState.IsValid)
            {
                var bill = await _context.ElectricityBills.FindAsync(model.Id);
                if (bill == null) return NotFound();

                bill.PreviousReading = model.PreviousReading;
                bill.CurrentReading = model.CurrentReading;
                bill.NetReading = model.CurrentReading - model.PreviousReading;
                bill.Month = model.Month;
                bill.Year = model.Year;
                bill.ReferenceNumber = model.SelectedReferenceNumber;

                await _context.SaveChangesAsync();
                return RedirectToAction("CalculateForm");
            }

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteBill(int id)
        {
            var bill = await _context.ElectricityBills.FindAsync(id);
            if (bill != null)
            {
                _context.ElectricityBills.Remove(bill);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("CalculateForm");
        }
    }
}
