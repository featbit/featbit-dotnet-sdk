using System;
using System.Collections.Generic;

namespace FeatBit.Sdk.Server.Model
{
    public interface IFbUserBuilder
    {
        FbUser Build();

        IFbUserBuilder Name(string name);

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