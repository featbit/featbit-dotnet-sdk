using System;
using System.Collections.Generic;
using FeatBit.Sdk.Server.Store;

namespace FeatBit.Sdk.Server.Model
{
    internal sealed class Segment : IStorableObject
    {
        public Guid Id { get; set; }

        public ICollection<string> Included { get; set; }

        public ICollection<string> Excluded { get; set; }

        public ICollection<MatchRule> Rules { get; set; }

        public DateTime UpdatedAt { get; set; }

        public Segment(
            ICollection<string> included,
            ICollection<string> excluded,
            ICollection<MatchRule> rules,
            DateTime updatedAt)
        {
            Id = Guid.NewGuid();
            Included = included;
            Excluded = excluded;
            Rules = rules;
            UpdatedAt = updatedAt;
        }

        public string StoreKey => $"segment_{Id}";

        public ObjectDescriptor Descriptor()
        {
            var version = UpdatedAt == default ? 0 : new DateTimeOffset(UpdatedAt).ToUnixTimeMilliseconds();

            return new ObjectDescriptor(version, this);
        }
    }

    internal sealed class MatchRule
    {
        public ICollection<Condition> Conditions { get; set; } = Array.Empty<Condition>();
    }
}