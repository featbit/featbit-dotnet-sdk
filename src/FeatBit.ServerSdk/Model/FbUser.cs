using System.Collections.Generic;

namespace FeatBit.Sdk.Server.Model
{
    public class FbUser
    {
        public readonly string Key;
        public readonly string Name;
        public readonly Dictionary<string, string> Custom;

        internal FbUser(string key, string name, Dictionary<string, string> custom)
        {
            Key = key;
            Name = name;
            Custom = custom;
        }

        public static IFbUserBuilder Builder(string key)
        {
            return new FbUserBuilder(key);
        }

        public string ValueOf(string property)
        {
            if (string.IsNullOrWhiteSpace(property))
            {
                return string.Empty;
            }

            if (property == "keyId")
            {
                return Key;
            }

            if (property == "name")
            {
                return Name;
            }

            return Custom.TryGetValue(property, out var value) ? value : string.Empty;
        }
    }
}