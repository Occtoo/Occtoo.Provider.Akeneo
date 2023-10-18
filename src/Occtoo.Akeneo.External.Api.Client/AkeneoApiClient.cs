using CSharpFunctionalExtensions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Occtoo.Akeneo.External.Api.Client.Model;
using Occtoo.Functional.Extensions;
using Occtoo.Functional.Extensions.Functional.Types;
using System.Collections.Immutable;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace Occtoo.Akeneo.External.Api.Client;

public interface IAkeneoApiClient
{
    Task<Result<AkeneoAccessTokenDto, DomainError>> GetAccessTokenUserContext(string pimUrl, string username, string password, string base64ClientIdSecret);
    Task<Result<AkeneoAccessTokenDto, DomainError>> GetAccessToken(string pimUrl, string authorizationCode);
    Task<Result<AkeneoProductsDto, DomainError>> GetProducts(string pimUrl, string accessToken, string channel, Maybe<DateTimeOffset> lastRunDateTime);
    Task<Result<AkeneoProductModelsDto, DomainError>> GetProductModels(string pimUrl, string accessToken, string channel, Maybe<DateTimeOffset> lastRunDateTime);
    Task<Result<AkeneoAttributesDto, DomainError>> GetAttributes(string pimUrl, string accessToken, ImmutableList<string> attributeCodes);
    Task<Result<AkeneoAttributeOptionsDto, DomainError>> GetAttributeOptions(string pimUrl, string accessToken, string attributeCode);
    Task<Result<AkeneoChannelsDto, DomainError>> GetChannels(string pimUrl, string accessToken);
    Task<Result<AkeneoAssetDto, DomainError>> GetAssetFamilyAsset(string pimUrl, string accessToken, string assetFamily, string assetCode);
    Task<Result<Stream, DomainError>> GetFile(string downloadUrl, string accessToken);
    Task<Result<AkeneoCategoriesDto, DomainError>> GetCategories(string pimUrl,
        string accessToken,
        Maybe<DateTimeOffset> lastRunDateTime,
        Maybe<string> parent);
    Task<Result<AkeneoCategoriesDto, DomainError>> GetCategories2(string pimUrl,
        string accessToken,
        Maybe<string> parent);

    Task<Result<T, DomainError>> GetNextPage<T>(string nextUrl, string accessToken) where T : class;
}

public class AkeneoApiClient : IAkeneoApiClient
{
    public const string CodeIdentifier = "VerySecretAndSecureString";
    private readonly AkeneoClientConfiguration _akeneoClientConfiguration;
    private static readonly JsonSerializerSettings SnakeCaseOptions = new()
    {
        ContractResolver = new DefaultContractResolver
        {
            NamingStrategy = new SnakeCaseNamingStrategy()
        }
    };

    public AkeneoApiClient(IOptions<AkeneoClientConfiguration> akeneoClientOptions)
    {
        _akeneoClientConfiguration = akeneoClientOptions.Value;
    }

    public async Task<Result<AkeneoAccessTokenDto, DomainError>> GetAccessTokenUserContext(string pimUrl, string username, string password, string base64ClientIdSecret)
    {
        var data = new
        {
            username,
            password,
            grant_type = "password"
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"{pimUrl}/api/oauth/v1/token")
        {
            Content = new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json"),
            Headers = { Authorization = new AuthenticationHeaderValue("Basic", base64ClientIdSecret) }
        };

        return await ExecuteRequest(request)
            .Bind(MapResponse)
            .Bind(resp => ParseResponse<AkeneoAccessTokenDto>(resp, SnakeCaseOptions));
    }

