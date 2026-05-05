using System.Net.Http;

namespace iVault.Api.Services
{
    public class SeaweedStorageService : IFileStorageService
    {
        private readonly HttpClient _httpClient;
        private readonly string _seaweedUrl = "http://localhost:9000"; // Your Filer URL

        public SeaweedStorageService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<string> UploadFileAsync(IFormFile file, string customPath)
        {
            if (file == null || file.Length == 0)
            {
                throw new ArgumentException("File is empty or null");
            }

            using var fileStream = file.OpenReadStream();

            // We use StreamContent for a direct PUT request. 
            // This is more efficient for SeaweedFS than MultipartFormData.
            using var streamContent = new StreamContent(fileStream);

            // Explicitly set the content type from the uploaded file
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);

            // The customPath already starts with /ivault/xx/xx/guid_name.pdf
            // We trim any double slashes just in case.
            var requestUrl = $"{_seaweedUrl.TrimEnd('/')}/{customPath.TrimStart('/')}";

            // PUT to the Filer API. SeaweedFS will auto-create the directory structure.
            var response = await _httpClient.PutAsync(requestUrl, streamContent);

            if (!response.IsSuccessStatusCode)
            {
                var errorDetails = await response.Content.ReadAsStringAsync();
                throw new Exception($"iVault Storage Error (Status: {response.StatusCode}): {errorDetails}");
            }

            // Return the path exactly as it was stored so the Controller can save it to PostgreSQL
            return customPath;
        }
    }
}