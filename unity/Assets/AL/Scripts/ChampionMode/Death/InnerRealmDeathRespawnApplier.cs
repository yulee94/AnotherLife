using AL.ChampionMode.Control;
using UnityEngine;

namespace AL.ChampionMode.Death
{
    /// <summary>
    /// Applies an already-planned inner-realm stand-up. No save write, no
    /// scene reload, no pillar bind.
    /// </summary>
    public static class InnerRealmDeathRespawnApplier
    {
        public static bool TryApply(
            InnerRealmDeathRespawnPlan plan,
            ChampionCombat combat,
            ChampionController controller)
        {
            if (plan == null || !plan.IsApplied || combat == null || controller == null)
            {
                return false;
            }

            if (!combat.TryRevive(1f))
            {
                return false;
            }

            InnerRealmVec3 position = plan.Site.Position;
            controller.TeleportTo(new Vector3(position.X, position.Y, position.Z));
            return true;
        }
    }
}
