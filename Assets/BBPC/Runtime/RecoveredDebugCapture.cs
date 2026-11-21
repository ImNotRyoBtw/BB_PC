using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace BBPC.UnityRuntime
{
    // Diagnostic aid (no gameplay effect). Always mirrors warnings/errors/exceptions to <persistent>/BBPC.Logs/ui-errors.log.
    // F9 writes <persistent>/BBPC.Logs/capture-<time>/ with screenshot.png + hierarchy.txt: every ACTIVE UI object with its screen rectangle,
    // sprite / texture / text, so a layout can be compared against the original without sending photos.
    public sealed class RecoveredDebugCapture : MonoBehaviour
    {
        private string dir;
        private void Awake()
        {
            try { dir = Path.Combine(Application.persistentDataPath, "BBPC.Logs"); Directory.CreateDirectory(dir); } catch { dir = null; }
            Application.logMessageReceived += OnLog;
        }
        private void OnDestroy() { Application.logMessageReceived -= OnLog; }

        private void OnLog(string message, string stack, LogType type)
        {
            if (dir == null || type == LogType.Log) return;
            try { File.AppendAllText(Path.Combine(dir, "ui-errors.log"), DateTime.Now.ToString("HH:mm:ss") + " [" + type + "] " + message + (type == LogType.Warning ? "" : "\n" + stack) + "\n"); } catch { }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F9)) Capture();
        }

        public void Capture()
        {
            if (dir == null) return;
            try
            {
                var folder = Path.Combine(dir, "capture-" + DateTime.Now.ToString("yyyyMMdd-HHmmss")); Directory.CreateDirectory(folder);
                StartCoroutine(Shot(Path.Combine(folder, "screenshot.png")));
                var sb = new StringBuilder(); sb.AppendLine("screen " + Screen.width + "x" + Screen.height);
                foreach (var canvas in FindObjectsOfType<Canvas>())
                {
                    if (!canvas.gameObject.activeInHierarchy || !canvas.isRootCanvas) continue;
                    sb.AppendLine("== canvas " + canvas.name + " mode=" + canvas.renderMode);
                    Dump(canvas.transform, 0, sb);
                }
                File.WriteAllText(Path.Combine(folder, "hierarchy.txt"), sb.ToString());
                Debug.LogWarning("[BBPC.Debug] capture saved: " + folder);
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        // The screencapture module is not in this project's manifest: read the frame buffer and encode with imageconversion (which is).
        private System.Collections.IEnumerator Shot(string file)
        {
            yield return new WaitForEndOfFrame();
            Texture2D tex = null;
            try
            {
                tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0); tex.Apply();
                File.WriteAllBytes(file, tex.EncodeToPNG());
            }
            catch (Exception e) { Debug.LogException(e); }
            if (tex != null) Destroy(tex);
        }

        private static void Dump(Transform t, int depth, StringBuilder sb)
        {
            if (depth > 40) return;
            var line = new StringBuilder(new string(' ', depth * 2)); line.Append(t.name);
            if (t is RectTransform rt)
            {
                var c = new Vector3[4]; rt.GetWorldCorners(c);
                line.Append(" [" + (int)c[0].x + "," + (int)c[0].y + " " + (int)(c[2].x - c[0].x) + "x" + (int)(c[2].y - c[0].y) + "]");
            }
            if (t.TryGetComponent<Image>(out var im)) line.Append(" img=" + (im.sprite != null ? im.sprite.name : "-") + (im.enabled ? "" : "(off)"));
            if (t.TryGetComponent<RawImage>(out var ri)) line.Append(" raw=" + (ri.texture != null ? ri.texture.name + ":" + ri.texture.width + "x" + ri.texture.height : "-") + (ri.enabled ? "" : "(off)"));
            if (t.TryGetComponent<Text>(out var tx)) line.Append(" text=\"" + tx.text.Replace("\n", "\\n") + "\" size=" + tx.fontSize);
            if (t.TryGetComponent<Button>(out var b)) line.Append(" button" + (b.interactable ? "" : "(disabled)"));
            sb.AppendLine(line.ToString());
            for (int i = 0; i < t.childCount; i++) { var c = t.GetChild(i); if (c.gameObject.activeSelf) Dump(c, depth + 1, sb); }
        }
    }
}
