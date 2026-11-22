using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BBPC.UnityRuntime
{
    // Diagnostic aid (no gameplay effect). Only active when the player is started with:  -bbpc-driver <folder>
    // It runs <folder>/script.txt INSIDE the game: clicks / scroll / drag are sent straight to the EventSystem and
    // screenshots are read from the game's own back buffer. Nothing depends on window focus, on other windows
    // being on top, or on the real mouse/keyboard, so it also works while the user keeps using the PC.
    // Script syntax (same as the MCP): wait S | click X Y [right] | move X Y | drag X1 Y1 X2 Y2 | scroll N | shot LABEL
    // Coordinates are window pixels of a 1280x720 reference (origin top-left); `ref W H` changes the reference.
    // key / type are ignored (legacy Input cannot be injected); the game has no Enter handler in the menus.
    // Output: <folder>/status.txt (RUNNING | DONE | ERROR: ..), <folder>/driver.log, <folder>/<label>.png
    public sealed class RecoveredTestDriver : MonoBehaviour
    {
        // Safety net for the "no spins / purchases" rule: clicks whose target hierarchy matches are refused
        // unless the script contains `unsafe on`.
        private static readonly Regex Risky = new Regex("buy|purchase|roulette|lottery|spin|redeem|exchange", RegexOptions.IgnoreCase);
        private string folder; private float refW = 1280f, refH = 720f; private Vector2 pointer; private bool allowRisky;

        public static string FolderFromArgs()
        {
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "-bbpc-driver");
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        public void Begin(string output)
        {
            folder = output; Application.runInBackground = true;
            try { Directory.CreateDirectory(folder); } catch { }
            StartCoroutine(Run());
        }

        private void Log(string line)
        {
            Debug.Log("[BBPC.Driver] " + line);
            try { File.AppendAllText(Path.Combine(folder, "driver.log"), DateTime.Now.ToString("HH:mm:ss.fff") + " " + line + "\n"); } catch { }
        }
        private void Status(string value) { try { File.WriteAllText(Path.Combine(folder, "status.txt"), value); } catch { } }
        private Vector2 ToScreen(float x, float y) => new Vector2(x * Screen.width / refW, Screen.height - y * Screen.height / refH);
        private static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);

        private IEnumerator Run()
        {
            Status("RUNNING"); pointer = new Vector2(Screen.width * .5f, Screen.height * .5f);
            string path = Path.Combine(folder, "script.txt");
            if (!File.Exists(path)) { Status("ERROR: falta script.txt"); yield break; }
            var steps = new List<string[]>();
            foreach (var raw in File.ReadAllText(path).Split(new[] { ';', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var line = raw.Split('#')[0].Trim(); if (line.Length == 0) continue;
                steps.Add(line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
            }
            Log("script: " + steps.Count + " pasos, ventana " + Screen.width + "x" + Screen.height);
            int n = 0;
            foreach (var s in steps)
            {
                n++; string cmd = s[0].ToLowerInvariant(); IEnumerator step = null;
                try
                {
                    switch (cmd)
                    {
                        case "wait": step = Wait(Mathf.Min(F(s[1]), 60f)); break;
                        case "click": step = Click(ToScreen(F(s[1]), F(s[2])), s.Length > 3 && s[3].ToLowerInvariant() == "right"); break;
                        case "move": pointer = ToScreen(F(s[1]), F(s[2])); Log(n + " move"); break;
                        case "drag": step = Drag(ToScreen(F(s[1]), F(s[2])), ToScreen(F(s[3]), F(s[4]))); break;
                        case "scroll": step = Scroll(F(s[1])); break;
                        case "shot": step = Shot(s.Length > 1 ? s[1] : "shot" + n); break;
                        case "ref": refW = F(s[1]); refH = F(s[2]); break;
                        case "unsafe": allowRisky = s.Length > 1 && s[1].ToLowerInvariant() == "on"; Log("unsafe=" + allowRisky); break;
                        case "key": case "type": Log(n + " '" + cmd + "' ignorado (sin inyección de teclado)"); break;
                        default: Log(n + " comando desconocido '" + cmd + "'"); break;
                    }
                }
                catch (Exception e) { Log(n + " '" + cmd + "' argumentos inválidos: " + e.Message); }
                if (step != null)
                {
                    // MoveNext in a loop so one failing step cannot kill the whole script.
                    while (true)
                    {
                        bool more; try { more = step.MoveNext(); } catch (Exception e) { Log(n + " '" + cmd + "' falló: " + e); break; }
                        if (!more) break; yield return step.Current;
                    }
                }
            }
            Log("fin"); Status("DONE");
        }

        private IEnumerator Wait(float seconds) { yield return new WaitForSecondsRealtime(seconds); }

        private IEnumerator Shot(string label)
        {
            yield return new WaitForEndOfFrame();
            // screencapture module is not in Packages/manifest.json: read the frame buffer instead.
            var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0); tex.Apply();
            try { File.WriteAllBytes(Path.Combine(folder, Regex.Replace(label, "[^A-Za-z0-9_-]", "_") + ".png"), ImageConversion.EncodeToPNG(tex)); Log("shot " + label); }
            finally { Destroy(tex); }
        }

        private PointerEventData NewPointer(Vector2 pos, bool right)
            => new PointerEventData(EventSystem.current) { position = pos, pressPosition = pos, button = right ? PointerEventData.InputButton.Right : PointerEventData.InputButton.Left };

        private bool Hit(PointerEventData data, out GameObject target)
        {
            target = null; var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
            if (hits.Count == 0) return false;
            data.pointerCurrentRaycast = data.pointerPressRaycast = hits[0]; target = hits[0].gameObject; return true;
        }

        private static string PathOf(GameObject go)
        {
            var parts = new List<string>(); for (var t = go.transform; t != null && parts.Count < 8; t = t.parent) parts.Add(t.name);
            parts.Reverse(); return string.Join("/", parts);
        }

        private IEnumerator Click(Vector2 pos, bool right)
        {
            pointer = pos;
            if (EventSystem.current == null) { Log("click: no hay EventSystem"); yield break; }
            var data = NewPointer(pos, right); GameObject target;
            if (!Hit(data, out target)) { Log("click (" + (int)pos.x + "," + (int)pos.y + ") sin objetivo"); yield break; }
            string where = PathOf(target);
            if (!allowRisky && Risky.IsMatch(where)) { Log("click BLOQUEADO (regla sin giros/compras): " + where); yield break; }
            Log("click (" + (int)pos.x + "," + (int)pos.y + ") -> " + where);
            ExecuteEvents.ExecuteHierarchy(target, data, ExecuteEvents.pointerEnterHandler);
            var press = ExecuteEvents.ExecuteHierarchy(target, data, ExecuteEvents.pointerDownHandler);
            if (press == null) press = ExecuteEvents.GetEventHandler<IPointerClickHandler>(target);
            data.pointerPress = press; data.rawPointerPress = target; data.eligibleForClick = true; data.clickCount = 1;
            yield return null;
            ExecuteEvents.ExecuteHierarchy(target, data, ExecuteEvents.pointerUpHandler);
            if (press != null && ExecuteEvents.GetEventHandler<IPointerClickHandler>(target) == press)
                ExecuteEvents.Execute(press, data, ExecuteEvents.pointerClickHandler);
            ExecuteEvents.ExecuteHierarchy(target, data, ExecuteEvents.pointerExitHandler);
            yield return null;
        }

        private IEnumerator Scroll(float notches)
        {
            if (EventSystem.current == null) yield break;
            int count = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(notches))); float sign = Mathf.Sign(notches);
            for (int i = 0; i < count; i++)
            {
                var data = NewPointer(pointer, false); data.scrollDelta = new Vector2(0, sign); GameObject target;
                if (Hit(data, out target)) ExecuteEvents.ExecuteHierarchy(target, data, ExecuteEvents.scrollHandler);
                yield return null;
            }
            Log("scroll " + notches);
        }

        private IEnumerator Drag(Vector2 from, Vector2 to)
        {
            if (EventSystem.current == null) yield break;
            var data = NewPointer(from, false); GameObject target;
            if (!Hit(data, out target)) { Log("drag sin objetivo"); yield break; }
            if (!allowRisky && Risky.IsMatch(PathOf(target))) { Log("drag BLOQUEADO: " + PathOf(target)); yield break; }
            Log("drag -> " + PathOf(target));
            var press = ExecuteEvents.ExecuteHierarchy(target, data, ExecuteEvents.pointerDownHandler);
            data.pointerPress = press; data.rawPointerPress = target;
            var dragger = ExecuteEvents.GetEventHandler<IDragHandler>(target);
            data.pointerDrag = dragger;
            ExecuteEvents.Execute(dragger, data, ExecuteEvents.initializePotentialDrag);
            ExecuteEvents.Execute(dragger, data, ExecuteEvents.beginDragHandler); data.dragging = true;
            const int frames = 12;
            for (int i = 1; i <= frames; i++)
            {
                var next = Vector2.Lerp(from, to, i / (float)frames); data.delta = next - data.position; data.position = next;
                ExecuteEvents.Execute(dragger, data, ExecuteEvents.dragHandler); yield return null;
            }
            ExecuteEvents.Execute(dragger, data, ExecuteEvents.endDragHandler); data.dragging = false;
            ExecuteEvents.ExecuteHierarchy(target, data, ExecuteEvents.pointerUpHandler);
            pointer = to;
        }
    }
}
