using iVault.Api.DTOs;

namespace iVault.Api.Services
{
    public interface IElasticsearchService
    {
        // This method will only query Elasticsearch and return 
        // high-level data for the results table.
        Task<IEnumerable<SearchRecordDto>> SearchRecordsAsync(string query);

        // For partial updates to metadata
        Task<bool> UpdateMetadataAsync(string id, Dictionary<string, object> metadata);

        // To fetch specific pages (like Page 1) when Page 0 is empty
        Task<SearchRecordDto?> GetRecordPageAsync(Guid recordId, int pageNumber);


    }
}