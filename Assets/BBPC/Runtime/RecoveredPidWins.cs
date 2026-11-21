using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BBPC.UnityRuntime
{
    // LotteryModelV2.OwnedProbabilityInfos (Dictionary<string, PidInfo>) = the zone's "pid_wins": {"<pid>": {"count": n, "max": m}}.
    // JsonUtility cannot read dictionaries, so this reads the raw shop response with a tiny parser and fills zone.pid_infos.
    // PidInfo (Ghidra): IsUnlimited = max missing or max < 0; HasAll = !IsUnlimited && max <= count.
    [Serializable] public sealed class RecoveredPidInfo { public string pid; public int count; public int max = -1; public bool IsUnlimited => max < 0; public bool HasAll => !IsUnlimited && max <= count; }

    public static class RecoveredPidWins
    {
        public static void Attach(RecoveredShopPayload payload, string body)
        {
            if (payload == null || string.IsNullOrEmpty(body)) return;
            object root; try { root = new Reader(body).Value(); } catch (Exception) { return; }
            var byId = new Dictionary<string, RecoveredRaffleZone>();
            foreach (var list in new[] { payload.lottery_zone_v2, payload.themed_lottery_zone, payload.vs_legacy_zone, payload.vs_legacy_premium_zone })
                if (list != null) foreach (var z in list) if (z != null && !string.IsNullOrEmpty(z.id)) byId[z.id] = z;
            Walk(root, byId);
        }

        private static void Walk(object node, Dictionary<string, RecoveredRaffleZone> zones)
        {
            if (node is Dictionary<string, object> obj)
            {
                if (obj.TryGetValue("id", out var id) && id is string sid && obj.TryGetValue("pid_wins", out var pw) && pw is Dictionary<string, object> wins && zones.TryGetValue(sid, out var zone))
                {
                    var infos = new List<RecoveredPidInfo>();
                    foreach (var pair in wins)
                    {
                        var info = new RecoveredPidInfo { pid = pair.Key };
                        if (pair.Value is Dictionary<string, object> d)
                        {
                            if (d.TryGetValue("count", out var c) && c is double cd) info.count = (int)cd;
                            if (d.TryGetValue("max", out var m) && m is double md) info.max = (int)md;
                        }
                        infos.Add(info);
                    }
                    zone.pid_infos = infos.ToArray();
                }
                foreach (var v in obj.Values) Walk(v, zones);
            }
            else if (node is List<object> list) foreach (var v in list) Walk(v, zones);
        }

        public static RecoveredPidInfo Find(RecoveredRaffleZone zone, string pid)
        {
            if (zone?.pid_infos == null || pid == null) return null;
            foreach (var i in zone.pid_infos) if (i != null && i.pid == pid) return i;
            return null;
        }

        // LotteryModelV2.CheckWins (Ghidra): type 3 (themed) -> OwnedProbabilityInfos[pid].HasAll; type 2 -> OwnedProbabilityIDs contains pid.
        // OwnedProbabilityIDs source (zone "wins") is not captured for type 2 zones, so only the themed branch is evaluated here.
        public static bool CheckWins(RecoveredRaffleZone zone, RecoveredProbability probability)
        {
            if (zone == null || probability == null || zone.type != 3) return false;
            var info = Find(zone, probability.pid); return info != null && info.HasAll;
        }

        private sealed class Reader
        {
            private readonly string s; private int i;
            public Reader(string text) { s = text; }
            private void Ws() { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }
            public object Value()
            {
                Ws(); if (i >= s.Length) throw new FormatException();
                char c = s[i];
                if (c == '{') { i++; var d = new Dictionary<string, object>(); Ws(); if (s[i] == '}') { i++; return d; } while (true) { Ws(); string k = Str(); Ws(); Expect(':'); d[k] = Value(); Ws(); if (s[i] == ',') { i++; continue; } Expect('}'); return d; } }
                if (c == '[') { i++; var l = new List<object>(); Ws(); if (s[i] == ']') { i++; return l; } while (true) { l.Add(Value()); Ws(); if (s[i] == ',') { i++; continue; } Expect(']'); return l; } }
                if (c == '"') return Str();
                int st = i; while (i < s.Length && ",}] \t\r\n".IndexOf(s[i]) < 0) i++;
                string t = s.Substring(st, i - st);
                if (t == "true") return true; if (t == "false") return false; if (t == "null") return null;
                return double.Parse(t, NumberStyles.Float, CultureInfo.InvariantCulture);
            }
            private void Expect(char c) { if (s[i] != c) throw new FormatException(); i++; }
            private string Str()
            {
                Expect('"'); var sb = new StringBuilder();
                while (s[i] != '"')
                {
                    if (s[i] == '\\') { i++; char e = s[i++]; switch (e) { case 'n': sb.Append('\n'); break; case 't': sb.Append('\t'); break; case 'r': sb.Append('\r'); break; case 'b': sb.Append('\b'); break; case 'f': sb.Append('\f'); break; case 'u': sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16)); i += 4; break; default: sb.Append(e); break; } }
                    else sb.Append(s[i++]);
                }
                i++; return sb.ToString();
            }
        }
    }
}
