using Elastic.Clients.Elasticsearch;
using iVault.Api.DTOs;

namespace iVault.Api.Services
{
    public class ElasticsearchService : IElasticsearchService
    {
        private readonly ElasticsearchClient _client;

        public ElasticsearchService(ElasticsearchClient client)
        {
            _client = client;
        }

        public async Task<IEnumerable<SearchRecordDto>> SearchRecordsAsync(string query)
        {
            // Use .Indices() instead of .Index()
            var response = await _client.SearchAsync<SearchRecordDto>(s => s
                .Indices("ivault-records")
                .Query(q => q
                    .QueryString(qs => qs
                        .Query($"*{query}*") // Search for your text
                    )
                )

                // 1. Sort determines which page "wins" the collapse
                // We sort by pageNumber ascending (0, 1, 2...) 
                .Sort(sort => sort
                    .Field(f => f.PageNumber, d => d.Order(SortOrder.Asc))
                )
                // 2. Collapse groups them by the RecordId
                .Collapse(c => c
                    .Field("recordId.keyword")
                )
                
                .From(0)
                .Size(20)
            );

            // In v8, .IsSuccess() or .IsValidResponse is the standard check
            if (!response.IsSuccess())
            {
                Console.WriteLine($"Search Failed: {response.DebugInformation}");
                return new List<SearchRecordDto>();
            }

            return response.Documents;
        }

        // Inside ElasticsearchService.cs implementation

        public async Task<bool> UpdateMetadataAsync(string id, Dictionary<string, object> metadata)
        {
            // Use the Update API to only change the 'metadata' field without touching the rest of the doc
            var response = await _client.UpdateAsync<SearchRecordDto, object>("ivault-records", id, u => u
                .Doc(new { metadata = metadata })
                .Refresh(Refresh.True)
            );

            return response.IsSuccess();
        }

        public async Task<SearchRecordDto?> GetRecordPageAsync(Guid recordId, int pageNumber)
        {
            var response = await _client.SearchAsync<SearchRecordDto>(s => s
                .Indices("ivault-records")
                .Query(q => q
                    .Bool(b => b
                        .Must(
                            // Convert recordId to string so the Value method can accept it
                            m => m.Term(t => t.Field(f => f.RecordId).Value(recordId.ToString())),
                            m => m.Term(t => t.Field(f => f.PageNumber).Value(pageNumber))
                        )
                    )
                )
                .Size(1)
            );

            return response.Documents.FirstOrDefault();
        }
    


    }
}