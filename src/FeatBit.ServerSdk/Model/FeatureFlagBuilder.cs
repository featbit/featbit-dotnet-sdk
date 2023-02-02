using System;
using System.Collections.Generic;

namespace FeatBit.Sdk.Server.Model
{
    internal class FeatureFlagBuilder
    {
        private Guid _id = Guid.NewGuid();
        private string _key;
        private long _version;
        private string _variationType = string.Empty;
        private ICollection<Variation> _variations = new List<Variation>();
        private ICollection<TargetUser> _targetUsers = new List<TargetUser>();
        private ICollection<TargetRule> _rules = new List<TargetRule>();
        private bool _isEnabled = true;
        private string _disabledVariationId = string.Empty;
        private Fallthrough _fallthrough;
        private bool _exptIncludeAllTargets = true;

        public FeatureFlag Build()
        {
            return new FeatureFlag(_id, _key, _version, _variationType, _variations, _targetUsers, _rules, _isEnabled,
                _disabledVariationId, _fallthrough, _exptIncludeAllTargets);
        }

        public FeatureFlagBuilder Id(Guid id)
        {
            _id = id;
            return this;
        }

        public FeatureFlagBuilder Key(string key)
        {
            _key = key;
            return this;
        }

        public FeatureFlagBuilder Version(long version)
        {
            _version = version;
            return this;
        }

        public FeatureFlagBuilder VariationType(string variationType)
        {
            _variationType = variationType;
            return this;
        }

        public FeatureFlagBuilder Variations(params Variation[] variations)
        {
            foreach (var variation in variations)
            {
                _variations.Add(variation);
            }

            return this;
        }

        public FeatureFlagBuilder Variations(List<Variation> variations)
        {
            _variations = variations;
            return this;
        }

        public FeatureFlagBuilder TargetUsers(params TargetUser[] targetUsers)
        {
            foreach (var targetUser in targetUsers)
            {
                _targetUsers.Add(targetUser);
            }

            return this;
        }

        public FeatureFlagBuilder TargetUsers(List<TargetUser> targetUsers)
        {
            _targetUsers = targetUsers;
            return this;
        }

        public FeatureFlagBuilder Rules(params TargetRule[] rules)
        {
            foreach (var rule in rules)
            {
                _rules.Add(rule);
            }

            return this;
        }

        public FeatureFlagBuilder Rules(List<TargetRule> rules)
        {
            _rules = rules;
            return this;
        }

        public FeatureFlagBuilder IsEnabled(bool isEnabled)
        {
            _isEnabled = isEnabled;
            return this;
        }

        public FeatureFlagBuilder DisabledVariationId(string disabledVariationId)
        {
            _disabledVariationId = disabledVariationId;
            return this;
        }

        public FeatureFlagBuilder Fallthrough(Fallthrough fallthrough)
        {
            _fallthrough = fallthrough;
            return this;
        }

        public FeatureFlagBuilder ExptIncludeAllTargets(bool exptIncludeAllTargets)
        {
            _exptIncludeAllTargets = exptIncludeAllTargets;
            return this;
        }
    }
}