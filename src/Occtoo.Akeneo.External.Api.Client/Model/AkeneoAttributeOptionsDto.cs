using Newtonsoft.Json;

namespace Occtoo.Akeneo.External.Api.Client.Model;

public record EmbeddedAttributeOptions(
    [property: JsonProperty("items")] IReadOnlyList<AttributeOptions> Items
);

public record AttributeOptions(
    [property: JsonProperty("_links")] Links Links,
    [property: JsonProperty("code")] string Code,
    [property: JsonProperty("attribute")] string Attribute,
    [property: JsonProperty("sort_order")] int SortOrder,
    [property: JsonProperty("labels")] Dictionary<string, string> Labels
);

public record AkeneoAttributeOptionsDto(
    [property: JsonProperty("_links")] Links Links,
    [property: JsonProperty("current_page")] int CurrentPage,
    [property: JsonProperty("_embedded")] EmbeddedAttributeOptions Embedded
);
