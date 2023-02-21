using System;
using System.Linq;
using System.Threading.Tasks;
using FeatBit.Sdk.Server.DataSynchronizer;
using FeatBit.Sdk.Server.Evaluation;
using FeatBit.Sdk.Server.Model;
using FeatBit.Sdk.Server.Options;
using FeatBit.Sdk.Server.Store;
using Microsoft.Extensions.Logging;

namespace FeatBit.Sdk.Server
{
    public sealed class FbClient
    {
        #region private fields

        private readonly FbOptions _options;
        private readonly IMemoryStore _store;
        private readonly IDataSynchronizer _dataSynchronizer;
        private readonly Evaluator _evaluator;
        private readonly ILogger _logger;

        #endregion

        /// <summary>
        /// Indicates whether the client is ready to be used.
        /// </summary>
        /// <value>true if the client is ready</value>
        public bool Initialized => _store.Populated;

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
            _dataSynchronizer = new WebSocketDataSynchronizer(options, _store);
            _evaluator = new Evaluator(_store);
            _logger = options.LoggerFactory.CreateLogger<FbClient>();

            // starts client
            Start();
        }

        // internal use for testing
        internal FbClient(FbOptions options, IMemoryStore store, IDataSynchronizer synchronizer)
        {
            _options = options;
            _store = store;
            _dataSynchronizer = synchronizer;
            _evaluator = new Evaluator(_store);
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
                    "Waiting up to {0} milliseconds for FbClient to start...",
                    _options.StartWaitTime.TotalMilliseconds
                );
                var success = task.Wait(_options.StartWaitTime);
                if (success)
                {
                    _logger.LogInformation("FbClient successfully started");
                }
                else
                {
                    _logger.LogWarning("Timeout encountered waiting for FbClient initialization");
                }
            }
            catch (Exception ex)
            {
                // we do not want to throw exceptions from the FbClient constructor, so we'll just swallow this.
                _logger.LogWarning("Exception occurs when initialize FbClient", ex);
            }
        }

        /// <summary>
        /// Calculates the boolean value of a feature flag for a given user.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If the flag variation does not have a boolean value, <c>defaultValue</c> is returned.
        /// </para>
        /// <para>
        /// If an error makes it impossible to evaluate the flag (for instance, the feature flag key
        /// does not match any existing flag), <c>defaultValue</c> is returned.
        /// </para>
        /// </remarks>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="user">a given user</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>the variation for the given user, or <c>defaultValue</c> if the flag cannot
        /// be evaluated</returns>
        /// <seealso cref="BoolVariationDetail(string, FbUser, bool)"/>
        public bool BoolVariation(string key, FbUser user, bool defaultValue = false)
            => EvaluateCore(key, user, defaultValue, ValueConverters.Bool).Value;

        /// <summary>
        /// Calculates the boolean value of a feature flag for a given user, and returns an object that
        /// describes the way the value was determined.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="user">a given user</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>an <see cref="EvalDetail{T}"/> object</returns>
        /// <seealso cref="BoolVariation(string, FbUser, bool)"/>
        public EvalDetail<bool> BoolVariationDetail(string key, FbUser user, bool defaultValue = false)
            => EvaluateCore(key, user, defaultValue, ValueConverters.Bool);

        /// <summary>
        /// Calculates the string value of a feature flag for a given user.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Normally, the string value of a flag should not be null, since the FeatBit UI
        /// does not allow you to assign a null value to a flag variation. However, since
        /// <c>defaultValue</c> is nullable, you should assume that the return value might be null.
        /// </para>
        /// </remarks>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="user">a given user</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>the variation for the given user, or <c>defaultValue</c> if the flag cannot
        /// be evaluated</returns>
        /// <seealso cref="StringVariationDetail(string, FbUser, string)"/>
        public string StringVariation(string key, FbUser user, string defaultValue)
            => EvaluateCore(key, user, defaultValue, ValueConverters.String).Value;

        /// <summary>
        /// Calculates the string value of a feature flag for a given context, and returns an object that
        /// describes the way the value was determined.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <see cref="EvalDetail{T}.Reason"/> property in the result will also be included
        /// in analytics events, if you are capturing detailed event data for this flag.
        /// </para>
        /// </remarks>
        /// <param name="key">the unique feature key for the feature flag</param>
        /// <param name="user">a given user</param>
        /// <param name="defaultValue">the default value of the flag</param>
        /// <returns>an <see cref="EvalDetail{T}"/> object</returns>
        /// <seealso cref="StringVariation(string, FbUser, string)"/>
        public EvalDetail<string> StringVariationDetail(string key, FbUser user, string defaultValue)
            => EvaluateCore(key, user, defaultValue, ValueConverters.String);

        /// <summary>
        /// Returns the variation of all feature flags for a given user, which can be passed to front-end code.
        /// </summary>
        /// <param name="user">a given user</param>
        /// <returns>an <see cref="EvalResult"/> array</returns>
        public EvalResult[] GetAllVariations(FbUser user)
        {
            var results = _store
                .Find<FeatureFlag>(x => x.StoreKey.StartsWith(StoreKeys.FlagPrefix))
                .Select(flag => _evaluator.Evaluate(flag, user))
                .ToArray();

            return results;
        }

        /// <summary>
        /// Shuts down the client and releases any resources it is using.
        /// </summary>
        public async Task CloseAsync()
        {
            await _dataSynchronizer.StopAsync();
        }

        private EvalDetail<TValue> EvaluateCore<TValue>(
            string key,
            FbUser user,
            TValue defaultValue,
            Func<string, TValue> converter)
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

            var result = _evaluator.Evaluate(ctx);
            if (result.Kind == ReasonKind.Error)
            {
                // error happened when evaluate flag, return default value 
                return new EvalDetail<TValue>(result.Kind, result.Reason, defaultValue);
            }

            try
            {
                var typedValue = converter(result.Value);
                return new EvalDetail<TValue>(result.Kind, result.Reason, typedValue);
            }
            catch
            {
                // type mismatch, return default value
                return new EvalDetail<TValue>(ReasonKind.WrongType, "type mismatch", defaultValue);
            }
        }
    }
}