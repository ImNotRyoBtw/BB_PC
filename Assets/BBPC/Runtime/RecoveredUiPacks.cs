using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BBPC.UnityRuntime
{
    // Converted UI asset-bundle packs, keyed by the md5 the server sends (shop payload: cover_res_pack / resource_pack {url, md5}).
    // The game downloads the bundle by url+md5 and loads a prefab from it (SimpleAssetBundleManager). An Android bundle cannot be loaded by a
    // Windows Player (scripts/shaders/texture formats), so, like the role packs, it is converted once into plain data:
    //   <pack>/pack.json   RecoveredUiData (scenes + assets, sprite resources "file:<md5>/Sprites/x.png")
    //   <pack>/Sprites/*.png
    // Search order: <persistent>/BBPC.Content606/ui-v1/<md5>/ (produced by Tools/ui_pack_convert.py), then StreamingAssets/Recovered606/UiPacks/<md5>/.
    // Sprites are integrity-checked against the sha256 stored in pack.json. A missing pack returns null and a reason, never invented art.
    public static class RecoveredUiPacks
    {
        private static readonly Dictionary<string, RecoveredUiData> packs = new Dictionary<string, RecoveredUiData>();
        private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        public static string LastError { get; private set; }

        private static IEnumerable<string> Roots()
        {
            yield return Path.Combine(Application.persistentDataPath, "BBPC.Content606", "ui-v1");
            yield return Path.Combine(Application.streamingAssetsPath, "Recovered606", "UiPacks");
        }

        private static bool ValidMd5(string md5)
        {
            if (string.IsNullOrEmpty(md5) || md5.Length < 8 || md5.Length > 64) return false;
            foreach (char c in md5) if (!Uri.IsHexDigit(c)) return false;
            return true;
        }

        public static string FindDirectory(string md5)
        {
            if (!ValidMd5(md5)) return null;
            foreach (var root in Roots()) { var dir = Path.Combine(root, md5.ToLowerInvariant()); if (File.Exists(Path.Combine(dir, "pack.json"))) return dir; }
            return null;
        }

        public static RecoveredUiData Load(RemoteSongFile pack)
        {
            LastError = null;
            if (pack == null || !ValidMd5(pack.md5)) { LastError = "Pack sin md5 válido"; return null; }
            string key = pack.md5.ToLowerInvariant();
            if (packs.TryGetValue(key, out var cached)) return cached;
            var dir = FindDirectory(key);
            if (dir == null) { LastError = "Pack " + key + " sin convertir (Tools/ui_pack_convert.py)"; return null; }
            try
            {
                var data = JsonUtility.FromJson<RecoveredUiData>(File.ReadAllText(Path.Combine(dir, "pack.json")));
                if (data == null || data.scenes == null || data.scenes.Length == 0 || data.sourceVersion != "6.0.6") { LastError = "pack.json inválido"; return null; }
                packs[key] = data; return data;
            }
            catch (Exception e) { LastError = "pack.json ilegible: " + e.Message; return null; }
        }

        public static Sprite LoadSprite(UiAsset asset)
        {
            // resource = "file:<md5>/Sprites/<name>.png"
            if (sprites.TryGetValue(asset.id + "|" + asset.resource, out var hit)) return hit;
            string relative = asset.resource.Substring(5);
            int slash = relative.IndexOf('/');
            if (slash <= 0) throw new InvalidDataException("Bad pack resource: " + asset.resource);
            string dir = FindDirectory(relative.Substring(0, slash));
            if (dir == null) throw new FileNotFoundException("Pack missing: " + asset.resource);
            string path = RecoveredDynamicRoles.SafePath(dir, relative.Substring(slash + 1));
            byte[] bytes = File.ReadAllBytes(path);
            if (!string.IsNullOrEmpty(asset.sha256) && RecoveredContentBytes.Hash(bytes) != asset.sha256) throw new InvalidDataException("Pack sprite integrity failed: " + asset.name);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            if (!texture.LoadImage(bytes, true)) throw new InvalidDataException("Pack sprite undecodable: " + asset.name);
            var pivot = asset.pivot != null && asset.pivot.Length == 2 ? new Vector2(asset.pivot[0], asset.pivot[1]) : new Vector2(.5f, .5f);
            var b = asset.border != null && asset.border.Length == 4 ? new Vector4(asset.border[0], asset.border[1], asset.border[2], asset.border[3]) : Vector4.zero;
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), pivot, asset.ppu > 0 ? asset.ppu : 100f, 0, SpriteMeshType.FullRect, b);
            sprite.name = asset.name;
            sprites[asset.id + "|" + asset.resource] = sprite;
            return sprite;
        }

        public static Sprite Find(RecoveredUiData data, string name)
        {
            foreach (var a in data.assets) if (a.type == "Sprite" && a.name == name) return RecoveredUiBuilder.Asset<Sprite>(data, a.id);
            return null;
        }
    }
}
