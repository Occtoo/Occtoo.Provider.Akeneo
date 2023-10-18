using Azure;
using Azure.Data.Tables;
using System;

namespace Occtoo.Akeneo.Function.Model
{
    public class LogEntity : ITableEntity
    {
        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public LogEntity()
        {
        }

        public LogEntity(string message, string type, string? data = null)
        {
            Message = message;
            Type = type;
            if (data != null)
            {
                Data = data;
            }
        }

        public string Message { get; set; }
        public string Type { get; set; }
        public string Data { get; set; }
    }
}