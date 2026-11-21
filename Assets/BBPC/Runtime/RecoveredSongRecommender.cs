using System;
using System.Collections.Generic;
using BBPC.Gameplay;
namespace BBPC.UnityRuntime
{
    // Réplica of SongsManager.RecommendedStageModels() (Beat Battle 7.7.7, RVA 0x2CAF7DC).
    // Generates (1) the 10 result-room stages / the "rsids" field of POST /beatmusic/result/report, and
    // (2) RecommendedSongSids = the sids that carry the "hot" tag in the result room.
    //
    // Everything is computed on the client. The server never sends rsids: neither stage/list nor the
    // result/report response carries a recommend field. Evidence:
    //   "Beat Battle 7.7.7 - rsids y etiqueta hot (guía para el port a Unity PC).md" section 3 (algorithm)
    //   and section 4 (two real cases reproduced in Python against captured result/report bodies).
    //
    // The port already owns the stage/list models (SongItem/SongMetadata/SongRestriction), so this is an
    // adaptation, not a re-derivation: Sid -> stage.sid, Genres -> stage.genres, IsLocked -> restriction,
    // More -> more[]. The algorithm itself is the observed one.
    public enum RecoveredRecommendPlayMode { Stage = 0, Story = 1, Adventure = 2, RoleChallenge = 3, MatchOffline = 4,
        MatchOnline = 5, PrivateRoom = 6, Arena = 7, BossFight = 8, WorldBoss = 9, TeamVS = 10, Trial = 11 }

    public sealed class RecoveredRecommendation
    {
        public List<SongItem> Stages = new List<SongItem>();   // up to 10, in order
        public List<string> Rsids = new List<string>();        // unique sids of Stages -> body of result/report
        public List<string> HotSids = new List<string>();      // RecommendedSongSids (hot tag)
        public bool HotUpdated;                                // false in Story/RoleChallenge or with no current song
    }

    public static class RecoveredSongRecommender
    {
        const int Max = 10;    // final size
        const int Head = 2;    // initial quota for E and for A

        /// all    = items of stage/list in their original order (SongsManager.stageModels).
        /// playingSid/playingAid = the song that was played (may be a version living inside a parent's more[]).
        public static RecoveredRecommendation Build(IReadOnlyList<SongItem> all, string playingSid, string playingAid, RecoveredRecommendPlayMode mode)
        {
            var r = new RecoveredRecommendation();
            if (mode == RecoveredRecommendPlayMode.Story || mode == RecoveredRecommendPlayMode.RoleChallenge || string.IsNullOrEmpty(playingSid)) return r;

            // Locate parent (the list item) and playing (the model actually played, possibly a 'more' variant).
            SongItem parent = null, playing = null;
            foreach (var s in all)
            {
                if (Match(s.stage, playingSid, playingAid)) { parent = s; playing = s; break; }
                if (s.more != null)
                    foreach (var m in s.more)
                        if (Match(m.stage, playingSid, playingAid)) { parent = s; playing = m; goto found; }
            }
            found:
            if (playing == null) return r;

            var more = parent.more;                       // null => no versions
            var E = new List<SongItem>();                 // blocked 'more' versions, distinct from the one played
            var A = new List<SongItem>();                 // same genre + locked
            var B = new List<SongItem>();                 // different genre + locked
            var C = new List<SongItem>();                 // same genre + unlocked
            var D = new List<SongItem>();                 // different genre + unlocked

            if (more != null)
                foreach (var m in more)
                    if (!string.Equals(m.stage.sid, playingSid, StringComparison.Ordinal) && IsLocked(m)) E.Add(m);

            int? g0 = FirstGenre(playing);
            foreach (var s in all)
            {
                if (string.Equals(s.stage.sid, playingSid, StringComparison.Ordinal)) continue;
                if (more != null && s == parent) continue;       // the parent item is excluded too
                bool same = g0.HasValue && FirstGenre(s) == g0;
                if (IsLocked(s)) (same ? A : B).Add(s);
                else (same ? C : D).Add(s);
            }

            var res = r.Stages;
            var sids = new HashSet<string>();
            void Add(SongItem x) { res.Add(x); sids.Add(x.stage.sid); }

            foreach (var x in E) { if (res.Count >= Head) break; Add(x); }
            foreach (var x in A) { if (res.Count >= Head) break; Add(x); }
            foreach (var list in new[] { A, B, C, D })
                foreach (var x in list)
                {
                    if (res.Count >= Max) break;
                    if (!sids.Contains(x.stage.sid)) Add(x);
                }

            foreach (var sid in Distinct(res)) r.Rsids.Add(sid);

            // RecommendedSongSids: walk the result from the start, adding each sid, stop at the first one NOT in E.
            r.HotUpdated = true;
            foreach (var x in res)
            {
                r.HotSids.Add(x.stage.sid);
                if (!E.Contains(x)) break;
            }
            return r;
        }

        // Call when stage/unlocking answers unlock_status = true; it affects future rsids.
        public static void MarkUnlocked(IEnumerable<SongItem> all, string sid)
        {
            foreach (var s in all)
            {
                if (string.Equals(s.stage.sid, sid, StringComparison.Ordinal)) Unlock(s);
                if (s.more != null)
                    foreach (var m in s.more)
                        if (string.Equals(m.stage.sid, sid, StringComparison.Ordinal)) Unlock(m);
            }
        }

        static void Unlock(SongItem s)
        {
            if (s.restriction == null) s.restriction = new SongRestriction();
            s.restriction.unlock_status = true;
        }

        static bool IsLocked(SongItem s) => s.restriction == null || s.restriction.unlock_status != true;

        static int? FirstGenre(SongItem s)
        {
            var g = s.stage?.genres;
            return (g == null || g.Length == 0) ? (int?)null : g[0];
        }

        static bool Match(SongMetadata stage, string sid, string aid)
        {
            if (stage == null || !string.Equals(stage.sid, sid, StringComparison.Ordinal)) return false;
            return string.IsNullOrEmpty(aid) || string.Equals(stage.aid, aid, StringComparison.Ordinal);
        }

        static IEnumerable<string> Distinct(List<SongItem> l)
        {
            var seen = new HashSet<string>();
            foreach (var x in l) if (seen.Add(x.stage.sid)) yield return x.stage.sid;
        }
    }
}