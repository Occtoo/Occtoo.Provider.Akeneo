using Occtoo.Akeneo.Function.Domain;
using System;
using System.Collections.Immutable;

namespace Occtoo.Akeneo.Function.Model;

public record AkeneoConnectionDto(Guid Id,
    Guid TenantId,
    string PimUrl,
    Guid DataProviderId,
    bool IsAlive,
    bool IsSynchronizing,
    string AkeneoAuthRedirectUrl,
    ChannelConfiguration ChannelConfiguration,
    ImmutableDictionary<DataSynchronizationSource, string> DataSources,
    ImmutableList<DataSynchronizationDetails> DataSynchronizationDetails)
{
    public static AkeneoConnectionDto FromDomain(AkeneoConnection connection)
    {
        return new(connection.Id,
            connection.TenantId,
            connection.PimUrl,
            connection.DataProvider?.ClientId ?? Guid.Empty,
            connection.IsAlive,
            connection.IsSynchronizing,
            string.Empty,
            connection.ChannelConfiguration,
            connection.DataSources,
            connection.DataSynchronizationDetails);
    }

    public static AkeneoConnectionDto FromDomain(AkeneoConnection connection, string redirectUrl)
    {
        return new(connection.Id,
            connection.TenantId,
            connection.PimUrl,
            connection.DataProvider?.ClientId ?? Guid.Empty,
            connection.IsAlive,
            connection.IsSynchronizing,
            redirectUrl,
            connection.ChannelConfiguration,
            connection.DataSources,
            connection.DataSynchronizationDetails);
    }
};

public record AkeneoConnectionAuthenticate(string Code, string State);
