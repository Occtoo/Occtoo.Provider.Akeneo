using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Azure.Cosmos.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Container = Microsoft.Azure.Cosmos.Container;

namespace Occtoo.Akeneo.Function.Services;

public class CosmosDbService
{
    private readonly Container _container;

    public CosmosDbService(string endpointUrl, string primaryKey, string databaseId, string containerId)
    {
        var client = new CosmosClientBuilder(endpointUrl, primaryKey)
            .WithSerializerOptions(new CosmosSerializationOptions { PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase })
            .Build();


        var database = client.GetDatabase(databaseId);
        _container = database.GetContainer(containerId);
    }

    public async Task<ItemResponse<T>> UpsertItemAsync<T>(T item, PartitionKey partitionKey) where T : class
    {
        return await _container.UpsertItemAsync(item, partitionKey);
    }

    public async Task<ItemResponse<T>> DeleteItemAsync<T>(string id, PartitionKey partitionKey) where T : class
    {
        return await _container.DeleteItemAsync<T>(id, partitionKey);
    }

    public async Task<T?> GetByIdAsync<T>(string id, string partitionKey) where T : class
    {
        try
        {
            ItemResponse<T> response = await _container.ReadItemAsync<T>(id, new PartitionKey(partitionKey));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<List<T>> Get<T>(Expression<Func<T, bool>> predicate, PartitionKey partitionKey) where T : class
    {
        var queryable = _container.GetItemLinqQueryable<T>(true)
            .Where(predicate)
            .ToFeedIterator();

        var results = new List<T>();
        while (queryable.HasMoreResults)
        {
            var response = await queryable.ReadNextAsync();
            results.AddRange(response.Resource);
        }

        return results;
    }
}