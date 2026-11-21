using System;
using System.IO;
using System.Linq;
using BBPC.Checks;
using BBPC.UnityRuntime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
namespace BBPC.EditorTools
{
    [InitializeOnLoad]
    public static class P2Validation
    {
        [Serializable] private sealed class Receipt { public bool success, sceneSaved, assetsLoaded, catalogChecksPassed, screenshotsRendered, imageConversionReference; public int assetCount, objectCount, coreAssertions, contentTypographyChecks,unityErrorCount; public string editorVersion, error, scene, revision, sourceFingerprint,requestId; }

        private static string HashFile(string file)
        {
            if (!File.Exists(file)) return "";
            using (var sha = System.Security.Cryptography.SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(file))).Replace("-", "").ToLowerInvariant();
        }
        private static string SourceFingerprint()
        {
            var files = new System.Collections.Generic.List<string>();
            foreach (var part in new[] { "Core", "Runtime", "Editor" })
            {
                files.AddRange(Directory.GetFiles(Path.Combine(Application.dataPath,"BBPC",part),"*.cs",SearchOption.AllDirectories));
                files.AddRange(Directory.GetFiles(Path.Combine(Application.dataPath,"BBPC",part),"*.asmdef",SearchOption.AllDirectories));
            }
            files.Add(Path.Combine(Application.dataPath,"../Packages/manifest.json"));
            files.Add(Path.Combine(Application.dataPath,"../ProjectSettings/ProjectSettings.asset"));
            files.Add(Path.Combine(Application.dataPath,"BBPC/Resources/Recovered606/ui.json"));
            files.Add(Path.Combine(Application.dataPath,"BBPC/Resources/Recovered606/loading.json"));
            files.Add(Path.Combine(Application.dataPath,"BBPC/Resources/Recovered606/navigation.json"));
            foreach(var relative in new[]{"Recovered606/playroom.json","Recovered606/freeplay.json","Recovered606/gameplay.json","Recovered606/note-bindings.json","Recovered606/note-effects.json","Recovered606/note-effects-ui.json","LocalStages606/manifest.json","LocalStages606/stage.json","LocalStages606/elastic.json","Localization606/en.json","Localization606/es.json","Tutorial606/manifest.json","Tutorial606/normal.json","Tutorial606/fnf.json"}) files.Add(Path.Combine(Application.dataPath,"BBPC/Resources",relative));
            files.AddRange(Directory.GetFiles(Path.Combine(Application.dataPath,"BBPC/Resources/Freeplay606/Sounds"),"*.wav"));
            files.Add(Path.Combine(Application.dataPath,"StreamingAssets/Recovered606/Elastic/ElasticDestruction_6.mp3"));
            files.Add(Path.Combine(Application.dataPath,"StreamingAssets/Recovered606/Dunk/Dunk.mp3"));
            files.AddRange(Directory.GetFiles(Path.Combine(Application.dataPath,"BBPC/Resources/LocalStages606/Dunk"),"*.json"));
            files.AddRange(Directory.GetFiles(Path.Combine(Application.dataPath,"BBPC/Resources/LocalStages606"),"*.json",SearchOption.AllDirectories).Where(p=>!files.Contains(p)));
            foreach(var folder in new[]{"Covers","Transport"})files.AddRange(Directory.GetFiles(Path.Combine(Application.dataPath,"StreamingAssets/Recovered606",folder),"*",SearchOption.AllDirectories));
            files.AddRange(Directory.GetFiles(Path.Combine(Application.dataPath,"StreamingAssets/Recovered606/StageConverter"),"*",SearchOption.AllDirectories));
            files.AddRange(Directory.GetFiles(Path.Combine(Application.dataPath,"BBPC/Resources/RemoteStages606"),"*.json",SearchOption.AllDirectories));
            var covers=Path.Combine(Application.dataPath,"StreamingAssets/Recovered606/Covers");if(Directory.Exists(covers))files.AddRange(Directory.GetFiles(covers,"*.png"));
            foreach(var name in new[]{"defbpmd.mp3","defbpmd_fnf.mp3"})files.Add(Path.Combine(Application.dataPath,"StreamingAssets/Recovered606/Tutorial",name));
            files.AddRange(Directory.GetFiles(Path.Combine(Application.dataPath,"BBPC/Resources/Catalog606"),"*.json"));
            files.AddRange(Directory.GetFiles(Path.Combine(Application.dataPath,"BBPC/Resources/CatalogUi606"),"*",SearchOption.AllDirectories));
            files.AddRange(Directory.GetFiles(Path.Combine(Application.dataPath,"BBPC/Resources/FreeplayUi606"),"*",SearchOption.AllDirectories));
            files.AddRange(Directory.GetFiles(Path.Combine(Application.dataPath,"BBPC/Resources/RoleStages606"),"*",SearchOption.AllDirectories));
            files.Add(Path.Combine(Application.dataPath,"BBPC/Scenes/RecoveredFrontend.unity"));
            files.Sort((a,b) => string.CompareOrdinal(a.Replace(Path.DirectorySeparatorChar,'/'), b.Replace(Path.DirectorySeparatorChar,'/')));
            var lines = new System.Text.StringBuilder();
            foreach(var file in files) lines.Append(file.Substring(Application.dataPath.Length).Replace('\\','/')).Append(':').Append(HashFile(file)).Append('\n');
            using(var sha=System.Security.Cryptography.SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(lines.ToString()))).Replace("-","").ToLowerInvariant();
        }
        [Serializable] private sealed class WindowsReceipt { public bool success, resizableWindow; public int defaultWidth, defaultHeight; public string revision, sourceFingerprint,requestId, runtimeAssemblySha256, coreAssemblySha256, playerPath, result; }
        private static string Workspace => Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        private static string Output => Path.Combine(Workspace, "analysis/p2");
        static P2Validation() { if (!Application.isBatchMode) EditorApplication.delayCall += ProcessPendingRequest; }
        public static void ProcessPendingRequest()
        {
            string request = Path.Combine(Output, "editor-request.txt"), done = Path.Combine(Output, "editor-request-done.txt");
            if(File.Exists(request)&&!Application.isBatchMode&&File.ReadAllText(request).StartsWith("P2-cont-build-isolated-"))return;
            if (!File.Exists(request) || (File.Exists(done) && File.ReadAllText(done) == File.ReadAllText(request))) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.delayCall += ProcessPendingRequest; return; }
            File.WriteAllText(done, File.ReadAllText(request)); if (File.ReadAllText(request).StartsWith("P2-cont-build")) BuildWindowsFrontend(); else Run();
        }
        [MenuItem("BB_PC/P2/Toggle inspection in Play Mode")]
        public static void ToggleInspectionInPlay()
        { if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first"); var frontend=UnityEngine.Object.FindObjectOfType<RecoveredFrontend>(); if(frontend==null) throw new InvalidOperationException("RecoveredFrontend not active"); frontend.ToggleInspection(); }
        [MenuItem("BB_PC/P2/Build and validate recovered scene")]
        public static void Run()
        {
            RecoveredRoleImporter.EnsureImported();
            RecoveredCatalogImporter.EnsureImported();
            Directory.CreateDirectory(Output); var receipt = new Receipt { editorVersion = Application.unityVersion, scene = RecoveredFrontend.ScenePath, revision = RecoveredFrontend.Revision, sourceFingerprint = SourceFingerprint(),requestId=File.Exists(Path.Combine(Output,"editor-request.txt"))?File.ReadAllText(Path.Combine(Output,"editor-request.txt")):"" };
            Scene previous = SceneManager.GetActiveScene(); Scene scene = default;
            string firstUnityError=null;Application.LogCallback onLog=(message,trace,type)=>{if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){receipt.unityErrorCount++;if(firstUnityError==null)firstUnityError=message;}};Application.logMessageReceived+=onLog;
            try
            {
                if (Application.unityVersion != "2022.3.62f3") throw new InvalidOperationException("Wrong Editor");
                if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first");
                foreach (var assembly in UnityEditor.Compilation.CompilationPipeline.GetAssemblies(UnityEditor.Compilation.AssembliesType.Editor))
                    if (assembly.name == "BBPC.Unity") foreach (var reference in assembly.compiledAssemblyReferences)
                        if (Path.GetFileName(reference) == "UnityEngine.ImageConversionModule.dll") receipt.imageConversionReference = true;
                if (!receipt.imageConversionReference) throw new InvalidOperationException("Unity compilation did not reference ImageConversionModule");
                scene = EditorSceneManager.NewPreviewScene();
                var host = new GameObject("BB_PC — recovered 6.0.6 frontend");
                SceneManager.MoveGameObjectToScene(host, scene);
                var frontend = host.AddComponent<RecoveredFrontend>();

                // Preview validation never saves or replaces the user's open scene.
                receipt.sceneSaved = File.Exists(RecoveredFrontend.ScenePath);
                if (!receipt.sceneSaved) throw new FileNotFoundException("Recovered entry scene is missing", RecoveredFrontend.ScenePath);
                frontend.Initialize(); frontend.PortraitCamera.scene = scene; CheckGraphics(frontend); receipt.assetsLoaded = true;
                var loaded = new System.Collections.Generic.HashSet<string>();
                var loadingData = JsonUtility.FromJson<RecoveredUiData>(Resources.Load<TextAsset>("Recovered606/loading").text);
                foreach (var data in new[] { frontend.Data, loadingData, frontend.Navigation.Data, frontend.PlayRoom.Data,frontend.PlayRoom.GameplayData,frontend.PlayRoom.Overlays.Data,frontend.PlayRoom.Effects.Data,RecoveredDifficultyStyles.Data,RecoveredDifficultyStyles.DialogData }) foreach (var a in data.assets)
                {
                    if (!loaded.Add(a.id)) continue;
                    UnityEngine.Object asset = a.type == "Font" ? (UnityEngine.Object)Resources.Load<Font>(a.resource) : a.type == "Sprite" ? (UnityEngine.Object)Resources.Load<Sprite>(a.resource) : Resources.Load<Texture2D>(a.resource);
                    if (asset == null) throw new InvalidOperationException("Import failed: " + a.id);
                    receipt.assetCount++;
                }
                receipt.objectCount = frontend.Splash.Objects.Count + frontend.MainRoom.Objects.Count + frontend.Loading.Objects.Count + frontend.Data.row.objects.Length;
                foreach(var page in frontend.Navigation.Data.scenes) receipt.objectCount += page.objects.Length;
                foreach(var sceneData in frontend.PlayRoom.Data.scenes)receipt.objectCount += sceneData.objects.Length;
                foreach(var sceneData in frontend.PlayRoom.GameplayData.scenes)if(sceneData.name=="ResultRoom"||sceneData.name=="ResultHead")receipt.objectCount+=sceneData.objects.Length;
                foreach(var sceneData in frontend.PlayRoom.Overlays.Data.scenes)receipt.objectCount+=sceneData.objects.Length;
                foreach(var sceneData in frontend.PlayRoom.Effects.Data.scenes)receipt.objectCount+=sceneData.objects.Length;
                foreach(var sceneData in RecoveredDifficultyStyles.Data.scenes)receipt.objectCount+=sceneData.objects.Length;
                foreach(var sceneData in RecoveredDifficultyStyles.DialogData.scenes)receipt.objectCount+=sceneData.objects.Length;
                receipt.contentTypographyChecks=P3Validation.Check(frontend);
                RecoveredRoleValidation.Run(frontend,Path.Combine(Workspace,"analysis/p3-roles/unity"));
                receipt.coreAssertions = CoreChecks.Run(Path.Combine(Application.dataPath, "BBPC/Tests/Fixtures"), Path.Combine(Output, "unity-core-results.json"));
                CheckCatalog(); CheckServiceEnvelopes(); CapturedContentValidation.RunIfAvailable();
                RecoveredReadValidation.Run(Path.Combine(Workspace,"analysis/network-v412/editor"),Resources.Load<TextAsset>("LocalStages606/stage").text);
                frontend.catalogSnapshot=Resources.Load<TextAsset>("Catalog606/captured-catalog");frontend.catalogOrigin=CatalogOrigin.Captured606Partial;frontend.ShowCatalog();
                CheckNavigation(frontend);
                if (frontend.CatalogCount != 3 || frontend.catalogOrigin != CatalogOrigin.Captured606Partial) throw new Exception("Captured partial SID binding failed");
                var content = frontend.MainRoom.Find("SonglistScrollView").GetComponent<ScrollRect>().content; Button choose = null;
                foreach (var button in content.GetComponentsInChildren<Button>(true))
                {
                    if(button.name=="PlayButton"&&button.interactable){var row=button.transform;while(row.parent!=null&&row.parent!=content)row=row.parent;string title="";foreach(var t in row.GetComponentsInChildren<Text>(true))if(t.name=="SongText")title=t.text;if(title!=RecoveredTutorialContent.Definition("normal").title&&title!=RecoveredTutorialContent.Definition("elastic").title)throw new Exception("Unverified play row enabled");}
                    if(button.name=="SongText"&&button.GetComponent<Text>().text.StartsWith("SID: "))choose=button;
                }
                if (choose == null) throw new Exception("Song selection target missing");
                choose.onClick.Invoke();
                if (frontend.Selection.Song == null || !frontend.Selection.PreparationBlocker().Contains("truncada")) throw new Exception("Song selection/preparation guard failed");
                receipt.catalogChecksPassed = true;
                RecoveredCatalogValidation.Run(frontend,Path.Combine(Workspace,"analysis/freeplay-v411/editor"));
                foreach (var phase in new[] { "Splash", "MainRoom", "Loading" }) { Capture(frontend, phase, 828, 1792, phase.ToLowerInvariant()+".png"); Capture(frontend, phase, 900, 1600, phase.ToLowerInvariant()+"-9x16.png"); Capture(frontend, phase, 1600, 900, phase.ToLowerInvariant()+"-16x9.png"); } foreach(var page in new[]{MainPage.Versus,MainPage.Shop,MainPage.Character,MainPage.Adventure})
                { frontend.ShowMainRoom(); frontend.Navigation.Select(page); frontend.CaptureCurrentView(Path.Combine(Output,page.ToString().ToLowerInvariant()+"-9x16.png"),900,1600); frontend.CaptureCurrentView(Path.Combine(Output,page.ToString().ToLowerInvariant()+"-16x9.png"),1600,900); }
                receipt.screenshotsRendered = true;
                receipt.success = true;
                Debug.Log("BBPC P2 scene/asset checks PASS. Open " + RecoveredFrontend.ScenePath + " then Play. No remote catalog or playable session validated.");
            }
            catch (Exception e) { receipt.error = e.ToString(); Debug.LogException(e); }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.ClosePreviewScene(scene);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                Application.logMessageReceived-=onLog;
                if(receipt.unityErrorCount>0){receipt.success=false;if(string.IsNullOrEmpty(receipt.error))receipt.error=firstUnityError;}
                File.WriteAllText(Path.Combine(Output, "unity-results.json"), JsonUtility.ToJson(receipt, true));
            }
        }
        private static void CheckNavigation(RecoveredFrontend frontend)
        {
            foreach(MainPage page in Enum.GetValues(typeof(MainPage)))
            {
                frontend.Navigation.ButtonFor(page).onClick.Invoke();
                if(frontend.Navigation.Current != page) throw new Exception("Navigation binding failed: " + page);
                foreach(var pair in frontend.Navigation.Pages) if(pair.Value.Root.activeSelf != (pair.Key == page)) throw new Exception("Navigation page exclusivity failed");
                if(frontend.Loading.Root.activeSelf) throw new Exception("Song Loading used for menu navigation");
                foreach(var text in frontend.GetComponentsInChildren<Text>()) if(text.text=="This is a title" || text.text=="This is a subtitle" || text.text=="Taik to XXX" || text.text=="BF Name") throw new Exception("Unbound authoring label displayed");
            }
            frontend.Navigation.Select(MainPage.Home);
            var phase = frontend.Phase;
            if(frontend.TryPrepareSelectedSong() || frontend.Phase != phase || frontend.Loading.Root.activeSelf) throw new Exception("Incomplete song entered preparation");
            var slider=frontend.Loading.Find("LoadingSlider").GetComponent<Slider>();
            if(slider == null || slider.fillRect == null || slider.value != 1 || slider.interactable) throw new Exception("Original Loading Slider references failed");
        }
        private static void CheckGraphics(RecoveredFrontend frontend)
        {
            foreach (var source in frontend.Data.scenes)
            {
                var tree = source.name == "Splash" ? frontend.Splash : frontend.MainRoom;
                foreach (var item in source.objects)
                {
                    var go = tree.Objects[item.id]; int expected = Array.IndexOf(item.scripts,"UnityEngine.UI.Image") >= 0 || Array.IndexOf(item.scripts,"UnityEngine.UI.Text") >= 0 || Array.IndexOf(item.scripts,"UnityEngine.UI.RawImage") >= 0 ? 1 : 0;
                    if (go.GetComponents<Graphic>().Length != expected) throw new Exception("Incorrect Graphic count: " + item.id);
                    if(Array.IndexOf(item.scripts,"UnityEngine.UI.RawImage")>=0){var raw=go.GetComponent<RawImage>();if(raw==null||raw.color!=new Color(item.rawImage.color[0],item.rawImage.color[1],item.rawImage.color[2],item.rawImage.color[3]))throw new Exception("RawImage source projection mismatch: "+item.id);}
                    if (item.name == "LoadingImg" && (go.GetComponent<Image>() == null || go.GetComponent<Text>() != null)) throw new Exception("LoadingImg Graphic regression");
                }
            }
            if(frontend.PlayRoom.View.Find("Health").GetComponent<Image>().sprite.texture.wrapMode!=TextureWrapMode.Clamp)throw new Exception("Standalone Health sprite repeats its opposite edge");
            bool camera = false; foreach (var c in frontend.GetComponentsInChildren<Camera>(true)) if(c.enabled && c.gameObject.activeInHierarchy && c.targetDisplay==0) camera=true;
            if (!camera || frontend.Loading == null || frontend.Loading.Objects.Count != 14) throw new Exception("Display1 camera/Loading hierarchy unavailable");
            frontend.ShowLoading(); if (!frontend.Loading.Root.activeInHierarchy || frontend.Splash.Root.activeSelf || frontend.MainRoom.Root.activeSelf) throw new Exception("Loading phase isolation failed"); frontend.ShowSplash();
        }
        private static void CheckServiceEnvelopes()
        {
            // Structure from captured responses; all identity values are pseudonyms for tests only.
            var p = RecoveredServiceClient.ParseIdentity("{\"status\":1,\"data\":{\"auid\":\"sanitized-auid\",\"unique_device_id\":\"sanitized-udid\"}}");
            if(p.auid!="sanitized-auid") throw new Exception("Identity envelope failed");
            foreach (var raw in new[]{"{}","{\"status\":0}","{\"status\":1,\"data\":{\"auid\":\"sanitized\"}}"})
            { bool rejected=false;try{RecoveredServiceClient.ParseIdentity(raw);}catch(FormatException){rejected=true;}if(!rejected)throw new Exception("Incomplete identity accepted"); }
            if(CatalogEnvelope.Parse("{\"status\":1,\"data\":{\"items\":[]}}").items.Length!=0)throw new Exception("Catalog envelope failed");
        }
        [MenuItem("BB_PC/P2/Validate and build Windows frontend")]
        public static void BuildWindowsFrontend()
        {
            // Original CDN descriptors use HTTP. Resource clients retain host/size/hash validation.
            PlayerSettings.insecureHttpOption=InsecureHttpOption.AlwaysAllowed;
            Run(); var receipt = JsonUtility.FromJson<Receipt>(File.ReadAllText(Path.Combine(Output,"unity-results.json")));
            if (!receipt.success) throw new InvalidOperationException("Unity validation failed; see analysis/p2/unity-results.json");
            if (receipt.sourceFingerprint != SourceFingerprint()) throw new InvalidOperationException("Sources changed during validation; rerun before building");
            string target = Path.GetFullPath(Path.Combine(Workspace,"validation/Candidates/Freeplay-v4.20/BB_PC.exe")); Directory.CreateDirectory(Path.GetDirectoryName(target));
            PlayerSettings.resizableWindow=true; PlayerSettings.defaultScreenWidth=414; PlayerSettings.defaultScreenHeight=896; PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone,ScriptingImplementation.Mono2x); PlayerSettings.bundleVersion="6.0.6";
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=new[]{RecoveredFrontend.ScenePath},locationPathName=target,target=BuildTarget.StandaloneWindows64,options=BuildOptions.None });
            File.WriteAllText(Path.Combine(Output,"windows-build.txt"),result.summary.result.ToString()+"\n"+target);
            string managed=Path.Combine(Path.GetDirectoryName(target),"BB_PC_Data/Managed");
            var buildReceipt = new WindowsReceipt { success=result.summary.result==UnityEditor.Build.Reporting.BuildResult.Succeeded, resizableWindow=PlayerSettings.resizableWindow, defaultWidth=PlayerSettings.defaultScreenWidth, defaultHeight=PlayerSettings.defaultScreenHeight, revision=receipt.revision, sourceFingerprint=receipt.sourceFingerprint,requestId=receipt.requestId, runtimeAssemblySha256=HashFile(Path.Combine(managed,"BBPC.Unity.dll")), coreAssemblySha256=HashFile(Path.Combine(managed,"BBPC.Core.dll")), playerPath=target, result=result.summary.result.ToString() };
            File.WriteAllText(Path.Combine(Output,"windows-build.json"),JsonUtility.ToJson(buildReceipt,true));
            if (buildReceipt.success && !Application.isBatchMode) RunBuiltPlayer(target);
        }

        private static void RunBuiltPlayer(string target)
        {
            string output = Path.Combine(Workspace,"analysis/p2-visual/player");
            Directory.CreateDirectory(output);
            var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
                FileName = target, UseShellExecute = false, CreateNoWindow = true,
                Arguments = "-batchmode -force-d3d11 -logFile \"" + Path.Combine(output,"player.log") + "\" -bbpc-validate \"" + output + "\""
            });
            double started = EditorApplication.timeSinceStartup;
            EditorApplication.CallbackFunction observe = null;
            observe = () => {
                if (!process.HasExited && EditorApplication.timeSinceStartup-started < 90) return;
                bool timeout = !process.HasExited; if(timeout) process.Kill();
                File.WriteAllText(Path.Combine(output,"process-result.txt"),timeout ? "TIMEOUT: owned test Player terminated after 90s" : "exit_code="+process.ExitCode);
                process.Dispose(); EditorApplication.update -= observe;
            };
            EditorApplication.update += observe;
        }
        private static void CheckCatalog()
        {
            const string json = "{\"items\":[{\"stage\":{\"sid\":\"_schema_check_\",\"title\":\"SCHEMA TEST ONLY\",\"difficulty_v2\":4}}]}";
            var parsed = SongCatalog.Parse(json);
            if (parsed.items.Length != 1 || parsed.items[0].stage.difficulty_v2 != 4) throw new Exception("JSON wire mapping failed");
            var partial = CatalogEnvelope.Parse("{\"status\":1,\"data\":{\"items\":[{\"stage\":{\"sid\":\"_optional_fields_check_\"}}]}}").items[0].stage;
            if (partial.aid != null || partial.artist != null || partial.beatmap != null || partial.beatmap_data != null || partial.duration != 0 || partial.hp != 0 || partial.play_type != 0)
                throw new Exception("Partial SongMetadata optional fields regression");
            foreach (string invalid in new[] { "{}", "{\"items\":[{}]}", "{\"items\":[{\"stage\":{\"sid\":\"x\"}},{\"stage\":{\"sid\":\"x\"}}]}" })
            {
                bool rejected = false; try { SongCatalog.Parse(invalid); } catch (FormatException) { rejected = true; }
                if (!rejected) throw new Exception("Invalid catalog accepted");
            }
        }

        private static void Capture(RecoveredFrontend frontend, string phase, int width, int height, string name)
        {
            if (phase == "Loading") frontend.ShowLoading(); else if (phase == "MainRoom") frontend.ShowMainRoom(); else frontend.ShowSplash();
            frontend.CaptureCurrentView(Path.Combine(Output, name), width, height);
        }
    }
}
