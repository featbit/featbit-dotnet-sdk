using System;
using System.Threading.Tasks;
using FeatBit.Sdk.Server.Evaluation;
using FeatBit.Sdk.Server.Model;
using FeatBit.Sdk.Server.Options;

namespace FeatBit.Sdk.Server;

public interface IFbClient
{
    /// <summary>
    /// Indicates whether the client is ready to be used.
    /// </summary>
    /// <value>true if the client is ready</value>
    bool Initialized { get; }

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
    /// <returns>the variation for the given user, or <c>defaultValue</c> if the flag cannot be evaluated</returns>
    /// <seealso cref="BoolVariationDetail(string, FbUser, bool)"/>
    bool BoolVariation(string key, FbUser user, bool defaultValue = false);

    /// <summary>
    /// Calculates the boolean value of a feature flag for a given user, and returns an object that
    /// describes the way the value was determined.
    /// </summary>
    /// <param name="key">the unique feature key for the feature flag</param>
    /// <param name="user">a given user</param>
    /// <param name="defaultValue">the default value of the flag</param>
    /// <returns>an <see cref="EvalDetail{T}"/> object</returns>
    /// <seealso cref="BoolVariation(string, FbUser, bool)"/>
    EvalDetail<bool> BoolVariationDetail(string key, FbUser user, bool defaultValue = false);

    /// <summary>
    /// Calculates the integer value of a feature flag for a given user.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If the flag variation does not have a integer value, <c>defaultValue</c> is returned.
    /// </para>
    /// <para>
    /// If an error makes it impossible to evaluate the flag (for instance, the feature flag key
    /// does not match any existing flag), <c>defaultValue</c> is returned.
    /// </para>
    /// </remarks>
    /// <param name="key">the unique feature key for the feature flag</param>
    /// <param name="user">a given user</param>
    /// <param name="defaultValue">the default value of the flag</param>
    /// <returns>the variation for the given user, or <c>defaultValue</c> if the flag cannot be evaluated</returns>
    /// <seealso cref="IntVariationDetail(string, FbUser, int)"/>
    int IntVariation(string key, FbUser user, int defaultValue);

    /// <summary>
    /// Calculates the integer value of a feature flag for a given user, and returns an object that
    /// describes the way the value was determined.
    /// </summary>
    /// <param name="key">the unique feature key for the feature flag</param>
    /// <param name="user">a given user</param>
    /// <param name="defaultValue">the default value of the flag</param>
    /// <returns>an <see cref="EvalDetail{T}"/> object</returns>
    /// <seealso cref="IntVariation(string, FbUser, int)"/>
    EvalDetail<int> IntVariationDetail(string key, FbUser user, int defaultValue);

    /// <summary>
    /// Calculates the float value of a feature flag for a given user.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If the flag variation does not have a float value, <c>defaultValue</c> is returned.
    /// </para>
    /// <para>
    /// If an error makes it impossible to evaluate the flag (for instance, the feature flag key
    /// does not match any existing flag), <c>defaultValue</c> is returned.
    /// </para>
    /// </remarks>
    /// <param name="key">the unique feature key for the feature flag</param>
    /// <param name="user">a given user</param>
    /// <param name="defaultValue">the default value of the flag</param>
    /// <returns>the variation for the given user, or <c>defaultValue</c> if the flag cannot be evaluated</returns>
    /// <seealso cref="FloatVariationDetail(string, FbUser, float)"/>
    /// <seealso cref="DoubleVariation(string, FbUser, double)"/>
    float FloatVariation(string key, FbUser user, float defaultValue);

    /// <summary>
    /// Calculates the float value of a feature flag for a given user, and returns an object that
    /// describes the way the value was determined.
    /// </summary>
    /// <param name="key">the unique feature key for the feature flag</param>
    /// <param name="user">a given user</param>
    /// <param name="defaultValue">the default value of the flag</param>
    /// <returns>an <see cref="EvalDetail{T}"/> object</returns>
    /// <seealso cref="FloatVariation(string, FbUser, float)"/>
    /// <seealso cref="DoubleVariationDetail(string, FbUser, double)"/>
    EvalDetail<float> FloatVariationDetail(string key, FbUser user, float defaultValue);

    /// <summary>
    /// Calculates the double value of a feature flag for a given user.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If the flag variation does not have a double value, <c>defaultValue</c> is returned.
    /// </para>
    /// <para>
    /// If an error makes it impossible to evaluate the flag (for instance, the feature flag key
    /// does not match any existing flag), <c>defaultValue</c> is returned.
    /// </para>
    /// </remarks>
    /// <param name="key">the unique feature key for the feature flag</param>
    /// <param name="user">a given user</param>
    /// <param name="defaultValue">the default value of the flag</param>
    /// <returns>the variation for the given user, or <c>defaultValue</c> if the flag cannot be evaluated</returns>
    /// <seealso cref="DoubleVariationDetail(string, FbUser, double)"/>
    /// <seealso cref="FloatVariation(string, FbUser, float)"/>
    double DoubleVariation(string key, FbUser user, double defaultValue);

