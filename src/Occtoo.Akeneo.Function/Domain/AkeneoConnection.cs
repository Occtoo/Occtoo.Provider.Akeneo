using System;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Occtoo.Akeneo.Function.Domain;

public record AkeneoConnection
{
    public AkeneoConnection()
    {
    }

    public AkeneoConnection(Guid id, Guid tenantId, string pimUrl, bool isAlive)
    {
        Id = id;
        TenantId = tenantId;
        PimUrl = pimUrl;
        IsAlive = isAlive;
    }

    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public string PimUrl { get; init; }
    public string Username { get; set; }
    public string Password { get; set; }
    public string Base64ClientIdSecret { get; set; }
    public bool IsAlive { get; init; }
    public bool IsSynchronizing { get; init; }
    public ChannelConfiguration ChannelConfiguration { get; init; }
    public ImmutableDictionary<DataSynchronizationSource, string> DataSources { get; init; } = ImmutableDictionary<DataSynchronizationSource, string>.Empty;
    public DataProvider? DataProvider { get; set; }
    public ImmutableList<DataSynchronizationDetails> DataSynchronizationDetails { get; init; } = ImmutableList<DataSynchronizationDetails>.Empty;
}

public record DataSynchronizationDetails
{
    public DataSynchronizationDetails(DateTimeOffset lastSynchronizationDate,
        int ingestedAmount,
        DataSynchronizationType dataSynchronizationType,
        DataSynchronizationSource dataSynchronizationSource,
        bool succeeded)
    {
        LastSynchronizationDate = lastSynchronizationDate;
        IngestedAmount = ingestedAmount;
        DataSynchronizationType = dataSynchronizationType;
        DataSynchronizationSource = dataSynchronizationSource;
        Succeeded = succeeded;
    }

    public DateTimeOffset LastSynchronizationDate { get; init; }
    public int IngestedAmount { get; init; }
    public bool Succeeded { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DataSynchronizationType DataSynchronizationType { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DataSynchronizationSource DataSynchronizationSource { get; init; }
}

public record ChannelConfiguration
{
    public string ChannelName { get; init; } = string.Empty;
    public string ChannelCode { get; init; } = string.Empty;
    public string CategoryTree { get; init; } = string.Empty;
}

public record DataProvider(Guid ClientId, string ClientSecret);

public enum DataSynchronizationType
{
    Manual, Automatic
}

public enum DataSynchronizationSource
{
    Categories, Products, Media
}
