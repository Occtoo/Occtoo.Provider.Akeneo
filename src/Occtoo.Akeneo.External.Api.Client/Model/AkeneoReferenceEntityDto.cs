using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Occtoo.Akeneo.External.Api.Client.Model;

public record EmbeddedReferenceEntities(
    [property: JsonProperty("items")] IReadOnlyList<ReferenceEntity> Items
);

public record ReferenceEntity(
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


public record AkeneoReferenceEntityDto(
    [property: JsonProperty("_links")] Links Links,
    [property: JsonProperty("_embedded")] EmbeddedReferenceEntities Embedded
);
