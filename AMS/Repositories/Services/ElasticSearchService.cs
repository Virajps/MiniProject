using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Configuration;

namespace Repositories.Services
{
    public class ElasticSearchService
    {
        private readonly ElasticsearchClient _client;
        private string _indexName;
        public ElasticSearchService(IConfiguration configuration, ElasticsearchClient client)
        {
            _indexName = configuration["Elasticsearch:DefaultIndex"]; _client = client;
        }
    //     public async Task<int> CreateIndexAsync()
    //     {
    //         var indexExistsResponse = await _client.Indices.ExistsAsync(_indexName);
    //         if (!indexExistsResponse.Exists)
    //         {
    //             var createIndexResponse = await _client.Indices.CreateAsync<t_Contact>(index => index
    //             .Index(_indexName)
    //             .Mappings(mappings => mappings
    //             .Properties(properties => properties
    //             .IntegerNumber(x => x.c_ContactId!)
    //             .IntegerNumber(x => x.c_UserId)
    //             .Text(x => x.c_ContactName)
    //             .Keyword(x => x.c_Email)
    //             .Keyword(x => x.c_Address)
    //             .Keyword(x => x.c_Mobile)
    //             .Keyword(x => x.c_Status)
    //             .Keyword(x => x.c_Group)
    //             .Keyword(x => x.c_Image))
    //             )
    //             );
    //             if (!createIndexResponse.IsValidResponse)
    //             {
    //                 Console.WriteLine($"Failed to create index: {createIndexResponse.DebugInformation}");
    //                 return -1;
    //             }
    //             Console.WriteLine("Contacts index created successfully.");
    //             return 1;
    //         }
    //         else
    //         {
    //             Console.WriteLine("Contacts index already exists.");
    //             return 0;
    //         }
    //     }
    //     public async Task IndexContactAsync(t_Contact contact)
    //     {
    //         var response = await _client.IndexAsync(contact, idx => idx.Index(_indexName));
    //         if (!response.IsValidResponse)
    //         {
    //             throw new Exception($"Error indexing contact: {response.DebugInformation}");
    //         }
    //     }
    //     public async Task<List<t_Contact?>> SearchContactNameAsync(string name)
    //     {
    //         var response = await _client.SearchAsync<t_Contact>(s => s
    //         .Query(q => q.MatchPhrasePrefix(m => m
    //             .Field(f => f.c_ContactName)
    //             .Query(name)
    //         ))
    //         );
    //         if (response == null || response.Documents == null)
    //         {
    //             Console.WriteLine(" ElasticSearch query returned null or invalid response.");
    //             return null;
    //         }
    //         return response.Documents.ToList();
    //     }
    }
}