    public async Task<Result<AkeneoAccessTokenDto, DomainError>> GetAccessToken(string pimUrl, string authorizationCode)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{pimUrl}/connect/apps/v1/oauth2/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                {"client_id", _akeneoClientConfiguration.ClientId},
                {"code", authorizationCode},
                {"grant_type", "authorization_code"},
                {"code_identifier", CodeIdentifier},
                {"code_challenge", PrepareCodeChallenge()}
            })
        };

        return await ExecuteRequest(request)
            .Bind(MapResponse)
            .Bind(resp => ParseResponse<AkeneoAccessTokenDto>(resp, SnakeCaseOptions));
    }

    public async Task<Result<AkeneoProductsDto, DomainError>> GetProducts(string pimUrl, string accessToken, string channel, Maybe<DateTimeOffset> lastRunDateTime)
    {
        var timeQuery = lastRunDateTime.HasValue
            ? PrepareSearchQueryString("updated", ">", $"{lastRunDateTime.Value.ToString("yyyy-MM-dd HH:mm:ss").Enquote()}")
            : string.Empty;

        var request = CreateRequest(HttpMethod.Get, $"{pimUrl}/api/rest/v1/products?limit=100&scope={channel}&{timeQuery}", accessToken);

        return await ExecuteRequest(request)
            .Bind(MapResponse)
            .Bind(resp => ParseResponse<AkeneoProductsDto>(resp, SnakeCaseOptions));
    }

    public async Task<Result<AkeneoProductModelsDto, DomainError>> GetProductModels(string pimUrl, string accessToken, string channel, Maybe<DateTimeOffset> lastRunDateTime)
    {
        var request = CreateRequest(HttpMethod.Get, $"{pimUrl}/api/rest/v1/product-models?limit=100&scope={channel}&search={{\"parent\":[{{\"operator\":\"NOT EMPTY\"}}]}}", accessToken);

        return await ExecuteRequest(request)
            .Bind(MapResponse)
            .Bind(resp => ParseResponse<AkeneoProductModelsDto>(resp, SnakeCaseOptions));
    }

    public async Task<Result<AkeneoAttributesDto, DomainError>> GetAttributes(string pimUrl, string accessToken, ImmutableList<string> attributeCodes)
    {
        var attributeQuery = attributeCodes.Any()
            ? PrepareSearchQueryString("code", "IN", $"[{string.Join(',', attributeCodes.Select(x => x.Enquote()))}]")
            : string.Empty;

        var request = CreateRequest(HttpMethod.Get, $"{pimUrl}/api/rest/v1/attributes?limit=100&{attributeQuery}", accessToken);

        return await ExecuteRequest(request)
            .Bind(MapResponse)
            .Bind(resp => ParseResponse<AkeneoAttributesDto>(resp, SnakeCaseOptions));
    }

    public async Task<Result<AkeneoAttributeOptionsDto, DomainError>> GetAttributeOptions(string pimUrl, string accessToken, string attributeCode)
    {
        var request = CreateRequest(HttpMethod.Get, $"{pimUrl}/api/rest/v1/attributes/{attributeCode}/options?limit=100", accessToken);

        return await ExecuteRequest(request)
            .Bind(MapResponse)
            .Bind(resp => ParseResponse<AkeneoAttributeOptionsDto>(resp, SnakeCaseOptions));
    }

    public async Task<Result<AkeneoAssetDto, DomainError>> GetAssetFamilyAsset(string pimUrl, string accessToken, string assetFamily, string assetCode)
    {
        var request = CreateRequest(HttpMethod.Get, $"{pimUrl}/api/rest/v1/asset-families/{assetFamily}/assets/{assetCode}", accessToken);

        return await ExecuteRequest(request)
            .Bind(MapResponse)
            .Bind(resp => ParseResponse<AkeneoAssetDto>(resp, SnakeCaseOptions));
    }

    public async Task<Result<Stream, DomainError>> GetFile(string downloadUrl, string accessToken)
    {
        var request = CreateRequest(HttpMethod.Get, downloadUrl, accessToken);

        return await ExecuteRequest(request)
            .Bind(MapResponse)
            .Map(resp => resp.Content.ReadAsStreamAsync());
    }

    public async Task<Result<T, DomainError>> GetNextPage<T>(string nextUrl, string accessToken) where T : class
    {
        var request = CreateRequest(HttpMethod.Get, nextUrl, accessToken);

        return await ExecuteRequest(request)
            .Bind(MapResponse)
            .Bind(resp => ParseResponse<T>(resp, SnakeCaseOptions));
    }

    public async Task<Result<AkeneoCategoriesDto, DomainError>> GetCategories(string pimUrl,
        string accessToken,
        Maybe<DateTimeOffset> lastRunDateTime,
        Maybe<string> categoryTree)
    {
        var timeQuery = lastRunDateTime.HasValue
            ? PrepareSearchQueryString("updated", ">", $"{lastRunDateTime.Value.ToString("yyyy-MM-ddTHH:mm:ssZ").Enquote()}")
            : string.Empty;
        var parentQuery = categoryTree.HasValue
            ? PrepareSearchQueryString("parent", "=", $"{categoryTree.Value.Enquote()}")
            : string.Empty;

        var request = CreateRequest(HttpMethod.Get, $"{pimUrl}/api/rest/v1/categories?limit=100&{parentQuery}&{timeQuery}", accessToken);

        return await ExecuteRequest(request)
            .Bind(MapResponse)
            .Bind(resp => ParseResponse<AkeneoCategoriesDto>(resp, SnakeCaseOptions));
    }

    public async Task<Result<AkeneoCategoriesDto, DomainError>> GetCategories2(string pimUrl,
        string accessToken,
        Maybe<string> categoryTree)
    {
        var parentQuery = categoryTree.HasValue
            ? PrepareSearchQueryString("parent", "=", $"{categoryTree.Value.Enquote()}")
            : string.Empty;

        var request = CreateRequest(HttpMethod.Get, $"{pimUrl}/api/rest/v1/categories?limit=100&{parentQuery}", accessToken);

        return await ExecuteRequest(request)
            .Bind(MapResponse)
            .Bind(resp => ParseResponse<AkeneoCategoriesDto>(resp, SnakeCaseOptions));
    }

    public async Task<Result<AkeneoChannelsDto, DomainError>> GetChannels(string pimUrl, string accessToken)
    {
        var request = CreateRequest(HttpMethod.Get, $"{pimUrl}/api/rest/v1/channels", accessToken);

        return await ExecuteRequest(request)
            .Bind(MapResponse)
            .Bind(resp => ParseResponse<AkeneoChannelsDto>(resp, SnakeCaseOptions));
    }

    public async Task<Result<AkeneoAssetDto, DomainError>> GetReferenceEntities(string pimUrl, string accessToken, string referenceEntities)
    {
        var request = CreateRequest(HttpMethod.Get, $"{pimUrl}/api/rest/v1/reference-entities/{referenceEntities}/records", accessToken);

        return await ExecuteRequest(request)
            .Bind(MapResponse)
            .Bind(resp => ParseResponse<AkeneoAssetDto>(resp, SnakeCaseOptions));
    }

    private string PrepareCodeChallenge()
    {
        var dataToHash = $"{CodeIdentifier}{_akeneoClientConfiguration.Secret}";
        var hashData = SHA256.HashData(Encoding.UTF8.GetBytes(dataToHash));

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < hashData.Length; i++)
        {
            builder.Append(hashData[i].ToString("x2"));
        }
        return builder.ToString();
    }

    private static HttpRequestMessage CreateRequest(HttpMethod httpMethod, string url, string accessToken)
    {
        var request = new HttpRequestMessage(httpMethod, url);
        request.Headers.Add("Authorization", "Bearer " + accessToken);
        return request;
    }

    private static Result<HttpResponseMessage, DomainError> MapResponse(HttpResponseMessage message)
        => message.StatusCode switch
        {
            HttpStatusCode.OK => message,
            HttpStatusCode.Created => message,
            HttpStatusCode.Unauthorized => Result.Failure<HttpResponseMessage, DomainError>(
                new UnauthorizedError("Access to akeneo service denied.")),
            HttpStatusCode.Forbidden => Result.Failure<HttpResponseMessage, DomainError>(
                new UnauthorizedError("Access to akeneo service denied.")),
            HttpStatusCode.BadRequest => Result.Failure<HttpResponseMessage, DomainError>(
                new ValidationError("Validation error occurred on akeneo side")),
            _ => Result.Failure<HttpResponseMessage, DomainError>(
                new UnknownError("Akeneo service does not indicate success"))
        };

    private static async Task<Result<T, DomainError>> ParseResponse<T>(HttpResponseMessage message,
        JsonSerializerSettings? settings = null)
    {
        try
        {
            var content = await message.Content.ReadAsStringAsync();
            var value = JsonConvert.DeserializeObject<T>(content, settings);
            return value is not null
                ? Result.Success<T, DomainError>(value)
                : Result.Failure<T, DomainError>(new("Response deserialized to null value"));
        }
        catch (JsonException)
        {
            return Result.Failure<T, DomainError>(new("Failed to parse response"));
        }
    }

    private static async Task<Result<HttpResponseMessage, DomainError>> ExecuteRequest(HttpRequestMessage httpRequest)
    {
        try
        {
            var response = await new HttpClient().SendAsync(httpRequest);
            return response;
        }
        catch (HttpRequestException)
        {
            return Result.Failure<HttpResponseMessage, DomainError>(new("An issue occurred when communicating with Akeneo external api"));
        }
    }

    private static string PrepareSearchQueryString(string queryBy, string queryOperator, string queryValue) =>
        $"search={{{queryBy.Enquote()}:[{{\"operator\":{queryOperator.Enquote()},\"value\":{queryValue}}}]}}";
}
