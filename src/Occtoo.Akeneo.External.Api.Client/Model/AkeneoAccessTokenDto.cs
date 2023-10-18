using Newtonsoft.Json;

namespace Occtoo.Akeneo.External.Api.Client.Model;

public record AkeneoAccessTokenDto(
    [property: JsonProperty("access_token")] string AccessToken,
    [property: JsonProperty("refresh_token")] string RefreshToken,
    [property: JsonProperty("expires_in")] int ExpirationTime,
    [property: JsonProperty("token_type")] string TokenType,
    [property: JsonProperty("scope")] string Scope);
