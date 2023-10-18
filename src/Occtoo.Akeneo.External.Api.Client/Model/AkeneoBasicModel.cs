using Newtonsoft.Json;

namespace Occtoo.Akeneo.External.Api.Client.Model;
public record Links(
    [property: JsonProperty("self")] Self Self,
    [property: JsonProperty("first")] First First,
    [property: JsonProperty("next")] Next? Next,
    [property: JsonProperty("download")] Download? Download
);

public record Self(
    [property: JsonProperty("href")] string Href
);

public record First(
    [property: JsonProperty("href")] string Href
);

public record Next(
    [property: JsonProperty("href")] string Href
);

public record Download(
    [property: JsonProperty("href")] string Href
);
