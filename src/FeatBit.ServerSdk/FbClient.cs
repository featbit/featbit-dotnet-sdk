using System;
using System.Linq;
using System.Threading.Tasks;
using FeatBit.Sdk.Server.DataSynchronizer;
using FeatBit.Sdk.Server.Evaluation;
using FeatBit.Sdk.Server.Events;
using FeatBit.Sdk.Server.Model;
using FeatBit.Sdk.Server.Options;
using FeatBit.Sdk.Server.Store;
using Microsoft.Extensions.Logging;

namespace FeatBit.Sdk.Server
{
    public sealed class FbClient : IFbClient
    {
        #region private fields

        private readonly FbOptions _options;
        private readonly IMemoryStore _store;
        private readonly IEvaluator _evaluator;

        // internal for testing
        internal readonly IDataSynchronizer _dataSynchronizer;
        internal readonly IEventProcessor _eventProcessor;

        private readonly ILogger _logger;

        #endregion

        /// <inheritdoc/>
        public bool Initialized => _dataSynchronizer.Initialized;

        /// <summary>
        /// Creates a new client instance that connects to FeatBit with the default option.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If you need to specify any custom SDK options, use <see cref="FbClient(FbOptions)"/>
        /// instead.
        /// </para>
        /// <para>
        /// Applications should instantiate a single instance for the lifetime of the application. In
        /// unusual cases where an application needs to evaluate feature flags from different FeatBit
        /// projects or environments, you may create multiple clients, but they should still be retained
        /// for the lifetime of the application rather than created per request or per thread.
        /// </para>
        /// <para>
        /// The constructor will never throw an exception, even if initialization fails. For more details
        /// about initialization behavior and how to detect error conditions, see
        /// <see cref="FbClient(FbOptions)"/>.
        /// </para>
        /// </remarks>
        /// <param name="secret">the sdk secret for your FeatBit environment</param>
        /// <seealso cref="FbClient(FbOptions)"/>
        public FbClient(string secret) : this(FbOptions.Default(secret))
        {
        }

        /// <summary>
        /// Creates a new client to connect to FeatBit with a custom option.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Applications should instantiate a single instance for the lifetime of the application. In
        /// unusual cases where an application needs to evaluate feature flags from different FeatBit
        /// projects or environments, you may create multiple clients, but they should still be retained
        /// for the lifetime of the application rather than created per request or per thread.
        /// </para>
        /// <para>
        /// Normally, the client will begin attempting to connect to FeatBit as soon as you call the
        /// constructor. The constructor returns as soon as any of the following things has happened:
        /// </para>
        /// <list type="number">
        /// <item><description> It has successfully connected to FeatBit and received feature flag data. In this
        /// case, <see cref="Initialized"/> will be true. </description></item>
        /// <item><description> It has not succeeded in connecting within the <see cref="FbOptionsBuilder.StartWaitTime(TimeSpan)"/>
        /// timeout (the default for this is 3 seconds). This could happen due to a network problem or a
        /// temporary service outage. In this case, <see cref="Initialized"/> will be false, 
        /// indicating that the SDK will still continue trying to connect in the background. </description></item>
        /// <item><description> It has encountered an unrecoverable error: for instance, FeatBit has rejected the
        /// sdk secret. Since an invalid key will not become valid, the SDK will not retry in this case.
        /// <see cref="Initialized"/> will be false. </description></item>
        /// </list>
        /// <para>
        /// Failure to connect to FeatBit will never cause the constructor to throw an exception.
        /// Under any circumstance where it is not able to get feature flag data from FeatBit (and
        /// therefore <see cref="Initialized"/> is false), if it does not have any other source of data
        /// (such as a persistent data store) then feature flag evaluations will behave the same as if
        /// the flags were not found: that is, they will return whatever default value is specified in
        /// your code.
        /// </para>
        /// </remarks>
        /// <param name="options">a FbOptions object (which includes the sdk secret)</param>
        /// <example>
        /// <code>
        ///     var options = new FbOptionsBuilder("your-sdk-secret")
        ///         .StartWaitTime(TimeSpan.FromSeconds(3))
        ///         .Build();
        ///     var client = new FbClient(options);
        /// </code>
        /// </example>
        /// <seealso cref="FbClient"/>
        public FbClient(FbOptions options)
        {
            _options = options;
            _store = new DefaultMemoryStore();
            _evaluator = new Evaluator(_store);

            if (_options.Offline)
            {
                _dataSynchronizer = new NullDataSynchronizer();
                _eventProcessor = new NullEventProcessor();

                // use bootstrap provider to populate store
                _options.BootstrapProvider.Populate(_store);
            }
            else
            {
                _dataSynchronizer = new WebSocketDataSynchronizer(options, _store);
                _eventProcessor = new DefaultEventProcessor(options);
            }

            _logger = options.LoggerFactory.CreateLogger<FbClient>();

            // starts client
            Start();
        }

        // internal use for testing
        internal FbClient(
            FbOptions options,
            IMemoryStore store,
            IDataSynchronizer synchronizer,
            IEventProcessor eventProcessor)
        {
            _options = options;
            _store = store;
            _dataSynchronizer = synchronizer;
            _evaluator = new Evaluator(_store);
            _eventProcessor = eventProcessor;
            _logger = options.LoggerFactory.CreateLogger<FbClient>();

            // starts client
            Start();
        }

