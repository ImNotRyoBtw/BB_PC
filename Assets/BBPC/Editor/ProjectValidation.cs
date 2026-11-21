using System;
using System.IO;
using System.Collections.Generic;
using BBPC.Checks;
using BBPC.Gameplay;
using BBPC.UnityRuntime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BBPC.EditorTools
{
    public static class ProjectValidation
    {
        [Serializable] private sealed class Receipt
        {
            public bool success;
            public string editorVersion;
            public string activeBuildTarget;
            public int coreAssertions;
            public bool bootstrapSceneSaved;
            public bool unityClockAdapterChecked;
            public bool playerBuilt;
            public string limitation;
        }

        // Batch entry point: runs only our port, never an Android sample or original DLL.
        public static void Run()
        {
            string workspace = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
            string output = Path.Combine(workspace, "analysis/implementation-p1");
            Directory.CreateDirectory(output);
            var receipt = new Receipt { editorVersion = Application.unityVersion, limitation = "Project import and component checks only; no playable session or player build.", playerBuilt = false };
            try
            {
                if (Application.unityVersion != "2022.3.62f3") throw new InvalidOperationException("Unexpected Editor: " + Application.unityVersion);
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                    throw new InvalidOperationException("Cannot select StandaloneWindows64.");
                PlayerSettings.companyName = "BB_PC";
                PlayerSettings.productName = "BB_PC";
                PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
                PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Standalone, ApiCompatibilityLevel.NET_Standard);
                receipt.activeBuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString();
                receipt.coreAssertions = CoreChecks.Run(Path.Combine(Application.dataPath, "BBPC/Tests/Fixtures"), Path.Combine(output, "unity-core-results.json"));
                var state = new AudioClockState { DspStartTime = -1, PlayedDuration = 1.25 };
                if (UnityAudioClock.Create(state).PlayingTimestamp() != 1250) throw new Exception("Unity adapter failed.");
                receipt.unityClockAdapterChecked = true;
                if (!AssetDatabase.IsValidFolder("Assets/BBPC/Scenes")) AssetDatabase.CreateFolder("Assets/BBPC", "Scenes");
                const string scenePath = "Assets/BBPC/Scenes/Bootstrap.unity";
                if (!File.Exists(scenePath))
                {
                    Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    new GameObject("BB_PC Bootstrap").AddComponent<PortBootstrap>();
                    if (!EditorSceneManager.SaveScene(scene, scenePath)) throw new Exception("Cannot save bootstrap scene.");
                }
                receipt.bootstrapSceneSaved = File.Exists(scenePath);
                var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
                if (!scenes.Exists(s => s.path == scenePath))
                {
                    scenes.Add(new EditorBuildSettingsScene(scenePath, true));
                    EditorBuildSettings.scenes = scenes.ToArray();
                }
                AssetDatabase.SaveAssets();
                receipt.success = true;
                Debug.Log("BBPC P1 validation PASS. Assertions=" + receipt.coreAssertions);
            }
            catch (Exception e) { Debug.LogException(e); throw; }
            finally { File.WriteAllText(Path.Combine(output, "unity-results.json"), JsonUtility.ToJson(receipt, true)); }
        }
    }
}