    /// <summary>
    /// Calculates the double value of a feature flag for a given user, and returns an object that
    /// describes the way the value was determined.
    /// </summary>
    /// <param name="key">the unique feature key for the feature flag</param>
    /// <param name="user">a given user</param>
    /// <param name="defaultValue">the default value of the flag</param>
    /// <returns>an <see cref="EvalDetail{T}"/> object</returns>
    /// <seealso cref="DoubleVariation(string, FbUser, double)"/>
    /// <seealso cref="FloatVariationDetail(string, FbUser, float)"/>
    EvalDetail<double> DoubleVariationDetail(string key, FbUser user, double defaultValue);

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
    string StringVariation(string key, FbUser user, string defaultValue);

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
    EvalDetail<string> StringVariationDetail(string key, FbUser user, string defaultValue);

    /// <summary>
    /// Returns the variation of all feature flags for a given user, which can be passed to front-end code.
    /// </summary>
    /// <param name="user">a given user</param>
    /// <returns>an <see cref="EvalResult"/> array</returns>
    EvalDetail<string>[] GetAllVariations(FbUser user);

    /// <summary>
    /// Tracks that an application-defined event occurred.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method creates a "custom" analytics event containing the specified event name (key),
    /// and the associated user.
    /// </para>
    /// <para>
    /// Note that event delivery is asynchronous, so the event may not actually be sent until
    /// later; see <see cref="FbClient.Flush"/>.
    /// </para>
    /// </remarks>
    /// <param name="user">the evaluation user associated with the event</param>
    /// <param name="eventName">the name of the event</param>
    /// <seealso cref="Track(FbUser, string, double)"/>
    void Track(FbUser user, string eventName);

    /// <summary>
    /// Tracks that an application-defined event occurred, and provides an additional numeric value for
    /// custom metrics.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Note that event delivery is asynchronous, so the event may not actually be sent until
    /// later; see <see cref="FbClient.Flush"/>.
    /// </para>
    /// </remarks>
    /// <param name="user">the evaluation user associated with the event</param>
    /// <param name="eventName">the name of the event</param>
    /// <param name="metricValue">a numeric value used by the FeatBit experimentation feature in
    /// custom numeric metrics</param>
    /// <seealso cref="Track(FbUser, string)"/>
    void Track(FbUser user, string eventName, double metricValue);

    /// <summary>
    /// Tells the client that all pending events (if any) should be delivered as soon
    /// as possible. 
    /// </summary>
    /// <remarks>
    /// <para>
    /// This flush is asynchronous, so this method will return before it is complete. To wait for
    /// the flush to complete, use <see cref="FlushAndWait(TimeSpan)"/> instead (or, if you are done
    /// with the SDK, <see cref="FbClient.CloseAsync()"/>).
    /// </para>
    /// </remarks>
    /// <seealso cref="FlushAndWait(TimeSpan)"/>
    void Flush();

    /// <summary>
    /// Tells the client to deliver any pending events synchronously now.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unlike <see cref="Flush"/>, this method waits for event delivery to finish. The timeout parameter, if
    /// greater than zero, specifies the maximum amount of time to wait. If the timeout elapses before
    /// delivery is finished, the method returns early and returns false; in this case, the SDK may still
    /// continue trying to deliver the events in the background.
    /// </para>
    /// <para>
    /// If the timeout parameter is zero or negative, the method waits as long as necessary to deliver the
    /// events. However, the SDK does not retry event delivery indefinitely; currently, any network error
    /// or server error will cause the SDK to wait <see cref="FbOptions.SendEventRetryInterval"/> and retry one time,
    /// after which the events will be discarded so that the SDK will not keep consuming more memory for events
    /// indefinitely.
    /// </para>
    /// <para>
    /// The method returns true if event delivery either succeeded, or definitively failed, before the
    /// timeout elapsed. It returns false if the timeout elapsed.
    /// </para>
    /// <para>
    /// This method is also implicitly called if you call <see cref="FbClient.CloseAsync()"/>. The difference is
    /// that FlushAndWait does not shut down the SDK client.
    /// </para>
    /// </remarks>
    /// <param name="timeout">the maximum time to wait</param>
    /// <returns>true if completed, false if timed out</returns>
    /// <seealso cref="Flush"/>
    bool FlushAndWait(TimeSpan timeout);

    /// <summary>
    /// Shuts down the client and releases any resources it is using.
    /// </summary>
    Task CloseAsync();
}