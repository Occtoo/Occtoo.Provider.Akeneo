using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Occtoo.Akeneo.External.Api.Client.Model;
public record EmbeddedProducts(
    [property: JsonProperty("items")] IReadOnlyList<Product> Items
);

public record Product(
    [property: JsonProperty("_links")] Links Links,
    [property: JsonProperty("uuid")] string Uuid,
    [property: JsonProperty("code")] string Code,
    [property: JsonProperty("identifier")] string Identifier,
    [property: JsonProperty("enabled")] bool Enabled,
    [property: JsonProperty("family")] string Family,
    [property: JsonProperty("categories")] IReadOnlyList<string> Categories,
    [property: JsonProperty("parent")] string Parent,
    [property: JsonProperty("values")] Dictionary<string, JToken> Values,
    [property: JsonProperty("created")] DateTime Created,
    [property: JsonProperty("updated")] DateTime Updated
);

public record Metadata(
    [property: JsonProperty("workflow_status")] string WorkflowStatus
);


public record AkeneoProductsDto(
    [property: JsonProperty("_links")] Links Links,
    [property: JsonProperty("current_page")] int CurrentPage,
    [property: JsonProperty("_embedded")] EmbeddedProducts Embedded
);
