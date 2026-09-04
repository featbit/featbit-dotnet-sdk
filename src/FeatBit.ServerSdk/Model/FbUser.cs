using System.Collections.Generic;

namespace FeatBit.Sdk.Server.Model
{
    public class FbUser
    {
        internal const string KeyIdAttribute = "keyId";
        internal const string NameAttribute = "name";

        public readonly string Key;
        public readonly string Name;
        public readonly Dictionary<string, string> Custom;

        internal FbUser(string key, string name, Dictionary<string, string> custom)
        {
            Key = key;
            Name = name;
            Custom = custom;
        }

        /// <summary>
        /// Creates an <see cref="IFbUserBuilder"/> for constructing a user object using a fluent syntax.
        /// </summary>
        /// <remarks>
        /// This is the only method for building a <see cref="FbUser"/>. The <see cref="IFbUserBuilder"/> has methods
        /// for setting any number of properties, after which you call <see cref="IFbUserBuilder.Build"/> to get the
        /// resulting <see cref="FbUser"/> instance.
        /// </remarks>
        /// <example>
        /// <code>
        ///     var user = FbUser.Builder("a-unique-key-of-user")
        ///         .Name("user-name")
        ///         .Custom("email", "test@example.com")
        ///         .Build();
        /// </code>
        /// </example>
        /// <param name="key">a <see langword="string"/> that uniquely identifies a user</param>
        /// <returns>a builder object</returns>
        public static IFbUserBuilder Builder(string key)
        {
            return new FbUserBuilder(key);
        }

        /// <summary>
        /// Gets the value of a built-in or custom attribute.
        /// </summary>
        /// <param name="property">the attribute name</param>
        /// <returns>
        /// The attribute value when the attribute exists; otherwise, <see langword="null"/>.
        /// An existing attribute whose value is an empty string returns <see cref="string.Empty"/>.
        /// </returns>
        public string ValueOf(string property)
            => TryGetValue(property, out var value) ? value : null;

        internal bool TryGetValue(string property, out string value)
        {
            if (string.IsNullOrWhiteSpace(property))
            {
                value = null;
                return false;
            }

            if (property == KeyIdAttribute)
            {
                value = Key;
                return true;
            }

            if (property == NameAttribute)
            {
                value = Name;
                return true;
            }

            return Custom.TryGetValue(property, out value);
        }
    }
}
