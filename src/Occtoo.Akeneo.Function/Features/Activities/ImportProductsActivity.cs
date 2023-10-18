using CSharpFunctionalExtensions;
using Microsoft.DurableTask;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Occtoo.Akeneo.DataSync.Model;
using Occtoo.Akeneo.External.Api.Client;
using Occtoo.Akeneo.External.Api.Client.Model;
using Occtoo.Akeneo.Function.Model;
using Occtoo.Akeneo.Function.Services;
using Occtoo.Functional.Extensions;
using Occtoo.Functional.Extensions.Functional.Types;
using Occtoo.Onboarding.Sdk;
using Occtoo.Onboarding.Sdk.Models;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace Occtoo.Akeneo.Function.Features.Activities;

[DurableTask(nameof(ImportProductsActivity))]
public class ImportProductsActivity : TaskActivity<ImportProductsActivity.Input, Result<ImportProductsActivity.Output, DomainError>>
{
    private readonly IAkeneoApiClient _akeneoApiClient;
    private readonly IOnboardingServiceClient _onboardingServiceClient;
    private readonly string[] _selectAttributes = { "pim_catalog_simpleselect", "pim_catalog_multiselect" };
    private readonly string[] _fileAttributes = { "pim_catalog_asset_collection" };
    private readonly string[] _propertiesToAddToPackshotsMedia = { "brand", "ecommerce_category", "generic_colour", "matStruct02", "matStruct03", "season", "sexe" };
    private readonly string[] _propertiesToAddToDetailshotsMedia = { "brand", "ecommerce_category", "generic_colour", "matStruct02", "matStruct03", "season", "sexe" };
    private readonly string[] _propertiesToAddToActionshotsMedia = { "brand", "ecommerce_category", "matStruct02", "matStruct03", "season", "sexe" };

    public record Input(Guid TenantId,
        AuthorizedToken DataProviderToken,
        AkeneoAccessTokenDto AkeneoAccessToken,
        string PimUrl,
        DateTimeOffset? LastSynchronizationDate,
        string ChannelCode,
        string? NextUrl);

    private record AttributeProperties(string AttributeType, List<AttributeOptions> Options);

    private record AttributeOptions(string ItemCode, ImmutableDictionary<string, string> Labels);

    public record Output(int IngestedAmount, string? NextUrl, ImmutableList<MediaDataToOnboard> EntryIdToDownloadLink);

    public ImportProductsActivity(IAkeneoApiClient akeneoApiClient, IOnboardingServiceClient onboardingServiceClient)
    {
        _akeneoApiClient = akeneoApiClient;
        _onboardingServiceClient = onboardingServiceClient;
    }

    public override async Task<Result<Output, DomainError>> RunAsync(TaskActivityContext context, Input input)
    {
        try
        {
            var products = await GetProducts(input);
            var categories = await _akeneoApiClient.GetCategories2(input.PimUrl, input.AkeneoAccessToken.AccessToken, input.ChannelCode);
            var attributes = await _akeneoApiClient.GetAttributes(input.PimUrl, input.AkeneoAccessToken.AccessToken,
                products.Value.Embedded.Items.SelectMany(x => x.Values.Keys).Distinct().ToImmutableList());
            var attributeCodesToProperties = await MapAttributeCodesToProperties(attributes.Value, input);
            var dynamicEntities = await PrepareEntities(products.Value.Embedded.Items.ToList(), categories.Value.Embedded.Items.ToList(), attributeCodesToProperties.Value, input);

            // Akeneo treats the attributes on given product as attribute types.
            // And out of attribute types, they derive values.
            // Might be helpful: https://miro.com/app/board/uXjVPgyxMuM=/
            int count = 0;
            if (dynamicEntities.Item1 != null)
            {
                OnboardingServiceTokenService onboardingServiceTokenService = new OnboardingServiceTokenService(Environment.GetEnvironmentVariable("AzureWebJobsStorage"), Environment.GetEnvironmentVariable("OnboardingServiceTokenTableName"));
                var tokenEntity = await onboardingServiceTokenService.GetTokenAsync();
                var response = await _onboardingServiceClient.StartEntityImportAsync(Constants.ProductsDataSourceId, dynamicEntities.Item1, tokenEntity.Token);
                if (response.StatusCode == 202)
                {
                    // Data was onboarded!
                    count = dynamicEntities.Item1.Count();
                }
            }

            return new Output(count, products.Value.Links?.Next?.Href, dynamicEntities.Item2.ToImmutableList());
        }
        catch (Exception ex)
        {
            return new DomainError(ex.Message);
        }
    }

