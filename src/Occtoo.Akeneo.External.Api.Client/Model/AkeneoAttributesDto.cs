using Newtonsoft.Json;

namespace Occtoo.Akeneo.External.Api.Client.Model;

public record EmbeddedAttributes(
    [property: JsonProperty("items")] IReadOnlyList<Attribute> Items
);

public record Attribute(
        [property: JsonProperty("_links")] Links Links,
        [property: JsonProperty("code")] string Code,
        [property: JsonProperty("type")] string Type,
        [property: JsonProperty("group")] string Group,
        [property: JsonProperty("unique")] bool Unique
);

public record AkeneoAttributesDto(
        [property: JsonProperty("_links")] Links Links,
        [property: JsonProperty("current_page")] int CurrentPage,
        [property: JsonProperty("_embedded")] EmbeddedAttributes Embedded);


