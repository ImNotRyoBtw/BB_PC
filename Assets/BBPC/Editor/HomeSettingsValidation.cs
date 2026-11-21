using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BBPC.UnityRuntime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BBPC.EditorTools
{
    // Explicit batch entry point only: never starts Player, frontend bootstrap or a service client.
    public static class HomeSettingsValidation
    {
        private const string Prefix = "BBPC.Windows.Settings.";
        private const string ListPath = "BgImage/MiniBg/List/";
        [Serializable] private sealed class Receipt
        {
            public bool success, isolatedPreferences, originalHierarchy, audioBindings, reopenPersistence, noServiceClient, extraHpHierarchy;
            public int checks, originalNodes, cycles, unityErrors, captures, extraHpNodes, extraHpCycles, extraHpCaptures;
            public bool battlePassPreviewIsolated, battlePassPreviewHierarchy, battlePassPreviewInert, battlePassPreviewNoOverlay;
            public bool battlePassPreviewSyntheticOwnershipBinding;
            public int battlePassPreviewNodes, battlePassPreviewCycles, battlePassPreviewButtonsChecked, battlePassPreviewCaptures;
            public bool battlePassCellsHierarchy, battlePassCellsInert, battlePassCellsStateUnchanged;
            public int battlePassCellsNodes, battlePassCellsAssets, battlePassCellsPendingControllers, battlePassCellsCycles, battlePassCellsButtonsChecked;
            public string battlePassCellsKind;
            public string battlePassPreviewKind, editorVersion, revision, error;
        }

        public static void Run()
        {
            var args = Environment.GetCommandLineArgs();
            int argument = Array.IndexOf(args, "-bbpc-validate");
            if (!Application.isBatchMode || argument < 0 || argument + 1 >= args.Length)
                throw new InvalidOperationException("Use batchmode with -bbpc-validate <output> to isolate preferences. See HomeSettingsValidation.Run.");
            string output = Path.GetFullPath(args[argument + 1]);
            Directory.CreateDirectory(output);
            var receipt = new Receipt { editorVersion = Application.unityVersion, revision = RecoveredFrontend.Revision };
            Scene previous = SceneManager.GetActiveScene(), preview = default;
            GameObject hostObject = null;
            string firstError = null;
            Application.LogCallback onLog = (message, trace, type) =>
            {
                if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
                receipt.unityErrors++; if (firstError == null) firstError = message;
            };
            Action<bool, string> check = (ok, message) =>
            {
                receipt.checks++;
                if (!ok) throw new InvalidOperationException(message);
            };
            Application.logMessageReceived += onLog;
            try
            {
                check(Application.unityVersion == "2022.3.62f3", "Wrong Unity Editor version");
                check(!EditorApplication.isPlayingOrWillChangePlaymode, "Stop Play Mode before validation");
                check(RecoveredStorage.Isolated, "Validation storage is not isolated");
                receipt.isolatedPreferences = true;
                // Unique absent keys exercise the isolated fallback branch without touching live PlayerPrefs.
                string absentKey = "BBPC.SettingsValidation.Absent." + Guid.NewGuid().ToString("N");
                check(RecoveredPreferences.GetInt(absentKey, -947) == -947, "Isolated integer fallback differs");
                check(RecoveredPreferences.GetString(absentKey, "isolated-fallback") == "isolated-fallback", "Isolated string fallback differs");
                check(Path.GetFullPath(RecoveredStorage.Root) != Path.GetFullPath(Application.persistentDataPath), "Validation uses live persistent storage");
                // If real preferences exist, deliberately request a different fallback: never inherit them.
                foreach (string key in new[] { Prefix + "SE", Prefix + "BGM", "BBPC.Windows.OnlineEnabled" })
                {
                    if (!PlayerPrefs.HasKey(key)) continue;
                    int fallback = PlayerPrefs.GetInt(key, 0) == -947 ? -948 : -947;
                    check(RecoveredPreferences.GetInt(key, fallback) == fallback, "Isolated preferences inherited a live Windows value");
                }
                // Seed deterministic starting values for every Settings control.
                foreach (string key in new[] { "SE", "BGM", "Vibrate", "Notification" })
                    RecoveredPreferences.SetInt(Prefix + key, 1);
                RecoveredPreferences.SetInt("BBPC.Windows.OnlineEnabled", 0);

                var resource = Resources.Load<TextAsset>("RecoveredHome606/dialogs");
                check(resource != null, "RecoveredHome606/dialogs missing");
                var data = JsonUtility.FromJson<RecoveredUiData>(resource.text);
                var source = data.scenes.Single(s => s.name == "SettingsDialog");
                check(data.sourceVersion == "6.0.6" && source.objects.Length == 58, "Expected original 6.0.6 SettingsDialog with 58 nodes");
                receipt.originalNodes = source.objects.Length;

                preview = EditorSceneManager.NewPreviewScene();
                hostObject = new GameObject("Offline Settings validation host");
                SceneManager.MoveGameObjectToScene(hostObject, preview);
                var frontend = hostObject.AddComponent<RecoveredFrontend>();
                frontend.enabled = false;
                // Avoid Initialize/Start, catalog, network, asset importers and saved scenes.
                SetProperty(frontend, "Data", data);
                var cameraObject = new GameObject("Settings validation camera", typeof(Camera));
                cameraObject.transform.SetParent(hostObject.transform, false);
                cameraObject.transform.localPosition = new Vector3(0, 0, -10);
                var camera = cameraObject.GetComponent<Camera>(); camera.scene = preview;
                camera.orthographic = true; camera.orthographicSize = 896;
                SetProperty(frontend, "PortraitCamera", camera);
                var roomObject = new GameObject("Settings validation audio");
                roomObject.transform.SetParent(hostObject.transform, false);
                var room = roomObject.AddComponent<RecoveredPlayRoom>(); room.enabled = false;
                var playback = roomObject.AddComponent<RecoveredAudioPlayback>(); playback.Initialize();
                var effects = roomObject.AddComponent<AudioSource>(); effects.playOnAwake = false;
                SetProperty(room, "Audio", playback); SetProperty(frontend, "PlayRoom", room);
                var dialog = hostObject.AddComponent<RecoveredHomeDialogs>(); dialog.Initialize(frontend);

                for (int cycle = 0; cycle < 3; cycle++)
                {
                    dialog.ShowSettings();
                    var root = RequireRoot(hostObject.transform, check);
                    CheckHierarchy(root, source, data, check); receipt.originalHierarchy = true;
                    if (cycle == 0 && Array.IndexOf(args, "-bbpc-settings-capture") >= 0)
                    {
                        check(SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null, "Capture requires graphics; omit -nographics");
                        frontend.CaptureCurrentView(Path.Combine(output, "settings-portrait.png"), 900, 1600);
                        frontend.CaptureCurrentView(Path.Combine(output, "settings-landscape.png"), 1600, 900);
                        receipt.captures = 2;
                    }
                    foreach (string key in new[] { "SE", "BGM" })
                    {
                        var button = Require(root, ListPath + key + "/Parent/CheckBG").GetComponent<Button>();
                        var mark = Require(button.transform, "Checkmark").GetComponent<Image>();
                        check(button.interactable && mark.enabled && mark.gameObject.activeSelf, key + " did not reopen enabled");
                        for (int click = 0; click < 2; click++)
                        {
                            bool expected = click == 1;
                            button.onClick.Invoke();
                            check(RecoveredPreferences.GetInt(Prefix + key, -1) == (expected ? 1 : 0), key + " click did not persist exactly one toggle");
                            check(mark.enabled == expected && mark.gameObject.activeSelf, key + " checkmark must toggle Image.enabled and retain its active object");
                            check(playback.Source.mute == (key == "BGM" && !expected), "BGM mute binding or SE independence failed");
                            check(effects.mute == (key == "SE" && !expected), "SE mute binding or BGM independence failed");
                        }
                    }
                    foreach (string key in new[] { "Vibrate", "Notification" })
                    {
                        var target = Require(root, ListPath + key + "/Parent/CheckBG");
                        var button = target.GetComponent<Button>();
                        check(button != null && !button.interactable, key + " has no Windows backend and must stay disabled");
                        check(!Require(target, "Checkmark").GetComponent<Image>().enabled, key + " unsupported checkmark visible");
                        button.onClick.Invoke(); // Programmatic invocation bypasses interactable; must still have no binding.
                        check(RecoveredPreferences.GetInt(Prefix + key, -1) == 1, key + " disabled control mutated preference");
                    }
                    check(root.GetComponentsInChildren<Toggle>(true).Length == 0, "Unsupported original Toggle was instantiated");
                    var close = Require(root, "BgImage/MiniBg/CloseButton").GetComponent<Button>();
                    check(close != null && close.interactable, "Original CloseButton unavailable");
                    close.onClick.Invoke();
                    check(!dialog.IsOpen && root == null, "Original CloseButton did not destroy SettingsDialog");
                    check(!hostObject.GetComponentsInChildren<Transform>(true).Any(t => t.name == "SettingsDialog"), "Closed dialog hierarchy leaked");
                    receipt.cycles++;
                }
                receipt.audioBindings = true;
                // Persist both OFF values, destroy the adapter, and recreate it to avoid cached-field false positives.
                dialog.ShowSettings();
                var before = RequireRoot(hostObject.transform, check);
                foreach (string key in new[] { "SE", "BGM" })
                    Require(before, ListPath + key + "/Parent/CheckBG").GetComponent<Button>().onClick.Invoke();
                dialog.Close(); UnityEngine.Object.DestroyImmediate(dialog);
                dialog = hostObject.AddComponent<RecoveredHomeDialogs>(); dialog.Initialize(frontend); dialog.ShowSettings();
                var reopened = RequireRoot(hostObject.transform, check);
                foreach (string key in new[] { "SE", "BGM" })
                {
                    check(RecoveredPreferences.GetInt(Prefix + key, -1) == 0, key + " persistence lost after adapter recreation");
                    check(!Require(reopened, ListPath + key + "/Parent/CheckBG/Checkmark").GetComponent<Image>().enabled, key + " stale checkmark after adapter recreation");
                }
                check(playback.Source.mute && effects.mute, "Persisted OFF preferences not applied during initialization");
                dialog.Close(); receipt.reopenPersistence = true;
                for (int cycle = 0; cycle < 2; cycle++)
                {
                    dialog.ShowExtraHp();
                    var extra = hostObject.transform.Find("PermanentExtraHPDialog");
                    check(extra != null && extra.localScale == Vector3.one, "Original ExtraHP root missing or invisible");
                    var extraSource = data.scenes.Single(s => s.name == "PermanentExtraHPDialog");
                    check(extraSource.objects.Length == 23, "Original ExtraHP source node count differs");
                    CheckHierarchy(extra, extraSource, data, check);
                    receipt.extraHpNodes = extraSource.objects.Length; receipt.extraHpHierarchy = true;
                    foreach (int tier in new[] { 2, 5, 10 })
                    {
                        var buy = Require(extra, "Background/Buy" + tier + "Button").GetComponent<Button>();
                        check(!buy.interactable, "Unavailable Windows IAP must remain disabled");
                        check(Require(buy.transform, "PriceText").GetComponent<Text>().text == "—", "Unverified prefab IAP price displayed");
                        buy.onClick.Invoke();
                    }
                    check(!Require(extra, "Background/RestoreButton").GetComponent<Button>().interactable, "Unavailable restore must remain disabled");
                    if (cycle == 0 && Array.IndexOf(args, "-bbpc-settings-capture") >= 0)
                    {
                        frontend.CaptureCurrentView(Path.Combine(output, "extra-hp-portrait.png"), 900, 1600);
                        receipt.extraHpCaptures++;
                    }
                    var close = Require(extra, "Background/CloseButton").GetComponent<Button>();
                    check(close != null && close.interactable, "Original ExtraHP close unavailable");
                    close.onClick.Invoke();
                    check(!dialog.IsOpen && extra == null, "Original ExtraHP close leaked hierarchy");
                    receipt.extraHpCycles++;
                }
                var passSource = data.scenes.Single(s => s.name == "BattlePassDialog");
                check(passSource.objects.Length == 196, "Expected original BattlePass prefab with 196 nodes");
                receipt.battlePassPreviewKind = "isolated authored prefab layout preview; defaults are not account state";
                receipt.battlePassPreviewIsolated = RecoveredStorage.Isolated;
                receipt.battlePassPreviewNodes = passSource.objects.Length;
                string stateBeforePreview = PreviewState(frontend);
                for (int cycle = 0; cycle < 3; cycle++)
                {
                    dialog.ShowBattlePassPreview();
                    var roots = hostObject.GetComponentsInChildren<Transform>(true).Where(t => t.name == "BattlePassDialog").ToArray();
                    check(roots.Length == 1, "Expected exactly one BattlePass prefab preview");
                    var pass = roots[0];
                    check(pass.gameObject.activeSelf && pass.localScale == Vector3.one, "BattlePass preview is invisible");
                    CheckHierarchy(pass, passSource, data, check);
                    receipt.battlePassPreviewHierarchy = true;
                    // Explicit synthetic DTO: ownership only, not real account or full native entitlement.
                    var syntheticPass = new RecoveredBattlePassData { bid = "synthetic-preview-only", has_purchased = true };
                    string syntheticBefore = JsonUtility.ToJson(syntheticPass);
                    var activate = Require(pass, "TopPanel/TopPanelRight/VipPassBtn").GetComponent<Button>();
                    dialog.BindBattlePassPreview(syntheticPass);
                    check(!activate.gameObject.activeSelf && !activate.interactable, "Synthetic purchased preview must hide disabled Activate");
                    check(JsonUtility.ToJson(syntheticPass) == syntheticBefore, "Preview binding mutated synthetic DTO");
                    syntheticPass.has_purchased = false;
                    dialog.BindBattlePassPreview(syntheticPass);
                    check(activate.gameObject.activeSelf && !activate.interactable, "Synthetic unpurchased preview must restore disabled Activate");
                    check(Require(pass, "Rewards/RewardScroll/Viewport/Content").childCount == 0, "Missing external reward cell prefabs must not be replaced with invented rows");
                    check(PreviewState(frontend) == stateBeforePreview, "Synthetic header binding wrote client/preferences/storage state");
                    receipt.battlePassPreviewSyntheticOwnershipBinding = true;
                    check(!hostObject.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Windows Home dialog" || t.name == "Modal input blocker"), "Generic overlay appeared in BattlePass preview");
                    receipt.battlePassPreviewNoOverlay = true;
                    var close = Require(pass, "BottomPanel/CloseBtn").GetComponent<Button>();
                    var buttons = pass.GetComponentsInChildren<Button>(true);
                    check(buttons.Length == passSource.objects.Count(o => o.button), "BattlePass preview Button count differs");
                    foreach (var button in buttons)
                    {
                        if (button == close) continue;
                        check(!button.interactable, "BattlePass preview control enabled: " + button.name);
                        // PrepareInvoke includes both persistent and runtime listeners, even on inactive controls.
                        var calls = (System.Collections.IList)typeof(UnityEngine.Events.UnityEventBase)
                            .GetMethod("PrepareInvoke", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(button.onClick, null);
                        check(calls.Count == 0, "BattlePass preview control is bound: " + button.name);
                        button.onClick.Invoke(); // Bypasses interactable: disabled controls must still be inert.
                        check(PreviewState(frontend) == stateBeforePreview, "BattlePass preview control wrote client/preferences/storage state: " + button.name);
                        check(frontend.Services == null && hostObject.GetComponentsInChildren<RecoveredServiceClient>(true).Length == 0, "BattlePass preview created a service client");
                        check(dialog.IsOpen, "Non-close BattlePass control closed the preview");
                        receipt.battlePassPreviewButtonsChecked++;
                    }
                    receipt.battlePassPreviewInert = true;
                    if (cycle == 0 && Array.IndexOf(args, "-bbpc-settings-capture") >= 0)
                    {
                        frontend.CaptureCurrentView(Path.Combine(output, "battle-pass-preview-portrait.png"), 900, 1600);
                        receipt.battlePassPreviewCaptures++;
                    }
                    check(close != null && close.interactable, "BattlePass original CloseBtn unavailable");
                    close.onClick.Invoke();
                    check(!dialog.IsOpen && pass == null, "BattlePass preview close leaked hierarchy");
                    check(!hostObject.GetComponentsInChildren<Transform>(true).Any(t => t.name == "BattlePassDialog"), "Closed BattlePass preview remains mounted");
                    check(PreviewState(frontend) == stateBeforePreview, "BattlePass preview lifecycle wrote client/preferences/storage state");
                    receipt.battlePassPreviewCycles++;
                }
                CheckBattlePassCells(frontend, hostObject.transform, check, receipt);
                check(frontend.Services == null && hostObject.GetComponentsInChildren<RecoveredServiceClient>(true).Length == 0, "Settings created a network service client");
                check(RecoveredPreferences.GetInt("BBPC.Windows.OnlineEnabled", -1) == 0, "Settings enabled connectivity");
                receipt.noServiceClient = true;
                check(receipt.unityErrors == 0, firstError ?? "Unity logged an error");
                receipt.success = true;
            }
            catch (Exception e) { receipt.error = e.ToString(); }
            finally
            {
                try
                {
                    if (hostObject != null) UnityEngine.Object.DestroyImmediate(hostObject);
                    if (preview.IsValid() && preview.isLoaded) EditorSceneManager.ClosePreviewScene(preview);
                    if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                }
                catch (Exception e) { receipt.success = false; receipt.error = (receipt.error ?? "") + "\nCleanup: " + e; }
                Application.logMessageReceived -= onLog;
                if (receipt.unityErrors > 0) { receipt.success = false; receipt.error = receipt.error ?? firstError; }
                File.WriteAllText(Path.Combine(output, "home-settings-results.json"), JsonUtility.ToJson(receipt, true));
            }
            if (!receipt.success) throw new InvalidOperationException("Home Settings validation failed: " + receipt.error);
            Debug.Log("PASS offline Home Settings: " + receipt.checks + " checks; original 58 nodes; 3 reopen cycles; isolated preferences.");
        }

        [Serializable] private sealed class CellManifest
        {
            public bool complete;
            public string sourceVersion;
            public int objects, assets;
            public PendingController[] pendingComponents;
            public ManifestFile[] files;
        }
        [Serializable] private sealed class PendingController { public string id, script; }
        [Serializable] private sealed class ManifestFile { public string path, sha256; }

        private static void CheckBattlePassCells(RecoveredFrontend frontend, Transform host, Action<bool, string> check, Receipt receipt)
        {
            const string resourcePath = "RecoveredBattlePassCells606/";
            var resource = Resources.Load<TextAsset>(resourcePath + "dialogs");
            var manifestResource = Resources.Load<TextAsset>(resourcePath + "import-manifest");
            check(resource != null && manifestResource != null, "Original BattlePass cells or import manifest missing");
            var data = JsonUtility.FromJson<RecoveredUiData>(resource.text);
            var manifest = JsonUtility.FromJson<CellManifest>(manifestResource.text);
            check(data.sourceVersion == "6.0.6" && manifest.sourceVersion == "6.0.6" && manifest.complete, "Unexpected BattlePass cell import");
            check(data.scenes.Length == 2 && manifest.objects == 299 && manifest.assets == 44 && manifest.pendingComponents.Length == 18, "BattlePass cell evidence counts differ");
            // Verify the imported bytes too: successful Resources.Load alone cannot detect stale copies.
            string folder = Path.Combine(Application.dataPath, "BBPC/Resources/RecoveredBattlePassCells606");
            using (var sha = System.Security.Cryptography.SHA256.Create())
                foreach (var file in manifest.files)
                {
                    string path = Path.GetFullPath(Path.Combine(folder, file.path));
                    check(path.StartsWith(Path.GetFullPath(folder) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "Manifest path escapes cell folder");
                    check(File.Exists(path) && BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant() == file.sha256, "Cell import hash differs: " + file.path);
                }
            check(data.assets.Length == 44 && data.assets.Select(a => a.id).Distinct().Count() == 44, "Original cell asset inventory differs");
            foreach (var asset in data.assets)
            {
                check(asset.resource.StartsWith(resourcePath, StringComparison.Ordinal), "Cell asset outside original import");
                check((asset.type == "Sprite" && RecoveredUiBuilder.Asset<Sprite>(data, asset.id) != null) ||
                      (asset.type == "Font" && RecoveredUiBuilder.Asset<Font>(data, asset.id) != null), "Original cell asset unresolved: " + asset.id);
                receipt.battlePassCellsAssets++;
            }
            receipt.battlePassCellsPendingControllers = manifest.pendingComponents.Length;
            receipt.battlePassCellsKind = "standalone authored layouts; controller bindings and rewards remain pending";
            string before = PreviewState(frontend);
            foreach (var source in data.scenes)
            {
                int expected = source.name == "BattlePassRewardCell" ? 283 : source.name == "BattlePassExtraRewardCell" ? 16 : 0;
                check(expected > 0 && source.objects.Length == expected, "Unexpected original cell root/count");
                var byTransform = source.objects.ToDictionary(o => o.transformId);
                var originalRoot = source.objects.Single(o => string.IsNullOrEmpty(o.parent) || !byTransform.ContainsKey(o.parent));
                check(originalRoot.name == source.name, "Cell authored root differs");
                receipt.battlePassCellsNodes += expected;
                for (int cycle = 0; cycle < 2; cycle++)
                {
                    var tree = RecoveredUiBuilder.Build(data, source, host);
                    try
                    {
                        RecoveredUiBuilder.UseAuthoredLayoutRoot(tree, source.name, host);
                        check(tree.Root == tree.Objects[originalRoot.id] && tree.Root.transform.parent == host, "Cell wrapper retained instead of authored root");
                        check(tree.Root.GetComponentsInChildren<Transform>(true).Length == expected, "Original cell hierarchy node count differs");
                        foreach (var item in source.objects)
                        {
                            var target = tree.Objects[item.id]; var t = target.transform;
                            var expectedParent = item == originalRoot ? host : tree.Transforms[item.parent];
                            check(t.name == item.name && t.parent == expectedParent && target.activeSelf == item.active, "Original cell parent/name/active differs: " + item.id);
                            var expectedChildren = item.children.Where(byTransform.ContainsKey).Select(id => tree.Transforms[id]).ToArray();
                            check(t.childCount == expectedChildren.Length && Enumerable.Range(0, t.childCount).All(i => t.GetChild(i) == expectedChildren[i]), "Original cell child order differs: " + item.id);
                            var rect = t as RectTransform;
                            check(rect != null && item.rect.Length == 10, "Cell RectTransform missing");
                            check(Vector2.Distance(rect.anchorMin, new Vector2(item.rect[0], item.rect[1])) < .001f &&
                                  Vector2.Distance(rect.anchorMax, new Vector2(item.rect[2], item.rect[3])) < .001f &&
                                  Vector2.Distance(rect.anchoredPosition, new Vector2(item.rect[4], item.rect[5])) < .001f &&
                                  Vector2.Distance(rect.sizeDelta, new Vector2(item.rect[6], item.rect[7])) < .001f &&
                                  Vector2.Distance(rect.pivot, new Vector2(item.rect[8], item.rect[9])) < .001f &&
                                  Vector3.Distance(t.localScale, new Vector3(item.scale[0], item.scale[1], item.scale[2])) < .001f &&
                                  Quaternion.Angle(t.localRotation, new Quaternion(item.rotation[0], item.rotation[1], item.rotation[2], item.rotation[3])) < .01f, "Original cell geometry differs: " + item.id);
                            if (RecoveredUiBuilder.HasScript(item, "UnityEngine.UI.Image"))
                            {
                                string sprite = item.image.sprite;
                                if (item.image.locales != null) foreach (var variant in item.image.locales) if (variant.language == RecoveredLocalization.Language) sprite = variant.sprite;
                                check(target.GetComponent<Image>() != null && target.GetComponent<Image>().sprite == RecoveredUiBuilder.Asset<Sprite>(data, sprite), "Original cell sprite differs: " + item.id);
                            }
                            if (RecoveredUiBuilder.HasScript(item, "UnityEngine.UI.Text"))
                                check(target.GetComponent<Text>() != null && target.GetComponent<Text>().font == RecoveredUiBuilder.Asset<Font>(data, item.text.font), "Original cell font differs: " + item.id);
                            if (RecoveredUiBuilder.HasScript(item, "UnityEngine.UI.RawImage"))
                                check(target.GetComponent<RawImage>() != null && target.GetComponent<RawImage>().texture == RecoveredUiBuilder.Asset<Texture2D>(data, item.rawImage.texture), "Original cell texture differs: " + item.id);
                            check((target.GetComponent<Button>() != null) == item.button, "Cell Button declaration differs");
                            // Opaque original controllers must remain declarations, never executable components.
                            foreach (var component in target.GetComponents<MonoBehaviour>())
                                check(component is Graphic || component is Button || component is LayoutGroup || component is LayoutElement || component is RecoveredUiShadow, "Unexpected executable cell component: " + component.GetType().FullName);
                        }
                        receipt.battlePassCellsHierarchy = true;
                        var buttons = tree.Root.GetComponentsInChildren<Button>(true);
                        check(buttons.Length == (expected == 283 ? 28 : 2), "Original cell button count differs");
                        foreach (var button in buttons)
                        {
                            check(!button.interactable, "Original cell button enabled");
                            var calls = (System.Collections.IList)typeof(UnityEngine.Events.UnityEventBase).GetMethod("PrepareInvoke", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(button.onClick, null);
                            check(calls.Count == 0 && button.onClick.GetPersistentEventCount() == 0, "Original cell button bound");
                            button.onClick.Invoke();
                            check(PreviewState(frontend) == before, "Original cell click mutated state");
                            check(frontend.Services == null && host.GetComponentsInChildren<RecoveredServiceClient>(true).Length == 0, "Cell created service client");
                            receipt.battlePassCellsButtonsChecked++;
                        }
                        receipt.battlePassCellsInert = true;
                    }
                    finally { if (tree.Root != null) UnityEngine.Object.DestroyImmediate(tree.Root); }
                    check(!host.GetComponentsInChildren<Transform>(true).Any(t => t.name == source.name), "Original cell leaked after disposal");
                    check(PreviewState(frontend) == before, "Original cell lifecycle mutated state");
                    receipt.battlePassCellsCycles++;
                }
            }
            receipt.battlePassCellsStateUnchanged = true;
        }

        private static string PreviewState(RecoveredFrontend frontend)
        {
            // Snapshot all isolated preferences (including new keys), frontend serialized state,
            // and persistent files, rather than checking only known Settings keys.
            var snapshot = new System.Text.StringBuilder(JsonUtility.ToJson(frontend));
            foreach (string field in new[] { "strings", "integers" })
            {
                var values = (System.Collections.IDictionary)typeof(RecoveredPreferences)
                    .GetField(field, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                foreach (string key in values.Keys.Cast<string>().OrderBy(k => k, StringComparer.Ordinal))
                    snapshot.Append(JsonUtility.ToJson(new PreferenceEntry { kind = field, key = key, value = values[key].ToString() }));
            }
            foreach (string path in Directory.GetFiles(RecoveredStorage.Root, "*", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
                snapshot.Append(path).Append(':').Append(Convert.ToBase64String(File.ReadAllBytes(path))).Append('\n');
            return snapshot.ToString();
        }
        [Serializable] private sealed class PreferenceEntry { public string kind, key, value; }

        private static void SetProperty(object instance, string name, object value)
        {
            var property = instance.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            if (property == null || property.GetSetMethod(true) == null) throw new MissingMemberException(instance.GetType().Name, name);
            property.GetSetMethod(true).Invoke(instance, new[] { value });
        }
        private static Transform Require(Transform root, string path)
        {
            var target = root.Find(path);
            if (target == null) throw new InvalidOperationException("Original Settings path missing: " + path);
            return target;
        }
        private static Transform RequireRoot(Transform host, Action<bool, string> check)
        {
            var roots = host.GetComponentsInChildren<Transform>(true).Where(t => t.name == "SettingsDialog").ToArray();
            check(roots.Length == 1, "Expected exactly one authored SettingsDialog; wrappers or duplicate dialogs remain");
            check(!host.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Windows Home dialog" || t.name == "Modal input blocker"), "Generic Windows panel appeared");
            check(roots[0].gameObject.activeSelf && roots[0].localScale == Vector3.one, "Original SettingsDialog must open active at scale one");
            return roots[0];
        }
        private static void CheckHierarchy(Transform root, UiScene source, RecoveredUiData data, Action<bool, string> check)
        {
            check(root.GetComponentsInChildren<Transform>(true).Length == source.objects.Length, "Original hierarchy node count differs: " + source.name);
            var byTransform = source.objects.ToDictionary(o => o.transformId);
            foreach (var item in source.objects)
            {
                string path = item.name; var node = item;
                while (!string.IsNullOrEmpty(node.parent) && byTransform.TryGetValue(node.parent, out var parent))
                { path = parent.name + "/" + path; node = parent; }
                check(path == root.name || path.StartsWith(root.name + "/", StringComparison.Ordinal), "Source contains a foreign root");
                var target = path == root.name ? root : Require(root, path.Substring(root.name.Length + 1));
                if (RecoveredUiBuilder.HasScript(item, "UnityEngine.UI.Image"))
                {
                    var image = target.GetComponent<Image>();
                    check(image != null && image.sprite == RecoveredUiBuilder.Asset<Sprite>(data, item.image.sprite), "Original Settings sprite differs: " + path);
                }
                if (item.button) check(target.GetComponent<Button>() != null, "Original Button missing: " + path);
            }
        }
    }
}
