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
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace Occtoo.Akeneo.Function.Features;

[DurableTask(nameof(ImportCategoriesOrchestration))]
public class ImportCategoriesOrchestration : TaskOrchestrator<ImportCategoriesOrchestration.Input, Result<None, DomainError>>
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
        string CategoryTree,
        string? NextUrl,
        DataSyncRetryContext RetryContext,
        int IngestedAmount = 0);


    public override async Task<Result<None, DomainError>> RunAsync(TaskOrchestrationContext context, Input input)
    {
        var logger = context.CreateReplaySafeLogger<ImportCategoriesOrchestration>();

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

        var importCategoriesOutput = await context.CallImportCategoriesActivityAsync(new ImportCategoriesActivity.Input(input.TenantId,
            input.DataProviderToken!,
            input.AkeneoAccessTokenDto,
            input.PimUrl,
            input.LastSynchronizationDate,
            input.CategoryTree,
            input.NextUrl));

        if (importCategoriesOutput.IsFailure)
        {
            return await HandleFailureCases(context, input, logger,
                $"Importing categories for tenant: {input.TenantId} failed with internal error: {importCategoriesOutput.Error.Message}");
        }

        if (importCategoriesOutput.Value.NextUrl.NotEmpty())
        {
            context.ContinueAsNew(input with
            {
                NextUrl = importCategoriesOutput.Value.NextUrl,
                IngestedAmount = input.IngestedAmount + importCategoriesOutput.Value.IngestedAmount
            });
            return None.Value;
        }

        if (input.IngestedAmount + importCategoriesOutput.Value.IngestedAmount > 0)
        {
            var updateResult = await context.CallUpdateSynchronizationDetailsActivityAsync(new UpdateSynchronizationDetailsActivity.Input(input.TenantId,
                ImmutableList<DataSynchronizationDetails>.Empty.Add(
                    new(DateTimeOffset.UtcNow,
                        input.IngestedAmount + importCategoriesOutput.Value.IngestedAmount,
                        DataSynchronizationType.Manual,
                        DataSynchronizationSource.Categories,
                        true))));

            if (updateResult.IsFailure)
            {
                return await HandleFailureCases(context, input, logger,
                    $"Importing categories succeeded for tenant: {input.TenantId} but problem occurred when updating connection. Error: {importCategoriesOutput.Error.Message}",
                    importCategoriesOutput.Value.IngestedAmount);
            }
        }

        return None.Value;
    }

    private static async Task<Result<None, DomainError>> HandleFailureCases(TaskOrchestrationContext context,
        Input input,
        ILogger logger,
        string errorMessage,
        int ingestedCategoriesInThisRun = 0)
    {
        if (input.RetryContext.CurrentAttempt < input.RetryContext.MaxNumberOfAttempts)
        {
            await context.CreateTimer(input.RetryContext.AttemptDelay, default);
            context.ContinueAsNew(input with { RetryContext = input.RetryContext.NextAttempt() });
            return None.Value;
        }

        _ = await context.CallUpdateSynchronizationDetailsActivityAsync(new UpdateSynchronizationDetailsActivity.Input(
            input.TenantId,
            ImmutableList<DataSynchronizationDetails>.Empty.Add(
                new(DateTimeOffset.UtcNow,
                    input.IngestedAmount + ingestedCategoriesInThisRun,
                    DataSynchronizationType.Manual,
                    DataSynchronizationSource.Categories,
                    false))));

        logger.LogError(errorMessage);
        context.SetCustomStatus($"Failure occurred while synchronizing categories data for tenant: {input.TenantId}");
        return Result.Failure<None, DomainError>(new(errorMessage));
    }
}
