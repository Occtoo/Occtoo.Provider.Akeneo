using CSharpFunctionalExtensions;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Occtoo.Akeneo.DataSync.Model;
using Occtoo.Akeneo.External.Api.Client.Model;
using Occtoo.Akeneo.Function.Domain;
using Occtoo.Akeneo.Function.Features.Activities;
using Occtoo.Functional.Extensions;
using Occtoo.Functional.Extensions.Functional.Types;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace Occtoo.Akeneo.Function.Features;

[DurableTask(nameof(ImportProductsOrchestration))]
public class ImportProductsOrchestration : TaskOrchestrator<ImportProductsOrchestration.Input, Result<None, DomainError>>
{
    public record Input(Guid TenantId,
        DataProvider DataProviderConfiguration,
        AuthorizedToken? DataProviderToken,
        AkeneoAccessTokenDto? AkeneoAccessTokenDto,
        string PimUrl,
        string Username,
        string Password,
        string Base64ClientIdSecret,
        DateTimeOffset? LastSynchronizationDate,
        string ChannelCode,
        string? NextUrl,
        DataSyncRetryContext RetryContext,
        int IngestedProductsAmount = 0,
        int IngestedMediaAmount = 0);


    public override async Task<Result<None, DomainError>> RunAsync(TaskOrchestrationContext context, Input input)
    {
        var logger = context.CreateReplaySafeLogger<ImportProductsOrchestration>();

        if (input.AkeneoAccessTokenDto == null ||
            DateTimeOffset.UtcNow.AddSeconds(input.AkeneoAccessTokenDto.ExpirationTime) < DateTimeOffset.UtcNow.AddMinutes(15))
        {
            var akeneoToken = await context.CallRetrieveAkeneoUserAuthTokenActivityAsync(
                new RetrieveAkeneoUserAuthTokenActivity.Input(input.PimUrl,
                    input.Username,
                    input.Password,
                    input.Base64ClientIdSecret));

            if (akeneoToken.IsFailure)
            {
                return await HandleFailureCases(context, input, logger, $"Getting token out of akeneo failed: {akeneoToken.Error.Message}");
            }

            input = input with { AkeneoAccessTokenDto = akeneoToken.Value };
        }

        var importProductsOutput = await context.CallImportProductsActivityAsync(new ImportProductsActivity.Input(input.TenantId,
            input.DataProviderToken,
            input.AkeneoAccessTokenDto,
            input.PimUrl,
            input.LastSynchronizationDate,
            input.ChannelCode,
            input.NextUrl));

        if (importProductsOutput.IsFailure)
        {
            return await HandleFailureCases(context, input, logger,
                $"Importing products for tenant: {input.TenantId} failed with internal error: {importProductsOutput.Error.Message}");
        }

        List<Task<Result<None, DomainError>>> mediaDownloadTasks = new List<Task<Result<None, DomainError>>>();
        foreach (var downloadLink in importProductsOutput.Value.EntryIdToDownloadLink)
        {
            mediaDownloadTasks.Add(context.CallImportMediaOrchestrationAsync(new ImportMediaOrchestration.Input(input.TenantId,
                input.DataProviderToken,
                input.AkeneoAccessTokenDto,
                input.PimUrl,
                downloadLink,
                DataSyncRetryContext.Empty(2, TimeSpan.FromSeconds(10)))));
        }

        await Task.WhenAll(mediaDownloadTasks);
        var importMediaOutput = await mediaDownloadTasks.CombineAll();
        if (importMediaOutput.IsFailure)
        {
            return await HandleFailureCases(context, input, logger,
                $"Importing media for tenant: {input.TenantId} failed with internal error: {importMediaOutput.Error.Message}",
                importProductsOutput.Value.IngestedAmount);
        }

        if (importProductsOutput.Value.NextUrl.NotEmpty())
        {
            context.ContinueAsNew(input with
            {
                NextUrl = importProductsOutput.Value.NextUrl,
                IngestedProductsAmount =
                input.IngestedProductsAmount + importProductsOutput.Value.IngestedAmount,
                IngestedMediaAmount = input.IngestedMediaAmount +
                                      importProductsOutput.Value.EntryIdToDownloadLink.Count
            });
            return None.Value;
        }

        if (input.IngestedProductsAmount + importProductsOutput.Value.IngestedAmount > 0)
        {
            var updateProductsSyncDetails = await context.CallUpdateSynchronizationDetailsActivityAsync(new UpdateSynchronizationDetailsActivity.Input(input.TenantId,
                ImmutableList<DataSynchronizationDetails>.Empty.Add(
                    new(DateTimeOffset.UtcNow,
                        input.IngestedProductsAmount + importProductsOutput.Value.IngestedAmount,
                        DataSynchronizationType.Manual,
                        DataSynchronizationSource.Products,
                        true))));

            if (updateProductsSyncDetails.IsFailure)
            {
                return await HandleFailureCases(context, input, logger,
                    $"Importing products succeeded for tenant: {input.TenantId} " +
                    $"but problem occurred when updating connection. Error: {updateProductsSyncDetails.Error.Message}",
                    importProductsOutput.Value.IngestedAmount,
                    importProductsOutput.Value.EntryIdToDownloadLink.Count);
            }
        }

        if (input.IngestedMediaAmount + importProductsOutput.Value.EntryIdToDownloadLink.Count > 0)
        {
            var updateMediaSyncDetails = await context.CallUpdateSynchronizationDetailsActivityAsync(new UpdateSynchronizationDetailsActivity.Input(input.TenantId,
                ImmutableList<DataSynchronizationDetails>.Empty.Add(
                    new(DateTimeOffset.UtcNow,
                        input.IngestedMediaAmount + importProductsOutput.Value.EntryIdToDownloadLink.Count,
                        DataSynchronizationType.Manual,
                        DataSynchronizationSource.Media,
                        true))));

            if (updateMediaSyncDetails.IsFailure)
            {
                return await HandleFailureCases(context, input, logger,
                    $"Importing products succeeded for tenant: {input.TenantId} " +
                    $"but problem occurred when updating connection. Error: {updateMediaSyncDetails.Error.Message}",
                    importProductsOutput.Value.IngestedAmount,
                    importProductsOutput.Value.EntryIdToDownloadLink.Count);
            }
        }

        return None.Value;
    }

