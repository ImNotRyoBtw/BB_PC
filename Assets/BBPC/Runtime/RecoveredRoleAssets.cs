using System;
using System.Collections.Generic;
using UnityEngine;
namespace BBPC.UnityRuntime
{
    public sealed class RecoveredRoleSource { public string version;public int converterVersion;public RecoveredRoleStage stage;public RecoveredRoleSprite[] assets;public RecoveredRoleClip[] clips;public RecoveredRoleController[] controllers;public RecoveredRolePrefab[] prefabs; }
    public sealed class RecoveredRoleStage { public string sid,aid,bundleMd5,progressDarkResource,progressLightResource,cameraConfigSource;public string[] bfs,bosses,gfs,bgs;public int assetFlag;public bool assetFlagKnown;public float cameraSize;public float[][] cameraPositions;public float[] cameraSizes; }
    public sealed class RecoveredRoleSprite { public string id,resource,sha256;public float[] pivot,rect;public float ppu;public float[][] vertices;public ushort[] triangles; }
    public sealed class RecoveredRoleClip { public string id,name;public float rate,duration;public bool loop;public RecoveredRoleKey[] keys;public RecoveredRoleTrack[] tracks;public RecoveredRoleFloat[] floats; }
    public sealed class RecoveredRoleTrack {public string path;public RecoveredRoleKey[] keys;}
    public sealed class RecoveredRoleKey { public float time;public string sprite; }
    public sealed class RecoveredRoleFloat { public string path,target,property;public RecoveredRoleFloatKey[] keys; }
    public sealed class RecoveredRoleFloatKey { public float time,value,inSlope,outSlope;public bool infiniteIn,infiniteOut; }
    public sealed class RecoveredRoleController { public string id,name;public int defaultState;public RecoveredRoleState[] states; }
    public sealed class RecoveredRoleState { public string name,clip;public float speed;public bool mirror;public RecoveredRoleTransition[] transitions; }
    public sealed class RecoveredRoleTransition {public int destination;public float exitTime;}
    public sealed class RecoveredRolePrefab { public string name,source,resource;public RecoveredRoleNode root; }
    public sealed class RecoveredRoleNode { public string id,name,controller;public float[] position,rotation,scale;public bool active,hasRenderer,hasAnimatorSettings,animatorEnabled,keepAnimatorState;public RecoveredRoleRenderer renderer;public RecoveredRoleNode[] children;public RecoveredBossExtraData bossExtra; }
    public sealed class RecoveredBossExtraData {public string source,script;public int orderInLayer;public bool enabled;}
    public sealed class RecoveredRoleRenderer { public string sprite;public float[] color;public bool flipX,flipY,enabled;public int sortingOrder,sortingLayer; }
    public static class RecoveredRoleAssets
    {
        private static RecoveredRoleSource source;
        public static RecoveredRoleSource ReadSource(string json)=>SongCatalog.Read<RecoveredRoleSource>(json);
        public static RecoveredRoleSource Source => source ?? (source=ReadSource(Resources.Load<TextAsset>("RoleStages606/source").text));

        // Entries are generated once by the Editor importer; loading 3,000 manifests on a click
        // stalls Unity's main thread and unnecessarily retains every stage's metadata.
        [Serializable] public sealed class StageIndex { public StageIndexEntry[] entries; }
        [Serializable] public sealed class StageIndexEntry { public string sid, aid, resource; }
        private static Dictionary<string,string> indexedPaths;
        private static Dictionary<string,RecoveredRoleSource> legacyPacks;
        private static readonly Dictionary<string,RecoveredRoleSource> recent = new Dictionary<string,RecoveredRoleSource>();
        private static readonly Queue<string> recentKeys = new Queue<string>();
        private const int CacheLimit = 8;
        private static string StageKey(string sid,string aid) => sid+"\u001f"+(aid??"");
        public static RecoveredRoleSource ForStage(string sid,string aid)
        {
            if(string.IsNullOrEmpty(sid))return null;
            var dynamicPack=RecoveredDynamicRoles.ForStage(sid,aid);if(dynamicPack!=null)return dynamicPack;
            if(indexedPaths==null)
            {
                indexedPaths=new Dictionary<string,string>();
                var index=Resources.Load<TextAsset>("RoleStages606/stage-index");
                if(index==null)
                {
                    // Compatibility only: older builds do not have the generated stage-index.
                    legacyPacks=new Dictionary<string,RecoveredRoleSource>();
                    foreach(var asset in Resources.LoadAll<TextAsset>("RoleStages606"))
                    {
                        if(asset.name!="source")continue;
                        var pack=ReadSource(asset.text);
                        if(pack?.stage!=null)legacyPacks.Add(StageKey(pack.stage.sid,pack.stage.aid),pack);
                    }
                }
                else
                {
                    var parsed=JsonUtility.FromJson<StageIndex>(index.text);
                    if(parsed?.entries==null)throw new FormatException("Role stage-index is invalid");
                    foreach(var entry in parsed.entries)
                    {
                        if(string.IsNullOrEmpty(entry.sid)||string.IsNullOrEmpty(entry.resource))throw new FormatException("Invalid role stage index row");
                        indexedPaths.Add(StageKey(entry.sid,entry.aid),entry.resource);
                    }
                }
            }
            var key=StageKey(sid,aid);
            if(legacyPacks!=null)
            {
                RecoveredRoleSource legacy;
                return legacyPacks.TryGetValue(key,out legacy)?legacy:null;
            }
            RecoveredRoleSource cached;
            if(recent.TryGetValue(key,out cached))return cached;
            string resource;
            if(!indexedPaths.TryGetValue(key,out resource))return null;
            var text=Resources.Load<TextAsset>(resource);
            if(text==null)throw new InvalidOperationException("Indexed stage source is missing: "+resource);
            var source=ReadSource(text.text);
            if(source?.stage==null||StageKey(source.stage.sid,source.stage.aid)!=key)throw new FormatException("Stage source does not match indexed SID/AID");
            if(recent.Count>=CacheLimit)recent.Remove(recentKeys.Dequeue());
            recent.Add(key,source);recentKeys.Enqueue(key);
            return source;
        }
        public static string PrefabResource(string bundleMd5,string name) => "RoleStages606/Generated/"+bundleMd5+"/"+name;
        public static GameObject Instantiate(string bundleMd5,string name,Transform parent)
        {
            if(name=="NoAnimation")return null;
            if(RecoveredDynamicRoles.Contains(bundleMd5))return RecoveredDynamicRoles.Instantiate(bundleMd5,name,parent);
            var prefab=Resources.Load<GameObject>(PrefabResource(bundleMd5,name));
            if(prefab==null)throw new InvalidOperationException("Original role prefab not reconstructed: "+bundleMd5+"/"+name);
            return UnityEngine.Object.Instantiate(prefab,parent,false);
        }
    }
}