    private async Task<Result<AkeneoProductsDto, DomainError>> GetProducts(Input input)
    {
        if (input.NextUrl.NotEmpty())
        {
            return await _akeneoApiClient.GetNextPage<AkeneoProductsDto>(input.NextUrl!,
                input.AkeneoAccessToken.AccessToken);
        }

        return await _akeneoApiClient.GetProducts(input.PimUrl,
            input.AkeneoAccessToken.AccessToken,
            input.ChannelCode,
            input.LastSynchronizationDate.ToMaybe());
    }

    private async Task<Result<AkeneoProductModelsDto, DomainError>> GetProductModels(Input input)
    {
        if (input.NextUrl.NotEmpty())
        {
            return await _akeneoApiClient.GetNextPage<AkeneoProductModelsDto>(input.NextUrl!,
                input.AkeneoAccessToken.AccessToken);
        }

        return await _akeneoApiClient.GetProductModels(input.PimUrl,
            input.AkeneoAccessToken.AccessToken,
            input.ChannelCode,
            input.LastSynchronizationDate.ToMaybe());
    }

    private async Task<Result<ImmutableDictionary<string, AttributeProperties>, DomainError>> MapAttributeCodesToProperties(AkeneoAttributesDto attributesRoot, Input input)
    {
        var attributeCodesToProperties = attributesRoot.Embedded.Items.ToDictionary(x => x.Code, x => new AttributeProperties(x.Type, new()));

        foreach (var attributeProperty in attributeCodesToProperties.Where(x =>
                     _selectAttributes.Contains(x.Value.AttributeType, StringComparer.InvariantCultureIgnoreCase)))
        {
            await _akeneoApiClient.GetAttributeOptions(input.PimUrl, input.AkeneoAccessToken.AccessToken, attributeProperty.Key)
                .Tap(attributeOptions =>
                    attributeProperty.Value.Options.AddRange(attributeOptions.Embedded.Items
                        .Select(x => new AttributeOptions(x.Code, x.Labels.ToImmutableDictionary()))));
        }

        return attributeCodesToProperties.ToImmutableDictionary();
    }

    private async Task<(List<DynamicEntity>, List<MediaDataToOnboard>)> PrepareEntities(List<Product> products, List<Category> categories, ImmutableDictionary<string, AttributeProperties> attributeCodesToProperties, Input input)
    {
        List<DynamicEntity> dynamicEntities = new List<DynamicEntity>();
        List<MediaDataToOnboard> mediaDataToOnboard = new List<MediaDataToOnboard>();

        foreach (Product product in products)
        {
            var propertiesAndDict = MapProductProperties(
                product.Identifier,
                product.Parent,
                product.Values,
                attributeCodesToProperties);

            List<DynamicProperty> properties = propertiesAndDict.Item1.ToList();
            string category = "";
            foreach (var channelCodeCategory in categories)
            {
                if (product.Categories.Contains(channelCodeCategory.Code))
                {
                    category = channelCodeCategory.Code;
                }
            }
            properties.Add(new DynamicProperty() { Id = input.ChannelCode + "_category", Value = category, Language = null });

            dynamicEntities.Add(new DynamicEntity() { Key = product.Identifier, Delete = false, Properties = properties });

            Dictionary<string, MediaFamilyData> mediaFamilyData = new Dictionary<string, MediaFamilyData>();

            List<string> packshotsAssetUrls = new List<string>();
            foreach (string assetCode in propertiesAndDict.Item2)
            {
                var asset = _akeneoApiClient.GetAssetFamilyAsset(input.PimUrl, input.AkeneoAccessToken.AccessToken, "variant_packshots", assetCode).Result;
                if (asset.Value != null)
                {
                    if (asset.Value.Asset.Media.FirstOrDefault()?.Links?.Download?.Href != null)
                    {
                        packshotsAssetUrls.Add(asset.Value.Asset.Media.First().Links.Download.Href);
                    }
                }
            }
            mediaFamilyData.Add("variant_packshots", new MediaFamilyData() { Properties = properties.Where(x => _propertiesToAddToPackshotsMedia.Contains(x.Id)).ToList(), Urls = packshotsAssetUrls });

            List<string> detailshotsAssetUrls = new List<string>();
            foreach (string assetCode in propertiesAndDict.Item3)
            {
                var asset = _akeneoApiClient.GetAssetFamilyAsset(input.PimUrl, input.AkeneoAccessToken.AccessToken, "detailshots", assetCode).Result;
                if (asset.Value != null)
                {
                    if (asset.Value.Asset.Media.FirstOrDefault()?.Links?.Download?.Href != null)
                    {
                        detailshotsAssetUrls.Add(asset.Value.Asset.Media.First().Links.Download.Href);
                    }
                }
            }
            mediaFamilyData.Add("detailshots", new MediaFamilyData() { Properties = properties.Where(x => _propertiesToAddToDetailshotsMedia.Contains(x.Id)).ToList(), Urls = detailshotsAssetUrls });

            List<string> actionshotsAssetUrls = new List<string>();
            foreach (string assetCode in propertiesAndDict.Item4)
            {
                var asset = _akeneoApiClient.GetAssetFamilyAsset(input.PimUrl, input.AkeneoAccessToken.AccessToken, "actionshots", assetCode).Result;
                if (asset.Value != null)
                {
                    if (asset.Value.Asset.Media.FirstOrDefault()?.Links?.Download?.Href != null)
                    {
                        actionshotsAssetUrls.Add(asset.Value.Asset.Media.First().Links.Download.Href);
                    }
                }
            }
            mediaFamilyData.Add("actionshots", new MediaFamilyData() { Properties = properties.Where(x => _propertiesToAddToActionshotsMedia.Contains(x.Id)).ToList(), Urls = actionshotsAssetUrls });

            mediaDataToOnboard.Add(new MediaDataToOnboard() { ProductId = product.Identifier, FamilyData = mediaFamilyData });
        }

        return (dynamicEntities, mediaDataToOnboard);
    }

