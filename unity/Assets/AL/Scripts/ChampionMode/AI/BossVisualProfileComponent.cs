using AL.Core;
using UnityEngine;

namespace AL.ChampionMode.AI
{
    public sealed class BossVisualProfileComponent : MonoBehaviour
    {
        public ItemGrade ThreatGrade { get; private set; } = ItemGrade.Legendary;
        public RealmId VisualRealm { get; private set; } = RealmId.Umbral;
        public Color PrimaryColor { get; private set; } = new Color(0.88f, 0.08f, 0.05f);
        public Color SecondaryColor { get; private set; } = new Color(0.22f, 0.42f, 0.62f);
        public float VisualIntensity { get; private set; } = 1.25f;
        public float SilhouetteScale { get; private set; } = 1.15f;

        public void Configure(ItemGrade threatGrade, RealmId visualRealm, Color primaryColor, Color secondaryColor, float visualIntensity, float silhouetteScale)
        {
            ThreatGrade = threatGrade;
            VisualRealm = visualRealm == RealmId.None ? RealmId.Umbral : visualRealm;
            PrimaryColor = primaryColor;
            SecondaryColor = secondaryColor;
            VisualIntensity = Mathf.Clamp(visualIntensity, 0.4f, 2.8f);
            SilhouetteScale = Mathf.Clamp(silhouetteScale, 0.8f, 1.8f);
        }
    }
}
