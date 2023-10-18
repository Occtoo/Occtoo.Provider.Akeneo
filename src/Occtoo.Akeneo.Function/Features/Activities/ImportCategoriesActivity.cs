using CSharpFunctionalExtensions;
using Microsoft.DurableTask;
using Occtoo.Akeneo.DataSync.Model;
using Occtoo.Akeneo.External.Api.Client;
using Occtoo.Akeneo.External.Api.Client.Model;
using Occtoo.Functional.Extensions;
using Occtoo.Functional.Extensions.Functional.Types;
using Occtoo.Onboarding.Sdk.Models;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Occtoo.Akeneo.Function.Features.Activities;

[DurableTask(nameof(ImportCategoriesActivity))]
public class ImportCategoriesActivity : TaskActivity<ImportCategoriesActivity.Input, Result<ImportCategoriesActivity.Output, DomainError>>
{
    private readonly IAkeneoApiClient _akeneoApiClient;


    public record Input(Guid TenantId,
        AuthorizedToken DataProviderToken,
        AkeneoAccessTokenDto AkeneoAccessToken,
        string PimUrl,
        DateTimeOffset? LastSynchronizationDate,
        string CategoryTree,
        string? NextUrl);

    public record Output(int IngestedAmount, string? NextUrl);

    public ImportCategoriesActivity(IAkeneoApiClient akeneoApiClient)
    {
        _akeneoApiClient = akeneoApiClient;
    }

    public override async Task<Result<Output, DomainError>> RunAsync(TaskActivityContext context, Input input)
    {
        try
        {
            var categories = GetCategories(input);
            var dynamicEntities = MapDynamicEntities(categories.Result.Value);
            var nextUrl = categories.Result.Value.Links.Next?.Href ?? string.Empty;
            int count = 0;
            if (dynamicEntities != null)
            {
                count = dynamicEntities.Count;
            }

            return new Output(count, nextUrl);
        }
        catch (Exception ex)
        {
            return new DomainError(ex.Message);
        }
    }

    private async Task<Result<AkeneoCategoriesDto, DomainError>> GetCategories(Input input)
    {
        if (input.NextUrl.NotEmpty())
        {
            return await _akeneoApiClient.GetNextPage<AkeneoCategoriesDto>(input.NextUrl!,
                input.AkeneoAccessToken.AccessToken);
        }

        return await _akeneoApiClient.GetCategories(input.PimUrl,
            input.AkeneoAccessToken.AccessToken,
            input.LastSynchronizationDate ?? Maybe<DateTimeOffset>.None,
            input.CategoryTree);
    }

    private static List<DynamicEntity> MapDynamicEntities(AkeneoCategoriesDto categories) =>
        categories.Embedded.Items.Select(category =>
            new DynamicEntity() { Key = category.Code, Delete = false, Properties = MapCategoryProperties(category).ToList() }).ToList();

    private static ImmutableList<DynamicProperty> MapCategoryProperties(Category category)
    {
        var localizedLabels = category.Labels.GroupBy(x => x.Key)
            .Select(group =>
                new DynamicProperty() { Id = nameof(category.Labels), Value = string.Join(";", group.Select(kv => kv.Value)), Language = group.Key });
        return ImmutableList<DynamicProperty>.Empty
            .AddRange(localizedLabels)
            .Add(new() { Id = nameof(category.Parent), Value = category.Parent, Language = null })
            .Add(new() { Id = nameof(category.Updated), Value = category.Updated.ToString(CultureInfo.InvariantCulture), Language = null });
    }
}


