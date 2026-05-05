using Microsoft.AspNetCore.Http;

namespace iVault.Api.Services
{
    public interface IFileStorageService
    {
        Task<string> UploadFileAsync(IFormFile file, string customPath);
    }
}