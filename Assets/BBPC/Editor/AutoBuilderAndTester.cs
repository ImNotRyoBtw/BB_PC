using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class AutoBuilderAndTester
{
    // Uso: -executeMethod AutoBuilderAndTester.Build -buildOutput <carpeta> -isTemp true|false
    public static void Build()
    {
        try
        {
            string output = GetArg("-buildOutput");
            bool isTemp = string.Equals(GetArg("-isTemp"), "true", StringComparison.OrdinalIgnoreCase);

            if (string.IsNullOrEmpty(output))
            {
                Debug.LogError("[AutoBuilder] Falta -buildOutput");
                EditorApplication.Exit(1);
                return;
            }

            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogError("[AutoBuilder] No hay escenas habilitadas en Build Settings");
                EditorApplication.Exit(1);
                return;
            }

            Directory.CreateDirectory(output);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(output, "BB_PC.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = isTemp ? BuildOptions.Development : BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            bool ok = report.summary.result == BuildResult.Succeeded;
            Debug.Log("[AutoBuilder] Resultado: " + report.summary.result);
            EditorApplication.Exit(ok ? 0 : 1);
        }
        catch (Exception e)
        {
            Debug.LogError("[AutoBuilder] Excepción: " + e);
            EditorApplication.Exit(1);
        }
    }

    static string GetArg(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];
        return null;
    }
}