    private static async Task<Result<None, DomainError>> HandleFailureCases(TaskOrchestrationContext context,
        Input input,
        ILogger logger,
        string errorMessage,
        int ingestedProductAmountInThisRun = 0,
        int ingestedMediaAmountInThisRun = 0)
    {
        if (input.RetryContext.CurrentAttempt < input.RetryContext.MaxNumberOfAttempts)
        {
            await context.CreateTimer(input.RetryContext.AttemptDelay, default);
            context.ContinueAsNew(input with { RetryContext = input.RetryContext.NextAttempt() });
            return None.Value;
        }

        ImmutableList<DataSynchronizationDetails> synchronizationDetails = ImmutableList<DataSynchronizationDetails>.Empty
            .Add(new(DateTimeOffset.UtcNow, input.IngestedProductsAmount + ingestedProductAmountInThisRun, DataSynchronizationType.Manual, DataSynchronizationSource.Products, false))
            .Add(new(DateTimeOffset.UtcNow, input.IngestedMediaAmount + ingestedMediaAmountInThisRun, DataSynchronizationType.Manual, DataSynchronizationSource.Media, false));

        _ = await context.CallUpdateSynchronizationDetailsActivityAsync(new UpdateSynchronizationDetailsActivity.Input(
            input.TenantId,
            synchronizationDetails));

        logger.LogError(errorMessage);
        context.SetCustomStatus($"Failure occurred while synchronizing product data for tenant: {input.TenantId}");
        return Result.Failure<None, DomainError>(new(errorMessage));
    }
}
