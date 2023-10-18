using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Occtoo.Akeneo.External.Api.Client;
using Occtoo.Akeneo.External.Api.Client.Model;
using Occtoo.Akeneo.Function.Services;
using Occtoo.Onboarding.Sdk;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Occtoo.Akeneo.Function;

public class Program
{
    public static Task Main()
    {
        var host = new HostBuilder()
            .ConfigureFunctionsWorkerDefaults(builder =>
            {
            },
                options =>
                {
                    options.Serializer = new MyNewtonsoftJsonObjectSerializer();
                })
            .ConfigureLogging((ctx, builder) => builder
                .SetMinimumLevel(LogLevel.Warning)
            )
            .ConfigureAppConfiguration(builder =>
            {
                builder.SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("local.settings.json", optional: false);
            })
            .ConfigureServices((context, services) =>
            {
                services
                    .AddSingleton<IAkeneoApiClient, AkeneoApiClient>()
                    .Configure<AkeneoClientConfiguration>(
                        context.Configuration.GetSection(nameof(AkeneoClientConfiguration)));
                var cosmosSettings = context.Configuration.GetSection("CosmosDbSettings").Get<CosmosDbSettings>();

                services.AddSingleton(new CosmosDbService(
                    cosmosSettings.Uri,
                    cosmosSettings.PrimaryKey,
                    cosmosSettings.Database,
                    cosmosSettings.Container
                ));
                services.AddSingleton<IOnboardingServiceClient>(new OnboardingServiceClient(
                    Environment.GetEnvironmentVariable("OcctooDataProviderId"),
                    Environment.GetEnvironmentVariable("OcctooDataProviderSecret")
                ));
            })
            .Build();

        return host
            .RunAsync();
    }

    public class CosmosDbSettings
    {
        public string Uri { get; set; }
        public string PrimaryKey { get; set; }
        public string Database { get; set; }
        public string Container { get; set; }
    }
}
