using System.Threading.Tasks;
using FeatBit.Sdk.Server.Evaluation;
using FeatBit.Sdk.Server.Model;

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
    /// Shuts down the client and releases any resources it is using.
    /// </summary>
    Task CloseAsync();
}