using System.Collections.Immutable;

namespace Occtoo.Akeneo.External.Api.Client.Model;

public record AkeneoClientConfiguration
{
    public AkeneoClientConfiguration() { }

    public AkeneoClientConfiguration(string clientId, string secret)
    {
        ClientId = clientId;
        Secret = secret;
    }

    public string ClientId { get; init; }
    public string Secret { get; init; }

    public string CreateRedirectUrl(string pimUrl) =>
        $"{pimUrl}/connect/apps/v1/authorize?response_type=code&client_id={ClientId}&scope={string.Join(' ', Scopes)}";

    public ImmutableList<string> Scopes = ImmutableList<string>.Empty
        .Add("read_products")
        .Add("read_catalog_structure")
        .Add("read_attribute_options")
        .Add("read_categories")
        .Add("read_channel_localization")
        .Add("read_channel_settings")
        .Add("read_association_types")
        .Add("read_catalogs")
        .Add("read_asset_families")
        .Add("read_assets")
        .Add("read_reference_entities")
        .Add("read_reference_entity_records");
}
