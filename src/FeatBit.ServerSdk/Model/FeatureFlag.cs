using System;
using System.Collections.Generic;
using System.Linq;
using FeatBit.Sdk.Server.Evaluation;
using FeatBit.Sdk.Server.Store;

namespace FeatBit.Sdk.Server.Model
{
    internal sealed class FeatureFlag : StorableObject
    {
        public override string StoreKey => StoreKeys.ForFeatureFlag(Key);

        public Guid Id { get; set; }

        public string Key { get; set; }

        public string VariationType { get; set; }

        public ICollection<Variation> Variations { get; set; }

        public ICollection<TargetUser> TargetUsers { get; set; }

        public ICollection<TargetRule> Rules { get; set; }

        public bool IsEnabled { get; set; }

        public string DisabledVariationId { get; set; }

        public Fallthrough Fallthrough { get; set; }

        public bool ExptIncludeAllTargets { get; set; }

        public FeatureFlag(
            Guid id,
            string key,
            long version,
            string variationType,
            ICollection<Variation> variations,
            ICollection<TargetUser> targetUsers,
            ICollection<TargetRule> rules,
            bool isEnabled,
            string disabledVariationId,
            Fallthrough fallthrough,
            bool exptIncludeAllTargets)
        {
            Id = id;
            Key = key;
            Version = version;
            VariationType = variationType;
            Variations = variations;
            TargetUsers = targetUsers;
            Rules = rules;
            IsEnabled = isEnabled;
            DisabledVariationId = disabledVariationId;
            Fallthrough = fallthrough;
            ExptIncludeAllTargets = exptIncludeAllTargets;
        }

        public Variation GetVariation(string variationId)
        {
            return Variations.FirstOrDefault(x => x.Id == variationId);
        }
    }

    internal sealed class TargetUser
    {
        public ICollection<string> KeyIds { get; set; }

        public string VariationId { get; set; }
    }

    internal sealed class TargetRule
    {
        public string Name { get; set; }

        public string DispatchKey { get; set; }

        public bool IncludedInExpt { get; set; }

        public ICollection<Condition> Conditions { get; set; }

        public ICollection<RolloutVariation> Variations { get; set; }
    }

    internal sealed class Fallthrough
    {
        public string DispatchKey { get; set; }

        public bool IncludedInExpt { get; set; }

        public ICollection<RolloutVariation> Variations { get; set; }
    }

    internal sealed class RolloutVariation
    {
        public string Id { get; set; }

        public double[] Rollout { get; set; }

        public double ExptRollout { get; set; }

        public bool IsInRollout(string key) => DispatchAlgorithm.IsInRollout(key, Rollout);

        public double DispatchRollout()
        {
            if (Rollout is not { Length: 2 })
            {
                // malformed rollout
                return 0.0;
            }

            return Rollout[1] - Rollout[0];
        }
    }
}