using System;
using FeatBit.Sdk.Server.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FeatBit.Sdk.Server.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds FeatBit services to the specified <see cref="IServiceCollection" />.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <param name="configureOptions">An <see cref="Action{FbOptions}"/> to configure the provided <see cref="FbOptions"/>.</param>
    /// <remarks>This method will block the current thread for the duration specified in <see cref="FbOptions.StartWaitTime"/>.</remarks>
    public static void AddFeatBit(this IServiceCollection services, Action<FbOptions> configureOptions)
    {
        var options = new FbOptionsBuilder().Build();
        configureOptions(options);

        AddFeatBit(services, options);
    }

    /// <summary>
    /// Adds FeatBit services to the specified <see cref="IServiceCollection" />.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <param name="options">The options for configuring FeatBit.</param>
    /// <remarks>This method will block the current thread for the duration specified in <see cref="FbOptions.StartWaitTime"/>.</remarks>
    public static void AddFeatBit(this IServiceCollection services, FbOptions options)
    {
        var serviceDescriptor = new ServiceDescriptor(
            typeof(IFbClient),
            serviceProvider =>
            {
                // Configure the logger factory if not provided or set to NullLoggerFactory
                if (options.LoggerFactory is null)
                {
                    options.LoggerFactory = NullLoggerFactory.Instance;
                }
                else if (options.LoggerFactory is NullLoggerFactory)
                {
                    var defaultLoggerFactory = serviceProvider.GetService<ILoggerFactory>();
                    if (defaultLoggerFactory != null)
                    {
                        options.LoggerFactory = defaultLoggerFactory;
                    }
                }

                var client = new FbClient(options);
                return client;
            },
            ServiceLifetime.Singleton
        );
        services.Add(serviceDescriptor);

        // The FbClientHostedService ensures:
        // 1. FbClient is created before the application starts.
        // 2. FbClient is closed when the application host performs a graceful shutdown.
        services.AddHostedService<FbClientHostedService>();
    }
}