    private async Task<(List<DynamicEntity>, List<MediaDataToOnboard>)> PrepareModelEntities(List<ProductModel> productModels, List<Category> categories, ImmutableDictionary<string, AttributeProperties> attributeCodesToProperties, Input input)
    {
        List<DynamicEntity> dynamicEntities = new List<DynamicEntity>();
        List<MediaDataToOnboard> mediaDataToOnboard = new List<MediaDataToOnboard>();

        foreach (ProductModel productModel in productModels)
        {
            var propertiesAndDict = MapProductProperties(
            productModel.Code,
            productModel.Parent,
            productModel.Values,
            attributeCodesToProperties);

            List<DynamicProperty> properties = propertiesAndDict.Item1.ToList();
            string category = "";
            foreach (var channelCodeCategory in categories)
            {
                if (productModel.Categories.Contains(channelCodeCategory.Code))
                {
                    category = channelCodeCategory.Code;
                }
            }
            properties.Add(new DynamicProperty() { Id = input.ChannelCode + "_category", Value = category, Language = null });

            dynamicEntities.Add(new DynamicEntity() { Key = productModel.Code, Delete = false, Properties = properties });

            Dictionary<string, MediaFamilyData> mediaFamilyData = new Dictionary<string, MediaFamilyData>();

            List<string> packshotsAssetUrls = new List<string>();
            foreach (string assetCode in propertiesAndDict.Item2)
            {
                var asset = _akeneoApiClient.GetAssetFamilyAsset(input.PimUrl, input.AkeneoAccessToken.AccessToken, "variant_packshots", assetCode).Result;
                if (asset.Value != null)
                {
                    if (asset.Value.Asset.Media.FirstOrDefault()?.Links?.Download?.Href != null)
                    {
                        packshotsAssetUrls.Add(asset.Value.Asset.Media.First().Links.Download.Href);
                    }
                }
            }
            mediaFamilyData.Add("variant_packshots", new MediaFamilyData() { Properties = properties.Where(x => _propertiesToAddToPackshotsMedia.Contains(x.Id)).ToList(), Urls = packshotsAssetUrls });

            List<string> detailshotsAssetUrls = new List<string>();
            foreach (string assetCode in propertiesAndDict.Item3)
            {
                var asset = _akeneoApiClient.GetAssetFamilyAsset(input.PimUrl, input.AkeneoAccessToken.AccessToken, "detailshots", assetCode).Result;
                if (asset.Value != null)
                {
                    if (asset.Value.Asset.Media.FirstOrDefault()?.Links?.Download?.Href != null)
                    {
                        detailshotsAssetUrls.Add(asset.Value.Asset.Media.First().Links.Download.Href);
                    }
                }
            }
            mediaFamilyData.Add("detailshots", new MediaFamilyData() { Properties = properties.Where(x => _propertiesToAddToDetailshotsMedia.Contains(x.Id)).ToList(), Urls = detailshotsAssetUrls });

            List<string> actionshotsAssetUrls = new List<string>();
            foreach (string assetCode in propertiesAndDict.Item4)
            {
                var asset = _akeneoApiClient.GetAssetFamilyAsset(input.PimUrl, input.AkeneoAccessToken.AccessToken, "actionshots", assetCode).Result;
                if (asset.Value != null)
                {
                    if (asset.Value.Asset.Media.FirstOrDefault()?.Links?.Download?.Href != null)
                    {
                        actionshotsAssetUrls.Add(asset.Value.Asset.Media.First().Links.Download.Href);
                    }
                }
            }
            mediaFamilyData.Add("actionshots", new MediaFamilyData() { Properties = properties.Where(x => _propertiesToAddToActionshotsMedia.Contains(x.Id)).ToList(), Urls = actionshotsAssetUrls });

            mediaDataToOnboard.Add(new MediaDataToOnboard() { ProductId = productModel.Code, FamilyData = mediaFamilyData });
        }

        return (dynamicEntities, mediaDataToOnboard);
    }

