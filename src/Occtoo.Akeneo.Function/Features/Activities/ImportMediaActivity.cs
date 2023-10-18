using CSharpFunctionalExtensions;
using Microsoft.DurableTask;
using Occtoo.Akeneo.DataSync.Model;
using Occtoo.Akeneo.External.Api.Client;
using Occtoo.Akeneo.External.Api.Client.Model;
using Occtoo.Akeneo.Function.Model;
using Occtoo.Akeneo.Function.Services;
using Occtoo.Functional.Extensions.Functional.Types;
using Occtoo.Onboarding.Sdk;
using Occtoo.Onboarding.Sdk.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Occtoo.Akeneo.Function.Features.Activities;

[DurableTask(nameof(ImportMediaActivity))]
public class ImportMediaActivity : TaskActivity<ImportMediaActivity.Input, Result<None, DomainError>>
{
    private readonly IAkeneoApiClient _akeneoApiClient;
    private readonly LogService _logService;
    private readonly IOnboardingServiceClient _onboardingServiceClient;
    private readonly OnboardingServiceTokenService _onboardingServiceTokenService;

    public record Input(Guid TenantId,
        AuthorizedToken DataProviderToken,
        AkeneoAccessTokenDto AkeneoAccessToken,
        string PimUrl,
        MediaDataToOnboard KeyToDownloadUrl);

    public ImportMediaActivity(IAkeneoApiClient akeneoApiClient, IOnboardingServiceClient onboardingService)
    {
        _akeneoApiClient = akeneoApiClient;
        _onboardingServiceClient = onboardingService;
        _logService = new LogService(
            Environment.GetEnvironmentVariable("AzureWebJobsStorage"), 
            Environment.GetEnvironmentVariable("LogTableName"));
        _onboardingServiceTokenService = new OnboardingServiceTokenService(
            Environment.GetEnvironmentVariable("AzureWebJobsStorage"),
            Environment.GetEnvironmentVariable("OnboardingServiceTokenTableName"));
    }

    public override async Task<Result<None, DomainError>> RunAsync(TaskActivityContext context, Input input)
    {
        var mediaInfo = await GetMediaInfo(input);

        if (mediaInfo.Item1.Any())
        {
            var tokenEntity = await _onboardingServiceTokenService.GetTokenAsync();
            var response = await _onboardingServiceClient.StartEntityImportAsync(Constants.ProductsDataSourceId, mediaInfo.Item1, tokenEntity.Token);
            if (response.StatusCode == 202)
            {
                // Data was onboarded!
            }
        }

        if (mediaInfo.Item2.Any())
        {
            var tokenEntity = await _onboardingServiceTokenService.GetTokenAsync();
            var response = await _onboardingServiceClient.StartEntityImportAsync(Constants.MediaDataSourceId, mediaInfo.Item2, tokenEntity.Token);
            if (response.StatusCode == 202)
            {
                // Data was onboarded!
            }
        }

        return None.Value;
    }

