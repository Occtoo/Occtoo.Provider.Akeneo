using CSharpFunctionalExtensions;
using Microsoft.Azure.Cosmos;
using Microsoft.DurableTask;
using Occtoo.Akeneo.Function.Domain;
using Occtoo.Akeneo.Function.Services;
using Occtoo.Functional.Extensions.Functional.Types;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace Occtoo.Akeneo.Function.Features.Activities;

[DurableTask(nameof(UpdateSynchronizationDetailsActivity))]
public class UpdateSynchronizationDetailsActivity : TaskActivity<UpdateSynchronizationDetailsActivity.Input, Result<None, DomainError>>
{
    private readonly CosmosDbService _cosmosDbService;

    public record Input(Guid TenantId, ImmutableList<DataSynchronizationDetails> DataSynchronizationDetails);

    public UpdateSynchronizationDetailsActivity(CosmosDbService cosmosDbService)
    {
        _cosmosDbService = cosmosDbService;
    }

    public override async Task<Result<None, DomainError>> RunAsync(TaskActivityContext context, Input input)
    {
        try
        {
            var connections = await _cosmosDbService.Get<AkeneoConnection>(q => q.TenantId == input.TenantId,
                new PartitionKey(input.TenantId.ToString()));

            var connection = connections.FirstOrDefault();
            if (connection == null)
            {
                return new NotFoundError("Connection not found");
            }

            await _cosmosDbService.UpsertItemAsync(
                connection with
                {
                    DataSynchronizationDetails =
                    connection.DataSynchronizationDetails.AddRange(input.DataSynchronizationDetails)
                },
                new PartitionKey(input.TenantId.ToString()));

            return None.Value;
        }
        catch (Exception ex)
        {
            return new DomainError(ex.Message);
        }
    }
}
