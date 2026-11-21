#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using BBPC.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BBPC.UnityRuntime
{
    // Explicit offline batch check; no real report/reward requests or account writes.
    public static class RecoveredDiamondRewardValidation
    {
        private static int checks;
        private static void Require(bool value, string message)
        { checks++; if (!value) throw new InvalidOperationException(message); }
        private static void Set(object target, string property, object value)
        { target.GetType().GetProperty(property).GetSetMethod(true).Invoke(target, new[] { value }); }
        private static void Drain(IEnumerator routine)
        { while (routine.MoveNext()) if (routine.Current is IEnumerator nested) Drain(nested); }
        public static void BuildWindows()
        {
            // Same Windows x64 BuildPipeline/scene/backend as P2Validation.BuildWindowsFrontend;
            // keep this delivery in a new canonical Builds directory.
            string output = System.IO.Path.Combine(Application.dataPath, "../Builds/Freeplay-reward-diamond");
            if (System.IO.Directory.Exists(output)) throw new InvalidOperationException("Build output already exists: " + output);
            string target = System.IO.Path.GetFullPath(System.IO.Path.Combine(output, "BB_PC.exe"));
            var result = UnityEditor.BuildPipeline.BuildPlayer(new UnityEditor.BuildPlayerOptions
            {
                scenes = new[] { RecoveredFrontend.ScenePath }, locationPathName = target,
                target = UnityEditor.BuildTarget.StandaloneWindows64, options = UnityEditor.BuildOptions.None
            });
            if (result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new InvalidOperationException("Windows build: " + result.summary.result);
            string assembly = System.IO.Path.Combine(output, "BB_PC_Data/Managed/BBPC.Unity.dll");
            using (var sha = System.Security.Cryptography.SHA256.Create())
                Debug.Log("PASS Windows build: " + target + " runtimeAssemblySha256=" + BitConverter.ToString(sha.ComputeHash(System.IO.File.ReadAllBytes(assembly))).Replace("-", "").ToLowerInvariant());
        }
        public static void Run()
        {
            checks = 0;
            // These recovered PNGs ship without importer metadata. Persist Sprite settings
            // through Unity so both Resources.Load<Sprite> and subsequent player imports work.
            foreach (var name in new[] { "zsd", "zuanshicg", "beiguang_0", "UISprite" })
            {
                string path = "Assets/BBPC/Resources/RecoveredGetDiamDialog606/Sprites/" + name + ".png";
                var importer = UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.TextureImporter;
                if (importer == null) throw new InvalidOperationException("Missing texture importer: " + path);
                if (importer.textureType != UnityEditor.TextureImporterType.Sprite || importer.spriteImportMode != UnityEditor.SpriteImportMode.Single)
                {
                    importer.textureType = UnityEditor.TextureImporterType.Sprite;
                    importer.spriteImportMode = UnityEditor.SpriteImportMode.Single;
                    importer.alphaIsTransparency = true;
                    importer.SaveAndReimport();
                }
                Require(Resources.Load<Sprite>("RecoveredGetDiamDialog606/Sprites/" + name) != null, "Sprite resource import failed: " + name);
            }
            Require(Application.isBatchMode && RecoveredStorage.Isolated, "Requires batchmode -bbpc-validate <isolated path>");
            var host = new GameObject("Offline diamond reward validation");
            try
            {
                var f = host.AddComponent<RecoveredFrontend>(); f.enabled = false; f.Initialize();
                var room = f.PlayRoom;
                var played = new RecoveredStageResult { sid = "synthetic-stage", aid = "synthetic-chart", roundId = Guid.NewGuid().ToString("D"), score = 12345 };
                Set(room, "Result", played); Set(room, "State", RecoveredPlayState.Results);
                Set(f, "Phase", FrontendPhase.ResultRoom); room.ResultView.Root.SetActive(true);
                f.Results.BeginResult(played);
                int reports = 0, rewards = 0, expectedIndex = f.Results.SelectedFactorIndex;
                string reportBody = null;
                f.Services.OfflinePost = (route, body) =>
                {
                    if (route == RecoveredReadContracts.ResultReport)
                    {
                        reports++; reportBody = body;
                        return "{\"status\":1,\"data\":{\"gain_diamond\":12,\"multiple_factors\":[2,3,4,5,4,3,2]}}";
                    }
                    Require(route == RecoveredReadContracts.ResultReward, "Unexpected route");
                    rewards++;
                    Require(body == RecoveredReadContracts.RewardBody(played.roundId, played.sid, played.aid, expectedIndex), "Reward changed report identity/index");
                    return "{\"status\":1,\"data\":{\"gain_diamond\":37}}";
                };
                RecoveredReportResultData report = null;
                Drain(f.Services.ReportOwnResult(played, answer => report = answer));
                Require(report != null && report.gain_diamond == 12 && reports == 1, "Score report regression");
                f.Results.SetReportStatus(played, "Synthetic report", report);
                var getMore = room.ResultHeader.Find("GetMoreButton").GetComponent<Button>();
                Require(getMore.interactable, "Reported result cannot claim");
                var canClaim = typeof(RecoveredResultPanel).GetMethod("CanClaim", BindingFlags.NonPublic | BindingFlags.Instance);
                Require((bool)canClaim.Invoke(f.Results, null), "GetMore gate rejected fixture");
                // Drive the same claim coroutine synchronously in Edit Mode, without StartCoroutine.
                typeof(RecoveredResultPanel).GetField("rewardAttempted", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(f.Results, true);
                var flow = typeof(RecoveredResultPanel).GetMethod("ClaimSelectedRewardFlow", BindingFlags.NonPublic | BindingFlags.Instance);
                Drain((IEnumerator)flow.Invoke(f.Results, new object[] { played, played.roundId, expectedIndex }));
                var dialog = host.GetComponent<RecoveredDiamondRewardDialog>();
                Require(dialog.IsOpen && dialog.Count.name == "NumText" && dialog.Count.text == "+37", "NumText must show exact gain_diamond, not factor prediction");
                Require(dialog.Count.font == Resources.Load<Font>("FreeplayUi606/Fonts/resources.assets_914"), "NumText font must be CLASH FONT_CN_REGULAR FINAL (resources.assets:914), not FORTE");
                Require(f.Results.ConfirmedRewardGain == 37 && !getMore.interactable && rewards == 1, "Reward duplicate protection regressed");
                Require(room.ResultHeader.Find("GetImage").activeSelf, "Received state missing");
                Require(f.Results.ConfirmedReport == report && played.score == 12345 && reports == 1 && f.Services.LastReportClientId == played.roundId, "Claim changed score/report state");
                Require(f.Services.ReadAttempts.Count == 0 && f.Services.OfflinePostCount == 2, "Unexpected transport activity");
                var mask = dialog.Count.transform.parent.parent;
                Require(mask.name == "MaskImage" && mask.childCount == 4 && dialog.Count.transform.parent.name == "Image", "GetDiamDialog hierarchy mismatch");
                Require(mask.Find("RewardImage").GetComponent<Image>().sprite == Resources.Load<Sprite>("RecoveredGetDiamDialog606/Sprites/zsd"), "RewardImage must use zsd");
                Require(mask.Find("Lighting").GetComponent<Image>().sprite == Resources.Load<Sprite>("RecoveredGetDiamDialog606/Sprites/beiguang_0"), "Lighting must use beiguang_0");
                Require(dialog.Count.transform.parent.GetComponent<Image>().sprite == Resources.Load<Sprite>("RecoveredGetDiamDialog606/Sprites/zuanshicg"), "Amount icon must use zuanshicg");
                var next = mask.Find("Continue");
                Require(next.GetComponent<Button>().interactable && next.Find("Text") != null && next.GetComponent<Image>().sprite == Resources.Load<Sprite>("RecoveredGetDiamDialog606/Sprites/UISprite"), "Continue hierarchy/sprite mismatch");
                using (RecoveredClaimLoading.Begin(host.transform, f.PortraitCamera))
                    Require(host.GetComponent<RecoveredClaimLoading>().Root.transform.Find("Mask/Loading").GetComponent<Image>().sprite == Resources.Load<Sprite>("RecoveredLoading606/Sprites/CAB-95b4509ffedfadad6d5e856ece52e29d_-9155981551599697757"), "Spinner must use the authored splash_progress sprite, not the diamond reward icon");
                var dismiss = mask.GetComponent<Button>();
                Require(dismiss != null && dismiss.interactable && mask.GetComponent<Image>().raycastTarget, "Mask dismiss button is not wired");
                var maskRect = (RectTransform)mask;
                Require(maskRect.anchorMin == Vector2.zero && maskRect.anchorMax == Vector2.one && maskRect.sizeDelta == Vector2.zero, "Dismiss mask does not stretch over the dialog");
                var popupRoot = mask.parent.gameObject;
                dismiss.onClick.Invoke();
                Require(!dialog.IsOpen && dialog.Count == null && (popupRoot == null || !popupRoot.activeSelf), "Mask callback did not close dialog");
                foreach (int gain in new[] { 0, 999 })
                { dialog.Show(host.transform, f.PortraitCamera, gain); Require(dialog.Count.text == "+" + gain.ToString(System.Globalization.CultureInfo.InvariantCulture), "Synthetic amount mismatch"); dialog.Count.transform.parent.parent.Find("Continue").GetComponent<Button>().onClick.Invoke(); Require(!dialog.IsOpen, "Continue did not dismiss"); }
                Debug.Log("PASS diamond reward: " + checks + " checks; synthetic score report -> ClaimReward -> dialog; MaskImage.onClick dismisses; no remote requests");
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }
    }
}
#endif
