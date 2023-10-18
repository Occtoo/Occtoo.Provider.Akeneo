using Newtonsoft.Json;

namespace Occtoo.Akeneo.External.Api.Client.Model;

public record EmbeddedCategories(
    [property: JsonProperty("items")] IReadOnlyList<Category> Items
);

public record Category(
    [property: JsonProperty("_links")] Links Links,
    [property: JsonProperty("code")] string Code,
    [property: JsonProperty("parent")] string Parent,
    [property: JsonProperty("updated")] DateTimeOffset Updated,
    [property: JsonProperty("labels")] Dictionary<string, string> Labels
);

public record AkeneoCategoriesDto(
    [property: JsonProperty("_links")] Links Links,
    [property: JsonProperty("current_page")] int CurrentPage,
    [property: JsonProperty("_embedded")] EmbeddedCategories Embedded
);
