using Azure;
using Azure.Data.Tables;
using System;

namespace Occtoo.Akeneo.Function.Model
{
    public class OnboardingServiceTokenEntity : ITableEntity
    {
        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public OnboardingServiceTokenEntity()
        {
        }

        public OnboardingServiceTokenEntity(String token, DateTimeOffset expirationDate)
        {
            Token = token;
            ExpirationDate = expirationDate;
        }

        public string Token { get; set; }
        public DateTimeOffset ExpirationDate { get; set; }
    }
}
