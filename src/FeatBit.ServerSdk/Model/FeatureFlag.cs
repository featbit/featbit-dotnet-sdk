using System;
using System.Collections.Generic;
using FeatBit.Sdk.Server.Store;

namespace FeatBit.Sdk.Server.Model
{
    internal sealed class FeatureFlag : IStorableObject
    {
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

        public DateTime UpdatedAt { get; set; }

        public FeatureFlag(
            string key,
            string variationType,
            ICollection<Variation> variations,
            ICollection<TargetUser> targetUsers,
            ICollection<TargetRule> rules,
            bool isEnabled,
            string disabledVariationId,
            Fallthrough fallthrough,
            bool exptIncludeAllTargets,
            DateTime updatedAt)
        {
            Id = Guid.NewGuid();
            Key = key;
            VariationType = variationType;
            Variations = variations;
            TargetUsers = targetUsers;
            Rules = rules;
            IsEnabled = isEnabled;
            DisabledVariationId = disabledVariationId;
            Fallthrough = fallthrough;
            ExptIncludeAllTargets = exptIncludeAllTargets;
            UpdatedAt = updatedAt;
        }

        public string StoreKey => $"ff_{Id}";

        public ObjectDescriptor Descriptor()
        {
            var version = UpdatedAt == default ? 0 : new DateTimeOffset(UpdatedAt).ToUnixTimeMilliseconds();

            return new ObjectDescriptor(version, this);
        }
    }

    internal sealed class TargetUser
    {
        public ICollection<string> KeyIds { get; set; }

        public string VariationId { get; set; }
    }

    internal sealed class TargetRule
    {
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
        public double[] Rollout { get; set; }

        public double ExptRollout { get; set; }
    }
}