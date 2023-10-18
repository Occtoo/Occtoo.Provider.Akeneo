using Newtonsoft.Json;

namespace Occtoo.Akeneo.External.Api.Client.Model;

public record Asset(
    [property: JsonProperty("media")] IReadOnlyList<Media> Media
);

public record Media(
    [property: JsonProperty("_links")] Links Links,
    [property: JsonProperty("data")] string FileName
);

public record AkeneoAssetDto(
    [property: JsonProperty("values")] Asset Asset
);
