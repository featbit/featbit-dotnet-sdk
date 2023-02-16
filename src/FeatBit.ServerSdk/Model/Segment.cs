using System;
using System.Collections.Generic;
using FeatBit.Sdk.Server.Store;

namespace FeatBit.Sdk.Server.Model
{
    internal sealed class Segment : StorableObject
    {
        public override string StoreKey => StoreKeys.ForSegment(Id.ToString());

        public Guid Id { get; set; }

        public ICollection<string> Included { get; set; }

        public ICollection<string> Excluded { get; set; }

        public ICollection<MatchRule> Rules { get; set; }

        public Segment(
            Guid id,
            long version,
            ICollection<string> included,
            ICollection<string> excluded,
            ICollection<MatchRule> rules)
        {
            Id = id;
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