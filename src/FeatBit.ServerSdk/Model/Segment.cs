using System;
using System.Collections.Generic;
using FeatBit.Sdk.Server.Store;

namespace FeatBit.Sdk.Server.Model
{
    internal sealed class Segment : StorableObject
    {
        public override string StoreKey => $"segment_{Id}";

        public Guid Id { get; set; }

        public ICollection<string> Included { get; set; }

        public ICollection<string> Excluded { get; set; }

        public ICollection<MatchRule> Rules { get; set; }

        public Segment(
            long version,
            ICollection<string> included,
            ICollection<string> excluded,
            ICollection<MatchRule> rules)
        {
            Id = Guid.NewGuid();
            Version = version;
            Included = included;
            Excluded = excluded;
            Rules = rules;
        }
    }

    internal sealed class MatchRule
    {
        public ICollection<Condition> Conditions { get; set; } = Array.Empty<Condition>();
    }
}