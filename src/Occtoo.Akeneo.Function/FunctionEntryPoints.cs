using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Newtonsoft.Json;
using Occtoo.Akeneo.DataSync.Model;
using Occtoo.Akeneo.External.Api.Client;
using Occtoo.Akeneo.Function.Domain;
using Occtoo.Akeneo.Function.Features;
using Occtoo.Akeneo.Function.Services;
using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Occtoo.Akeneo.Function;

public class FunctionEntryPoints
{
    private readonly CosmosDbService _cosmosDbService;
    private readonly IAkeneoApiClient _akeneoApiClient;

    public FunctionEntryPoints(CosmosDbService cosmosDbService,
        IAkeneoApiClient akeneoApiClient)
    {
        _cosmosDbService = cosmosDbService;
        _akeneoApiClient = akeneoApiClient;
    }

    [Function(nameof(FunctionEntryPoints) + "PrepareAkeneoConnection")]
    public async Task<HttpResponseData> PrepareAkeneoConnection(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "akeneo/connections")] HttpRequestData request,
        [DurableClient] DurableTaskClient client,
        CancellationToken cancellationToken
    )
    {
        var requestBody = await new StreamReader(request.Body).ReadToEndAsync(cancellationToken);
        var akeneoConnection = JsonConvert.DeserializeObject<AkeneoConnection>(requestBody);

        akeneoConnection.Username = Environment.GetEnvironmentVariable("AkeneoConnectionUsername");
        akeneoConnection.Password = Environment.GetEnvironmentVariable("AkeneoConnectionPassword");
        akeneoConnection.Base64ClientIdSecret = Environment.GetEnvironmentVariable("AkeneoConnectionBase64ClientIdSecret");

        var accessToken = await _akeneoApiClient.GetAccessTokenUserContext(akeneoConnection.PimUrl, akeneoConnection.Username, akeneoConnection.Password, akeneoConnection.Base64ClientIdSecret);
        var channels = await _akeneoApiClient.GetChannels(akeneoConnection.PimUrl, accessToken.Value.AccessToken);
        akeneoConnection.DataSources.Add(DataSynchronizationSource.Categories, "categories");
        akeneoConnection.DataProvider = new DataProvider(Guid.Parse(Environment.GetEnvironmentVariable("AkeneoConnectionDataProviderId")), Environment.GetEnvironmentVariable("AkeneoConnectionDataProviderSecret"));
        await _cosmosDbService.UpsertItemAsync(akeneoConnection with
        {
            ChannelConfiguration = new ChannelConfiguration()
            {
                CategoryTree = channels.Value.Embedded.Items.FirstOrDefault()?.CategoryTree,
                ChannelCode = channels.Value.Embedded.Items.FirstOrDefault()?.Code,
                ChannelName = channels.Value.Embedded.Items.FirstOrDefault()?.Labels["en_US"] // watch out for this one crashing your stuff
            }
        }, new PartitionKey(akeneoConnection.TenantId.ToString()));

        return request.CreateResponse(HttpStatusCode.OK);
    }

    [Function(nameof(FunctionEntryPoints) + "StartDataSynchronization")]
    public async Task<HttpResponseData> StartDataSynchronization(
        [HttpTrigger(AuthorizationLevel.Anonymous, Route = "akeneo/{tenantId:guid}/connections/sync")] HttpRequestData request,
        Guid tenantId,
        [DurableClient] DurableTaskClient client,
        CancellationToken cancellationToken
    )
    {
        var akeneoConnection = await _cosmosDbService.Get<AkeneoConnection>(
            connection => connection.TenantId == tenantId,
            new PartitionKey(tenantId.ToString()));

        var connection = akeneoConnection.FirstOrDefault();

        if (connection == null)
        {
            return request.CreateResponse(HttpStatusCode.BadRequest);
        }

        await client.ScheduleNewOrchestrationInstanceAsync("DataSynchronizationStartOrchestration", new DataSynchronizationStartOrchestration.Input(connection.TenantId,
                connection.DataProvider ?? new DataProvider(Guid.NewGuid(), "secret"),
                connection.PimUrl,
                connection.Username,
                connection.Password,
                connection.Base64ClientIdSecret,
                connection.DataSynchronizationDetails.GroupBy(x => x.DataSynchronizationSource)
                        .ToImmutableDictionary(group => group.Key,
                            group => group.LastOrDefault(x => x.Succeeded)?.LastSynchronizationDate),
                connection.ChannelConfiguration ?? new ChannelConfiguration(),
                DataSyncRetryContext.Empty(5, TimeSpan.FromSeconds(30))),
                new()
                {
                    InstanceId = DataSynchronizationStartOrchestration.NamingStrategy(connection.TenantId,
                        connection.DataProvider?.ClientId ?? Guid.NewGuid() )
                },
                cancellationToken);

        return request.CreateResponse(HttpStatusCode.OK);
    }
}
