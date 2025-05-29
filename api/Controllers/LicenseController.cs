using dbcollector_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dbcollector_api.Controllers
{
    [Authorize]
    public class LicenseController : Controller
    {
        private readonly LicenseService _licenseService;

        public LicenseController(LicenseService licenseService)
        {
            _licenseService = licenseService;
        }

        public IActionResult CanCreate()
        {
            return Ok(_licenseService.CanCreate());
        }

        public async Task<IActionResult> Upload(IFormFile file)
        {
            var (success, message) = await _licenseService.UploadLicenseAsync(file);
            return Ok(new { success, message });
        }

        public IActionResult Create(CreateLicenseRequest req)
        {
            try
            {
                var licenseBytes = _licenseService.CreateLicense(req);
                return File(licenseBytes, "application/octet-stream", "license.lic");
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        public async Task<IActionResult> GetLicenseInfo()
        {
            var info = await _licenseService.GetLicenseInfoAsync();
            return Ok(info);
        }
    }
}