    private async Task<(List<DynamicEntity>, List<DynamicEntity>)> GetMediaInfo(Input input)
    {
        List<DynamicEntity> productEntities = new List<DynamicEntity>();
        List<DynamicEntity> mediaEntities = new List<DynamicEntity>();
        List<MediaFileDto> mediaFiles = new List<MediaFileDto>();

        string thumbnailUrl = "";
        var tokenEntity = await _onboardingServiceTokenService.GetTokenAsync();

        foreach (var familyData in input.KeyToDownloadUrl.FamilyData)
        {
            string mediaFamilyType = familyData.Key;

            foreach (var url in familyData.Value.Urls)
            {
                var fileName = Path.GetFileName(url);
                var mimeType = MimeTypes.GetMimeType(fileName);
                var uniqueId = $"{mediaFamilyType}_{fileName}";

                try
                {
                    var existingMediaResponse = await _onboardingServiceClient.GetFileFromUniqueIdAsync(uniqueId, tokenEntity.Token);

                    if (existingMediaResponse.StatusCode == 200)
                    {
                        MediaFileDto existingMedia = existingMediaResponse.Result;
                        if (existingMedia != null)
                        {
                            mediaFiles.Add(existingMediaResponse.Result);
                            mediaEntities.Add(new DynamicEntity()
                            {
                                Key = existingMediaResponse.Result.Id,
                                Delete = false,
                                Properties = MapMediaProperties(existingMediaResponse.Result, uniqueId, mediaFamilyType, familyData.Value.Properties)
                            });
                            continue;
                        }
                    }

                    var file = await _akeneoApiClient.GetFile(url, input.AkeneoAccessToken.AccessToken);

                    if (file.IsSuccess)
                    {
                        var response = await _onboardingServiceClient.UploadFileIfNotExistAsync(file.Value, new UploadMetadata(fileName, mimeType, file.Value.Length) { UniqueIdentifier = uniqueId }, tokenEntity.Token);
                        if (response.StatusCode == 200)
                        {
                            mediaFiles.Add(response.Result);
                            mediaEntities.Add(new DynamicEntity()
                            {
                                Key = response.Result.Id,
                                Delete = false,
                                Properties = MapMediaProperties(response.Result, uniqueId, mediaFamilyType, familyData.Value.Properties)
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    string logData = "Stack: " + ex.StackTrace + Environment.NewLine + "FileName: " + fileName;
                    await _logService.StoreLogAsync(ex.Message, "MediaServiceError", logData);
                }
            }

            if (mediaFamilyType == "variant_packshots" && mediaFiles.Any())
            {
                thumbnailUrl = mediaFiles.First().PublicUrl + "?impolicy=small";
            }
        }


        if (mediaFiles.Any())
        {
            List<DynamicProperty> productMediaProperties = new List<DynamicProperty>() { new DynamicProperty() { Id = "media", Language = null, Value = string.Join('|', mediaFiles.Select(x => x.Id)) } };
            if (!string.IsNullOrEmpty(thumbnailUrl))
            {
                productMediaProperties.Add(new DynamicProperty() { Id = "thumbnail", Language = null, Value = mediaFiles.First().PublicUrl + "?impolicy=small" });
            }

            productEntities.Add(new DynamicEntity()
            {
                Key = input.KeyToDownloadUrl.ProductId,
                Delete = false,
                Properties = productMediaProperties
            });
        }

        return (productEntities, mediaEntities);
    }

    private static List<DynamicProperty> MapMediaProperties(MediaFileDto mediaFileDto, string uniqueId, string akeneo_asset_family, List<DynamicProperty> productLevelProperties)
    {
        var properties = new List<DynamicProperty>()
        {
            new () { Id = "fileId", Value = mediaFileDto.Id, Language = null },
            new () { Id = "uniqueId", Value = uniqueId, Language = null },
            new () { Id = "fileName", Value = mediaFileDto.Metadata.Filename, Language = null },
            new () { Id = "mimeType", Value = mediaFileDto.Metadata.MimeType, Language = null },
            new () { Id = "fileSize", Value = mediaFileDto.Metadata.Size.ToString(), Language = null },
            new () { Id = "containerName", Value = mediaFileDto.Location.ContainerName, Language = null },
            new () { Id = "url", Value = mediaFileDto.PublicUrl, Language = null },
            new () { Id = "thumbnail", Value = mediaFileDto.PublicUrl + "?impolicy=small", Language = null },
            new () { Id = "assetType", Value = akeneo_asset_family, Language = null }
        };

        if (mediaFileDto.Metadata.MediaInfo?.Image != null)
        {
            properties.Add(new() { Id = "height", Value = mediaFileDto.Metadata.MediaInfo.Image.Height.ToString(), Language = null });
            properties.Add(new() { Id = "width", Value = mediaFileDto.Metadata.MediaInfo.Image.Width.ToString(), Language = null });
            properties.Add(new() { Id = "horizontalResolution", Value = mediaFileDto.Metadata.MediaInfo.Image.Resolution.Horizontal.ToString(CultureInfo.InvariantCulture), Language = null });
            properties.Add(new() { Id = "verticalResolution", Value = mediaFileDto.Metadata.MediaInfo.Image.Resolution.Vertical.ToString(CultureInfo.InvariantCulture), Language = null });
        }

        foreach (DynamicProperty productLevelProperty in productLevelProperties)
        {
            properties.Add(productLevelProperty);
        }

        return properties;
    }
}


