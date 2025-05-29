using dbcollector_api.Utils;
using System.Text;
using System.Text.Json;

namespace dbcollector_api.Services
{
    public class LicenseService
    {
        private readonly IWebHostEnvironment _env;

        public LicenseService(IWebHostEnvironment env)
        {
            _env = env;
        }

        public bool CanCreate()
        {
            string publicKeyPath = Path.Combine(_env.ContentRootPath, "publicKey.txt");
            return System.IO.File.Exists(publicKeyPath);
        }

        public async Task<(bool success, string message)> UploadLicenseAsync(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return (false, "未收到文件");

                using var reader = new StreamReader(file.OpenReadStream());
                string licenseContent = await reader.ReadToEndAsync();

                try
                {
                    var decryptedContent = RSAHelper.DecryptWithPrivateKey(licenseContent);
                    
                    // 解析证书内容
                    var licenseInfo = JsonSerializer.Deserialize<LicenseInfo>(decryptedContent);
                    
                    // 验证证书有效期
                    if (DateTime.Now < licenseInfo.StartDate)
                    {
                        return (false, "证书尚未生效");
                    }
                    
                    if (DateTime.Now > licenseInfo.EndDate)
                    {
                        return (false, "证书已过期");
                    }

                    // 验证通过，保存证书文件
                    var licensePath = Path.Combine(_env.ContentRootPath, "license.lic");
                    await System.IO.File.WriteAllTextAsync(licensePath, licenseContent);

                    return (true, "License验证成功并保存");
                }
                catch (Exception)
                {
                    return (false, "证书验证失败，请确保证书有效");
                }
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public byte[] CreateLicense(CreateLicenseRequest req)
        {
            try
            {
                var licenseContent = new LicenseInfo
                {
                    StartDate = req.StartDate,
                    EndDate = req.EndDate,
                    MaxStations = req.MaxStations,
                    AuthDays = req.AuthDays,
                    CreateTime = DateTime.Now
                };

                var jsonContent = JsonSerializer.Serialize(licenseContent);
                var publicKeyPath = Path.Combine(_env.ContentRootPath, "publicKey.txt");
                var publicKey = System.IO.File.ReadAllText(publicKeyPath);
                var encryptedContent = RSAHelper.EncryptWithPublicKey(jsonContent, publicKey);

                return Encoding.UTF8.GetBytes(encryptedContent);
            }
            catch (Exception ex)
            {
                throw new Exception("创建证书失败: " + ex.Message);
            }
        }

        public async Task<dynamic> GetLicenseInfoAsync()
        {
            try
            {
                var licensePath = Path.Combine(_env.ContentRootPath, "license.lic");
                if (!System.IO.File.Exists(licensePath))
                {
                    return new { status = false, message = "未安装授权证书" };
                }

                var licenseContent = await System.IO.File.ReadAllTextAsync(licensePath);

                var decryptedContent = RSAHelper.DecryptWithPrivateKey(licenseContent);
                var licenseInfo = JsonSerializer.Deserialize<LicenseInfo>(decryptedContent);

                return new
                {
                    status = true,
                    startDate = licenseInfo.StartDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    endDate = licenseInfo.EndDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    maxStations = licenseInfo.MaxStations,
                    authDays = licenseInfo.AuthDays,
                    createTime = licenseInfo.CreateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    isExpired = DateTime.Now > licenseInfo.EndDate
                };
            }
            catch (Exception ex)
            {
                return new { status = false, message = "证书验证失败: " + ex.Message };
            }
        }
    }

    public class LicenseInfo
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int MaxStations { get; set; }
        public int AuthDays { get; set; }
        public DateTime CreateTime { get; set; }
    }

    public class CreateLicenseRequest
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int MaxStations { get; set; }
        public int AuthDays { get; set; }
    }
}
