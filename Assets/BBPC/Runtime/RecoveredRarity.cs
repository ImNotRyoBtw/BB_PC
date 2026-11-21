using UnityEngine;
using UnityEngine.UI;

namespace BBPC.UnityRuntime
{
    // BFCoverComponent.qualityBgDict / qualityIconDict (serialized in the original prefabs, sharedassets3):
    //   RarityV2Type 10 -> bg 1k / icon r      20 -> 2k / s      30 -> 3k / u      101..106 (EX) -> 3k / ex1..ex6
    //   key 0 (no rarity) -> bg bk, no icon.
    // Observed server data: rarity_v2 10 (rarity 3/4) and 20 (rarity 5). Old roles without rarity_v2 fall back to rarity >= 5 ? 20 : 10.
    public static class RecoveredRarity
    {
        public static int V2(RecoveredServerRole role) => role == null ? 0 : role.rarity_v2 ?? (role.rarity >= 5 ? 20 : role.rarity > 0 ? 10 : 0);

        public static Sprite Find(RecoveredUiData data, string name)
        {
            foreach (var asset in data.assets) if (asset.type == "Sprite" && asset.name == name) return RecoveredUiBuilder.Asset<Sprite>(data, asset.id);
            return null;
        }

        public static void Apply(RecoveredUiData data, Image background, Image icon, RecoveredServerRole role)
        {
            int v2 = V2(role);
            string bg = v2 == 10 ? "1k" : v2 == 20 ? "2k" : v2 == 30 ? "3k" : v2 >= 101 && v2 <= 106 ? "3k" : "bk";
            string ic = v2 == 10 ? "r" : v2 == 20 ? "s" : v2 == 30 ? "u" : v2 >= 101 && v2 <= 106 ? "ex" + (v2 - 100) : null;
            if (background != null) { var s = Find(data, bg); if (s != null) background.sprite = s; }
            if (icon != null)
            {
                var s = ic != null ? Find(data, ic) : null;
                if (s != null) icon.sprite = s;
                icon.enabled = s != null;
            }
        }
    }
}