    private (ImmutableList<DynamicProperty>, ImmutableList<string>, ImmutableList<string>, ImmutableList<string>) MapProductProperties(string productIdentifier,
        string productParent,
        Dictionary<string, JToken> productAttributesToValues,
        IReadOnlyDictionary<string, AttributeProperties> attributeCodesToProperties)
    {
        string productSku = productIdentifier;

        var dynamicProperties = new List<DynamicProperty>()
        {
            new () { Id = "Code", Value = productSku, Language = null },
            new () { Id = "Parent", Value = productParent, Language = null }
        };

        var packshotsAssets = new List<string>();
        var detailshotsAssets = new List<string>();
        var actionshotsAssets = new List<string>();
        foreach (var attribute in productAttributesToValues)
        {
            var attributeProperties = attributeCodesToProperties[attribute.Key];
            foreach (var value in attribute.Value)
            {
                if (_selectAttributes.Contains(attributeProperties.AttributeType))
                {
                    dynamicProperties.AddRange(MapMultiSelectAttribute(value, attributeProperties, attribute.Key));
                }
                else if (_fileAttributes.Contains(attributeProperties.AttributeType))
                {
                    if (attribute.Key == "packshots")
                    {
                        if (value["data"] is JArray)
                        {
                            foreach (var assetCode in value["data"])
                            {
                                packshotsAssets.Add(assetCode.ToString());
                            }
                        }
                    }

                    if (attribute.Key == "detailshots")
                    {
                        if (value["data"] is JArray)
                        {
                            foreach (var assetCode in value["data"])
                            {
                                detailshotsAssets.Add(assetCode.ToString());
                            }
                        }
                    }

                    if (attribute.Key == "actionshots")
                    {
                        if (value["data"] is JArray)
                        {
                            foreach (var assetCode in value["data"])
                            {
                                actionshotsAssets.Add(assetCode.ToString());
                            }
                        }
                    }
                }
                else if (attributeProperties.AttributeType == "pim_catalog_metric")
                {
                    dynamicProperties.Add(new DynamicProperty() { Id = attribute.Key + "_amount", Value = value["data"].First.Last.ToString() });
                    dynamicProperties.Add(new DynamicProperty() { Id = attribute.Key + "_unit", Value = value["data"].Last.Last.ToString() });
                }
                else
                {
                    dynamicProperties.Add(MapDefaultAttribute(value, attribute.Key));
                }
            }
        }

        return (dynamicProperties.ToImmutableList(), packshotsAssets.ToImmutableList(), detailshotsAssets.ToImmutableList(), actionshotsAssets.ToImmutableList());
    }

    private static IEnumerable<DynamicProperty> MapMultiSelectAttribute(JToken value, AttributeProperties attributeProperties, string attributeCode)
    {
        var codes = value["data"] is JArray
            ? value["data"]!.ToObject<string[]>()
            : new[] { value["data"]!.Value<string>() };

        return attributeProperties.Options.Where(option => codes.Contains(option.ItemCode))
            .SelectMany(option => option.Labels)
            .GroupBy(option => option.Key)
            .Select(group => new DynamicProperty()
            {
                Id = attributeCode,
                Value = string.Join("|", group.Select(kv => kv.Value)),
                Language = group.Key
            })
            .ToList();
    }

    private static DynamicProperty MapDefaultAttribute(JToken value, string key)
    {
        return new()
        {
            Id = key,
            Value = (value["data"] is JArray ? String.Join('|', value["data"]) : (value["data"] is JObject ? JsonConvert.SerializeObject(value["data"]) : value["data"]!.Value<string>()!)),
            Language = value["locale"]!.Value<string>()
        };
    }
}

