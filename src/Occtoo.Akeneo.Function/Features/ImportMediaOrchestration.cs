using CSharpFunctionalExtensions;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Occtoo.Akeneo.DataSync.Model;
using Occtoo.Akeneo.External.Api.Client.Model;
using Occtoo.Akeneo.Function.Features.Activities;
using Occtoo.Akeneo.Function.Model;
using Occtoo.Functional.Extensions.Functional.Types;
using System;
using System.Threading.Tasks;

namespace Occtoo.Akeneo.Function.Features;

[DurableTask(nameof(ImportMediaOrchestration))]
public class ImportMediaOrchestration : TaskOrchestrator<ImportMediaOrchestration.Input, Result<None, DomainError>>
{
    public record Input(Guid TenantId,
        AuthorizedToken DataProviderToken,
        AkeneoAccessTokenDto AkeneoAccessToken,
        string PimUrl,
        MediaDataToOnboard KeyToDownloadUrl,
        DataSyncRetryContext RetryContext);


    public override async Task<Result<None, DomainError>> RunAsync(TaskOrchestrationContext context, Input input)
    {
        var logger = context.CreateReplaySafeLogger<ImportMediaOrchestration>();

        Result<None, DomainError> importMedia = await context.CallImportMediaActivityAsync(new ImportMediaActivity.Input(input.TenantId,
            input.DataProviderToken,
            input.AkeneoAccessToken,
            input.PimUrl,
            input.KeyToDownloadUrl));

        if (importMedia.IsFailure)
        {
            if (input.RetryContext.CurrentAttempt < input.RetryContext.MaxNumberOfAttempts)
            {
                await context.CreateTimer(input.RetryContext.AttemptDelay, default);
                context.ContinueAsNew(input with { RetryContext = input.RetryContext.NextAttempt() });
                return None.Value;
            }

            logger.LogError("Importing media for tenant {tenantId} failed with internal error: {errorMessage}", input.TenantId, importMedia.Error.Message);
            context.SetCustomStatus($"Importing media for tenant: {input.TenantId} failed with internal error");
            return Result.Failure<None, DomainError>(new($"Importing media for tenant: {input.TenantId} failed with internal error: {importMedia.Error.Message}"));
        }

        return None.Value;

    }
}
