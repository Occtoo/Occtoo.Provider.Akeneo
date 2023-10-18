using Newtonsoft.Json;
using System.Collections.Immutable;

namespace Occtoo.Akeneo.External.Api.Client.Model;

public record AkeneoChannelsDto(
    [property: JsonProperty("_links")] Links Links,
    [property: JsonProperty("current_page")] int CurrentPage,
    [property: JsonProperty("_embedded")] EmbeddedChannels Embedded
);

public record EmbeddedChannels(
    [property: JsonProperty("items")] IReadOnlyList<Channels> Items
);

public record Channels(
    [property: JsonProperty("_links")] Links Links,
    [property: JsonProperty("code")] string Code,
    [property: JsonProperty("currencies")] IReadOnlyList<string> Currencies,
    [property: JsonProperty("locales")] IReadOnlyList<string> Locales,
    [property: JsonProperty("category_tree")] string CategoryTree,
    [property: JsonProperty("labels")] ImmutableDictionary<string, string> Labels
);
