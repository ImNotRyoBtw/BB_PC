using System;
using System.IO;
using System.Text;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Collections.Generic;
namespace BBPC.UnityRuntime
{
    // Unmarked public DTOs keep missing fields optional in DataContractJsonSerializer.
    // JSON names come from 6.0.6 JsonProperty attributes, not C# member spelling.
    public sealed class SongCatalog { public SongItem[] items; public SongFilter[] filters;
        internal static T Read<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > 16 * 1024 * 1024) throw new FormatException("Empty/oversized catalog");
            try { using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json))) return (T)new DataContractJsonSerializer(typeof(T),new DataContractJsonSerializerSettings{MaxItemsInObjectGraph=2000000,UseSimpleDictionaryFormat=true}).ReadObject(stream); }
            catch (SerializationException e) { throw new FormatException("Invalid catalog JSON", e); }
        }
        public static SongCatalog Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Empty catalog");
            var result = Read<SongCatalog>(json);
            if (result == null || result.items == null) throw new FormatException("Expected SongListRespModel.items");
            Validate(result);
            return result;
        }
        internal static void Validate(SongCatalog catalog)
        {
            if(catalog==null||catalog.items==null)throw new FormatException("Expected SongListRespModel.items");
            var ids=new HashSet<string>();int count=0;
            foreach(var item in Enumerate(catalog.items))
            {
                if(++count>10000||item==null||item.stage==null||string.IsNullOrWhiteSpace(item.stage.sid)||!ids.Add(item.stage.Key))throw new FormatException("Missing/duplicate stage.sid+aid or excessive catalog");
            }
        }
        public static IEnumerable<SongItem> Enumerate(SongItem[] items,int depth=0)
        {
            if(depth>16)throw new FormatException("Catalog variant nesting limit exceeded");
            if(items==null)yield break;
            foreach(var item in items){yield return item;if(item!=null)foreach(var variant in Enumerate(item.more,depth+1))yield return variant;}
        }
    }
    public sealed class CatalogEnvelope { public int status; public int? err_code; public SongCatalog data;
        public static SongCatalog Parse(string json)
        {
            var envelope = SongCatalog.Read<CatalogEnvelope>(json);
            if (envelope == null || envelope.status != 1 || envelope.err_code.GetValueOrDefault()!=0 || envelope.data == null) throw new FormatException("Catalog envelope rejected");
            // Native model has recursive more[]; JsonUtility materializes it up to its depth limit even when absent.
            // Standard managed JSON reader preserves the existing DTO without that Unity serialization warning.
            SongCatalog.Validate(envelope.data);
            return envelope.data;
        }
    }
    public sealed class SongItem { public SongMetadata stage; public SongItem[] more;public SongRestriction restriction; }
    public sealed class SongRestriction { public int unlock_type,quantity;public bool? unlock_status;public long unlock_ts;public int? unlocked_ex_level; }
    public sealed class SongMetadata
    {
        public string sid, aid, title, artist,version_title,bf_name,boss_name,yid; public int difficulty,difficulty_v2, hp, play_type,genre,asset_flag; public float duration,bpm;
        public long ctime;public int[] genres;public string[] keywords;public bool coverable;
        public SongImage cover;public SongProgressIcons progress_icon;public SongGameConfig game_config;
        public SongSubMark sub_mark;
        public RemoteSongFile beatmap, beatmap_data;
        public string Key => sid+"\u001f"+(aid??"");
        // StageMetaModel.CurrentDifficulty 6.0.6: clamp v2 to 1..8. Never fall back to legacy difficulty.
        public int CurrentDifficulty => Math.Max(1,Math.Min(8,difficulty_v2));
    }
    public sealed class SongImage {public string[] urls;public string uri;public int width,height;}
    public sealed class SongProgressIcons {public SongImage light,dark;}
    public sealed class SongSubMark {public int tag,style;public string text;}
    public sealed class SongGameConfig {public string[] bfs,gfs,bosses,bgs,others;public int[] groups;public string bf,gf,boss,bg;public RemoteSongFile asset;}
    public sealed class RemoteSongFile { public string url, md5; }
    public sealed class SongFilter { public string id, title; public int type, tag, genre; }
    public enum CatalogOrigin { Unavailable, HistoricalTestCapture, Verified606Snapshot, Captured606Partial, ValidatedLocal606, PublicOffline606 }
    public sealed class SongSelection
    {
        private static readonly Dictionary<string,string> localVariants=new Dictionary<string,string>{{"gEYv\u001f9m6w","elastic"},{"lJY\u001fMZm","dunk"}};
        public static void RegisterVerifiedLocal(string sid,string aid,string variant)
        {if(string.IsNullOrWhiteSpace(sid)||string.IsNullOrWhiteSpace(variant))throw new ArgumentException("Verified local binding required");string key=sid+"\u001f"+(aid??"");string previous;if(localVariants.TryGetValue(key,out previous)&&previous!=variant)throw new InvalidOperationException("Ambiguous local stage binding");localVariants[key]=variant;}
        public static string LocalVariant(SongMetadata song)=>song==null?null:song.sid=="beginner_guider"?"normal":localVariants.TryGetValue(song.Key,out var variant)?variant:null;
        public static bool HasVerifiedLocalContent(SongMetadata song)=>LocalVariant(song)!=null;
        public SongMetadata Song { get; private set; }
        public CatalogOrigin Origin { get; private set; }
        public void Select(SongMetadata song, CatalogOrigin origin)
        {
            if (song == null || string.IsNullOrWhiteSpace(song.sid)) throw new ArgumentException("stage.sid required");
            if (origin == CatalogOrigin.Unavailable) throw new InvalidOperationException("Catalog unavailable");
            Song = song; Origin = origin;
        }
        // No network request, decryption guess, paid unlock or Android bundle execution is hidden here.
        public string PreparationBlocker()
        {
            if (Song == null) return "Catálogo 6.0.6 no disponible: no hay canción seleccionada.";
            if(Origin==CatalogOrigin.ValidatedLocal606)return HasVerifiedLocalContent(Song)?"":"Contenido local sin vínculo verificado.";
            if(Origin==CatalogOrigin.PublicOffline606)return "Snapshot público offline: faltan stage/info, recursos verificados y restricciones de la identidad actual.";
            if (Origin == CatalogOrigin.Captured606Partial) return "Captura 6.0.6 truncada: solo campos visibles; audio/chart no recuperados.";
            if (Origin != CatalogOrigin.Verified606Snapshot) return "Captura histórica solo para pruebas; no autoriza una partida 6.0.6.";
            if (Song.beatmap == null || Song.beatmap_data == null) return "Faltan las locaciones originales beatmap / beatmap_data.";
            return "Pendiente: verificar caché, integridad, descifrado, parser, assets y lifecycle de partida. No se ha preparado audio/chart.";
        }
    }
}