        /// <summary>
        /// Starts FbClient.
        /// </summary>
        private void Start()
        {
            _logger.LogInformation("Starting FbClient...");
            var task = _dataSynchronizer.StartAsync();
            try
            {
                _logger.LogInformation(
                    "Waiting up to {StartWaitTime} milliseconds for FbClient to start...",
                    _options.StartWaitTime.TotalMilliseconds
                );
                var success = task.Wait(_options.StartWaitTime);
                if (success)
                {
                    _logger.LogInformation("FbClient successfully started");
                }
                else
                {
                    _logger.LogError(
                        "Timeout encountered while waiting for FbClient initialization. " +
                        "This error typically occurs when we are unable to connect to FeatBit or the provided secret is invalid. " +
                        "Please double-check your EnvSecret and StreamingUri configuration."
                    );
                }
            }
            catch (Exception ex)
            {
                // we do not want to throw exceptions from the FbClient constructor, so we'll just swallow this.
                _logger.LogError(ex, "An exception occurred during FbClient initialization.");
            }
        }

        /// <inheritdoc/>
        public bool BoolVariation(string key, FbUser user, bool defaultValue = false)
            => EvaluateCore(key, user, defaultValue, ValueConverters.Bool).Value;

        /// <inheritdoc/>
        public EvalDetail<bool> BoolVariationDetail(string key, FbUser user, bool defaultValue = false)
            => EvaluateCore(key, user, defaultValue, ValueConverters.Bool);

        /// <inheritdoc/>
        public int IntVariation(string key, FbUser user, int defaultValue)
            => EvaluateCore(key, user, defaultValue, ValueConverters.Int).Value;

        /// <inheritdoc/>
        public EvalDetail<int> IntVariationDetail(string key, FbUser user, int defaultValue)
            => EvaluateCore(key, user, defaultValue, ValueConverters.Int);

        /// <inheritdoc/>
        public float FloatVariation(string key, FbUser user, float defaultValue)
            => EvaluateCore(key, user, defaultValue, ValueConverters.Float).Value;

        /// <inheritdoc/>
        public EvalDetail<float> FloatVariationDetail(string key, FbUser user, float defaultValue)
            => EvaluateCore(key, user, defaultValue, ValueConverters.Float);

        /// <inheritdoc/>
        public double DoubleVariation(string key, FbUser user, double defaultValue)
            => EvaluateCore(key, user, defaultValue, ValueConverters.Double).Value;

        /// <inheritdoc/>
        public EvalDetail<double> DoubleVariationDetail(string key, FbUser user, double defaultValue)
            => EvaluateCore(key, user, defaultValue, ValueConverters.Double);

        /// <inheritdoc/>
        public string StringVariation(string key, FbUser user, string defaultValue)
            => EvaluateCore(key, user, defaultValue, ValueConverters.String).Value;

        /// <inheritdoc/>
        public EvalDetail<string> StringVariationDetail(string key, FbUser user, string defaultValue)
            => EvaluateCore(key, user, defaultValue, ValueConverters.String);

        /// <inheritdoc/>
        public EvalDetail<string>[] GetAllVariations(FbUser user)
        {
            var results = _store
                .Find<FeatureFlag>(x => x.StoreKey.StartsWith(StoreKeys.FlagPrefix))
                .Select(flag => _evaluator.Evaluate(flag, user).evalResult)
                .Select(x => new EvalDetail<string>(x.Kind, x.Reason, x.Value))
                .ToArray();

            return results;
        }

        /// <inheritdoc/>
        public async Task CloseAsync()
        {
            _logger.LogInformation("Closing FbClient...");
            await _dataSynchronizer.StopAsync();
            await _eventProcessor.FlushAndWaitAsync(_options.FlushTimeout);
            _logger.LogInformation("FbClient successfully closed.");
        }

        private EvalDetail<TValue> EvaluateCore<TValue>(
            string key,
            FbUser user,
            TValue defaultValue,
            ValueConverter<TValue> converter)
        {
            if (!Initialized)
            {
                // Flag evaluation before client initialized; always returning default value
                return new EvalDetail<TValue>(ReasonKind.ClientNotReady, "client not ready", defaultValue);
            }

            var ctx = new EvaluationContext
            {
                FlagKey = key,
                FbUser = user
            };

            var (evalResult, evalEvent) = _evaluator.Evaluate(ctx);
            if (evalResult.Kind == ReasonKind.Error)
            {
                // error happened when evaluate flag, return default value 
                return new EvalDetail<TValue>(evalResult.Kind, evalResult.Reason, defaultValue);
            }

            // record evaluation event
            _eventProcessor.Record(evalEvent);

            return converter(evalResult.Value, out var typedValue)
                ? new EvalDetail<TValue>(evalResult.Kind, evalResult.Reason, typedValue)
                // type mismatch, return default value
                : new EvalDetail<TValue>(ReasonKind.WrongType, "type mismatch", defaultValue);
        }
    }
}