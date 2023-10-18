using CSharpFunctionalExtensions;
using Microsoft.Azure.Cosmos;
using Microsoft.DurableTask;
using Occtoo.Akeneo.Function.Domain;
using Occtoo.Akeneo.Function.Services;
using Occtoo.Functional.Extensions.Functional.Types;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Occtoo.Akeneo.Function.Features.Activities;

[DurableTask(nameof(UpdateAkeneoConnectionFlagsActivity))]
public class UpdateAkeneoConnectionFlagsActivity : TaskActivity<UpdateAkeneoConnectionFlagsActivity.Input, Result<None, DomainError>>
{
    private readonly CosmosDbService _cosmosDbService;

    public record Input(Guid TenantId, bool IsAlive = true);

    public UpdateAkeneoConnectionFlagsActivity(CosmosDbService cosmosDbService)
    {
        _cosmosDbService = cosmosDbService;
    }

    public override async Task<Result<None, DomainError>> RunAsync(TaskActivityContext context, Input input)
    {
        try
        {
            var akeneoConnection = await _cosmosDbService.Get<AkeneoConnection>(
                q => q.TenantId == input.TenantId,
                new PartitionKey(input.TenantId.ToString()));

            var connection = akeneoConnection.FirstOrDefault();

            if (connection == null)
            {
                return new NotFoundError("Connection not found");
            }

            await _cosmosDbService.UpsertItemAsync(connection with { IsSynchronizing = false, IsAlive = input.IsAlive },
                new PartitionKey(connection.TenantId.ToString()));

            return None.Value;
        }
        catch (Exception ex)
        {
            return new DomainError(ex.Message);
        }
    }
}
