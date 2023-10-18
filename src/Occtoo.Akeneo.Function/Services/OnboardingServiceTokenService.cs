using Azure;
using Azure.Data.Tables;
using Occtoo.Akeneo.Function.Model;
using Occtoo.Onboarding.Sdk;
using System;
using System.Threading.Tasks;

namespace Occtoo.Akeneo.Function.Services
{
    public class OnboardingServiceTokenService
    {
        private readonly IOnboardingServiceClient _onboardingServiceClient;
        private readonly TableServiceClient _tableServiceClient;
        private readonly TableClient _tableClient;
        private const string PartitionKey = "TokenPartition";

        public OnboardingServiceTokenService(string connectionString, string tableName)
        {
            _tableServiceClient = new TableServiceClient(connectionString);
            _tableClient = _tableServiceClient.GetTableClient(tableName);
            _onboardingServiceClient = new OnboardingServiceClient(
                Environment.GetEnvironmentVariable("OcctooDataProviderId"),
                Environment.GetEnvironmentVariable("OcctooDataProviderSecret"));
        }

        public async Task StoreTokenAsync(string tokenValue, DateTimeOffset expirationDate)
        {
            OnboardingServiceTokenEntity tokenEntity = new OnboardingServiceTokenEntity(tokenValue, expirationDate);
            tokenEntity.PartitionKey = PartitionKey;
            tokenEntity.RowKey = "TokenRow";

            await _tableClient.CreateIfNotExistsAsync();

            await _tableClient.DeleteEntityAsync(PartitionKey, "TokenRow");
            await _tableClient.AddEntityAsync(tokenEntity);
        }

        public async Task<OnboardingServiceTokenEntity> GetTokenAsync()
        {
            try
            {
                Response<OnboardingServiceTokenEntity> response = await _tableClient.GetEntityAsync<OnboardingServiceTokenEntity>(PartitionKey, "TokenRow");

                if (response.Value.ExpirationDate.UtcDateTime.AddMinutes(-10) <= DateTime.UtcNow)
                {
                    return await CreateNewTokenAsync();
                }

                return response.Value;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                // Handle not found (token not in the table) gracefully.
                return await CreateNewTokenAsync();
            }
        }

        private async Task<OnboardingServiceTokenEntity> CreateNewTokenAsync()
        {
            var token = await _onboardingServiceClient.GetTokenAsync();
            if (!string.IsNullOrEmpty(token))
            {
                DateTimeOffset tokenTimeOffset = DateTimeOffset.UtcNow.AddHours(1);
                await StoreTokenAsync(token, tokenTimeOffset);
                OnboardingServiceTokenEntity newToken = new OnboardingServiceTokenEntity(token, tokenTimeOffset);
                return newToken;
            }
            else
            {
                return null;
            }
        }
    }
}
