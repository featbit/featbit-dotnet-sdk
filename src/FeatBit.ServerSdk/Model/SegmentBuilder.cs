using System;
using System.Collections.Generic;

namespace FeatBit.Sdk.Server.Model
{
    internal class SegmentBuilder
    {
        private Guid _id;
        private long _version;
        private ICollection<string> _included = new List<string>();
        private ICollection<string> _excluded = new List<string>();
        private ICollection<MatchRule> _rules = new List<MatchRule>();

        public Segment Build()
        {
            return new Segment(_id, _version, _included, _excluded, _rules);
        }

        public SegmentBuilder Id(Guid id)
        {
            _id = id;
            return this;
        }

        public SegmentBuilder Version(long version)
        {
            _version = version;
            return this;
        }

        public SegmentBuilder Included(params string[] keys)
        {
            foreach (var key in keys)
            {
                _included.Add(key);
            }

            return this;
        }

        public SegmentBuilder Excluded(params string[] keys)
        {
            foreach (var key in keys)
            {
                _excluded.Add(key);
            }

            return this;
        }

        public SegmentBuilder Rules(List<MatchRule> rules)
        {
            _rules = rules;
            return this;
        }

        public SegmentBuilder Rules(params MatchRule[] rules)
        {
            foreach (var rule in rules)
            {
                _rules.Add(rule);
            }

            return this;
        }
    }
}