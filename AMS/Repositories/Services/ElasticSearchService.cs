using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Configuration;
using Repositories.Models;

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
        public async Task<int> CreateIndexAsync()
        {
            var indexExistsResponse = await _client.Indices.ExistsAsync(_indexName);
            if (!indexExistsResponse.Exists)
            {
                var createIndexResponse = await _client.Indices.CreateAsync<t_Attendance>(index => index
                .Index(_indexName)
                .Mappings(mappings => mappings
                .Properties(properties => properties
                .IntegerNumber(x => x.AttendId!)
                .IntegerNumber(x => x.EmpId)
                .Date(x => x.AttendDate)
                .IntegerNumber(x => x.ClockInHour)
                .IntegerNumber(x => x.ClockInMin)
                .IntegerNumber(x => x.ClockOutHour)
                .IntegerNumber(x => x.ClockOutMin)
                .IntegerNumber(x => x.WorkingHour)
                .Keyword(x => x.TaskType)
                .Keyword(x => x.WorkType)
                .Keyword(k => k.Name(f => f.Role))
                    .Keyword(k => k.Name(f => f.Status))
                    .Keyword(k => k.Name(f => f.AttendStatus))
                .Keyword(x => x.EmployeeName))
                )
                );
                if (!createIndexResponse.IsValidResponse)
                {
                    Console.WriteLine($"Failed to create index: {createIndexResponse.DebugInformation}");
                    return -1;
                }
                Console.WriteLine("Attendance index created successfully.");
                return 1;
            }
            else
            {
                Console.WriteLine("Attendance index already exists.");
                return 0;
            }
        }
        public async Task IndexAttendanceAsync(t_Attendance Attend)
        {
            var response = await _client.IndexAsync(Attend, idx => idx.Index(_indexName));
            if (!response.IsValidResponse)
            {
                throw new Exception($"Error indexing Attendance: {response.DebugInformation}");
            }
        }
        public async Task<List<t_Attendance?>> SearchAttendanceEmployeeNameAsync(string name)
        {
            var response = await _client.SearchAsync<t_Attendance>(s => s
            .Query(q => q.MatchPhrasePrefix(m => m
                .Field(f => f.EmployeeName)
                .Query(name)
            ))
            );
            if (response == null || response.Documents == null)
            {
                Console.WriteLine(" ElasticSearch query returned null or invalid response.");
                return null;
            }
            return response.Documents.ToList();
        }
    }
}