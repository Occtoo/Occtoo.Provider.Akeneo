using Azure.Data.Tables;
using Occtoo.Akeneo.Function.Model;
using System;
using System.Threading.Tasks;

namespace Occtoo.Akeneo.Function.Services
{
    public class LogService
    {
        private readonly TableServiceClient _tableServiceClient;
        private readonly TableClient _tableClient;

        public LogService(string connectionString, string tableName)
        {
            _tableServiceClient = new TableServiceClient(connectionString);
            _tableClient = _tableServiceClient.GetTableClient(tableName);
        }

        public async Task StoreLogAsync(string message, string type, string? data = null)
        {
            LogEntity logEntity = new LogEntity(message, type, data);
            logEntity.PartitionKey = DateTime.UtcNow.ToString("yyyy-MM-dd");
            logEntity.RowKey = Guid.NewGuid().ToString();

            await _tableClient.CreateIfNotExistsAsync();

            await _tableClient.AddEntityAsync(logEntity);
        }
    }
}
