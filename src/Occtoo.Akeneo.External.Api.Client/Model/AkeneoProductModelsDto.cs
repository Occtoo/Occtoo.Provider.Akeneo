using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Occtoo.Akeneo.External.Api.Client.Model;
public record EmbeddedProductModels(
    [property: JsonProperty("items")] IReadOnlyList<ProductModel> Items
);

public record ProductModel(
    [property: JsonProperty("_links")] Links Links,
    [property: JsonProperty("code")] string Code,
    [property: JsonProperty("family")] string Family,
    [property: JsonProperty("categories")] IReadOnlyList<string> Categories,
    [property: JsonProperty("parent")] string Parent,
    [property: JsonProperty("values")] Dictionary<string, JToken> Values,
    [property: JsonProperty("created")] DateTime Created,
    [property: JsonProperty("updated")] DateTime Updated
);

public record AkeneoProductModelsDto(
    [property: JsonProperty("_links")] Links Links,
    [property: JsonProperty("current_page")] int CurrentPage,
    [property: JsonProperty("_embedded")] EmbeddedProductModels Embedded
);

public record AkeneoProductModelDto(
    [property: JsonProperty("code")] string Code,
    [property: JsonProperty("family")] string Family,
    [property: JsonProperty("categories")] IReadOnlyList<string> Categories,
    [property: JsonProperty("parent")] string Parent,
    [property: JsonProperty("values")] Dictionary<string, JToken> Values,
    [property: JsonProperty("created")] DateTime Created,
    [property: JsonProperty("updated")] DateTime Updated
);
