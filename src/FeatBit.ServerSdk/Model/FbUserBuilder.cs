using System;
using System.Collections.Generic;

namespace FeatBit.Sdk.Server.Model
{
    public interface IFbUserBuilder
    {
        /// <summary>
        /// Creates a <see cref="FbUser"/> based on the properties that have been set on the builder.
        /// Modifying the builder after this point does not affect the returned <see cref="FbUser"/>.
        /// </summary>
        /// <returns>the configured <see cref="FbUser"/> object</returns>
        FbUser Build();

        /// <summary>
        /// Sets the full name for a user.
        /// </summary>
        /// <param name="name">the name for the user</param>
        /// <returns>the same builder</returns>
        IFbUserBuilder Name(string name);

        /// <summary>
        /// Adds a custom attribute with a string value.
        /// </summary>
        /// <param name="key">the key for the custom attribute</param>
        /// <param name="value">the value for the custom attribute</param>
        /// <returns>the same builder</returns>
        IFbUserBuilder Custom(string key, string value);
    }

    internal class FbUserBuilder : IFbUserBuilder
    {
        private readonly string _key;
        private string _name;
        private readonly Dictionary<string, string> _custom;

        public FbUserBuilder(string key)
        {
            _key = key;
            _name = string.Empty;
            _custom = new Dictionary<string, string>();
        }

        public FbUser Build()
        {
            return new FbUser(_key, _name, _custom);
        }

        public IFbUserBuilder Name(string name)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                _name = name;
            }

            return this;
        }

        public IFbUserBuilder Custom(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("key cannot be null or empty");
            }

            _custom[key] = value;
            return this;
        }
    }
}