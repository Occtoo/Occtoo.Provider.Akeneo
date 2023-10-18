using CSharpFunctionalExtensions;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Occtoo.Akeneo.DataSync.Model;
using Occtoo.Akeneo.Function.Domain;
using Occtoo.Akeneo.Function.Features.Activities;
using Occtoo.Functional.Extensions;
using Occtoo.Functional.Extensions.Functional.Types;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace Occtoo.Akeneo.Function.Features;

[DurableTask(nameof(DataSynchronizationStartOrchestration))]
public class DataSynchronizationStartOrchestration : TaskOrchestrator<DataSynchronizationStartOrchestration.Input, byte>
{
    public static string NamingStrategy(Guid tenantId, Guid dataProviderClientId) =>
        $"data-sync-start-{tenantId}-{dataProviderClientId}";

    public record Input(Guid TenantId,
        DataProvider DataProviderConfiguration,
        string PimUrl,
        string Username,
        string Password,
        string Base64ClientIdSecret,
        ImmutableDictionary<DataSynchronizationSource, DateTimeOffset?> LastSynchronizationDateForSource,
        ChannelConfiguration ChannelConfiguration,
        DataSyncRetryContext RetryContext,
        bool IsSynchronizationComplete = false,
        bool IsConnectionAlive = true);

    public override async Task<byte> RunAsync(TaskOrchestrationContext context, Input input)
    {
        var logger = context.CreateReplaySafeLogger<DataSynchronizationStartOrchestration>();
        if (!input.IsSynchronizationComplete)
        {
            var connectionStatus = await PerformSynchronizationTasks(context, input, logger);
            input = input with { IsConnectionAlive = connectionStatus };
        }

        var updateFlagActivityResult = await context.CallUpdateAkeneoConnectionFlagsActivityAsync(new UpdateAkeneoConnectionFlagsActivity.Input(input.TenantId, input.IsConnectionAlive));
        return updateFlagActivityResult.IsFailure ? await HandleFailureCases(context, input, logger) : Empty.Value;
    }

    private static async Task<bool> PerformSynchronizationTasks(TaskOrchestrationContext context, Input input, ILogger logger)
    {
        var subOrchestrationTasks = new List<Task<Result<None, DomainError>>>
        {
            context.CallImportCategoriesOrchestrationAsync(new ImportCategoriesOrchestration.Input(input.TenantId,
                input.DataProviderConfiguration,
                null,
                null,
                input.PimUrl,
                input.Username,
                input.Password,
                input.Base64ClientIdSecret,
                input.LastSynchronizationDateForSource.GetValueOrDefault(DataSynchronizationSource.Categories),
                input.ChannelConfiguration.CategoryTree, 
                null,
                DataSyncRetryContext.Empty(2, TimeSpan.FromSeconds(20)))),
            context.CallImportProductsOrchestrationAsync(new ImportProductsOrchestration.Input(input.TenantId,
                input.DataProviderConfiguration,
                null,
                null,
                input.PimUrl,
                input.Username,
                input.Password,
                input.Base64ClientIdSecret,
                input.LastSynchronizationDateForSource.GetValueOrDefault(DataSynchronizationSource.Products),
                input.ChannelConfiguration.ChannelCode,  
                null,
                DataSyncRetryContext.Empty(3, TimeSpan.FromSeconds(25))))
        };

        var subOrchestrationResults = await Task.WhenAll(subOrchestrationTasks);
        var synchronizationResult = subOrchestrationResults.CombineAll()
            .Ignore();

        if (synchronizationResult.IsFailure)
        {
            logger.LogError("Data synchronization failed for tenant: {tenantId} with error: {errorMessage}",
                input.TenantId,
                synchronizationResult.Error.Message);
        }

        return !(synchronizationResult.IsFailure &&
                 synchronizationResult.Error.Message.ContainsIgnoreCase(Constants.AkeneoUnauthorizedErrorMessage));
    }

    private static async Task<byte> HandleFailureCases(TaskOrchestrationContext context, Input input, ILogger logger)
    {
        if (input.RetryContext.CurrentAttempt < input.RetryContext.MaxNumberOfAttempts)
        {
            await context.CreateTimer(input.RetryContext.AttemptDelay, default);
            context.ContinueAsNew(input with { RetryContext = input.RetryContext.NextAttempt(), IsSynchronizationComplete = true });
            return Empty.Value;
        }

        logger.LogError("Akeneo flags update failed for tenant: {tenantId}", input.TenantId);
        context.SetCustomStatus($"Akeneo flags update failed for tenant: {input.TenantId}");
        return Empty.Value;
    }
}
