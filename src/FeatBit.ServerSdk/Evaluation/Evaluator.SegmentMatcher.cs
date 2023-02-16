using System.Linq;
using System.Text.Json;
using FeatBit.Sdk.Server.Model;
using FeatBit.Sdk.Server.Store;

namespace FeatBit.Sdk.Server.Evaluation
{
    internal partial class Evaluator
    {
        internal static bool IsMatchSegment(Segment segment, FbUser user)
        {
            if (segment.Excluded.Contains(user.Key))
            {
                return false;
            }

            if (segment.Included.Contains(user.Key))
            {
                return true;
            }

            // if any rule match this user
            return segment.Rules.Any(
                rule => rule.Conditions.All(condition => IsMatchCondition(condition, user))
            );
        }

        internal bool IsMatchAnySegment(Condition segmentCondition, FbUser user)
        {
            var segmentIds = JsonSerializer.Deserialize<string[]>(segmentCondition.Value);
            if (segmentIds == null || !segmentIds.Any())
            {
                return false;
            }

            foreach (var segmentId in segmentIds)
            {
                var segment = _store.Get<Segment>(StoreKeys.ForSegment(segmentId));
                if (IsMatchSegment(segment, user))
                {
                    return true;
                }
            }

            return false;
        }
    }
}