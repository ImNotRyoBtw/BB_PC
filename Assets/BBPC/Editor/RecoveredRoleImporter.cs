using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using BBPC.UnityRuntime;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
namespace BBPC.EditorTools
{
    public static class RecoveredRoleImporter
    {
        private const string Base="Assets/BBPC/Resources/RoleStages606";
        private static string Key(string id){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(System.Text.Encoding.UTF8.GetBytes(id))).Replace("-","").Substring(0,24).ToLowerInvariant();}
        [MenuItem("BB_PC/P3/Reconstruct original role prefabs")]
        public static void EnsureImported()
        {
            Import(Base+"/source.json",Base+"/Generated/source-sha256.txt");
            if(File.Exists(Base+"/Dunk/source.json"))Import(Base+"/Dunk/source.json",Base+"/Generated/dunk-source-sha256.txt");
            foreach(var path in Directory.GetFiles(Base,"source.json",SearchOption.AllDirectories).OrderBy(x=>x,StringComparer.Ordinal))
            {
                string normalized=path.Replace('\\','/');
                if(normalized==Base+"/source.json"||normalized==Base+"/Dunk/source.json"||normalized.StartsWith(Base+"/Generated/",StringComparison.Ordinal))continue;
                Import(normalized,Base+"/Generated/"+Key(normalized)+"-source-sha256.txt");
            }
            WriteStageIndex();
        }

        private static void WriteStageIndex()
        {
            var entries=new List<RecoveredRoleAssets.StageIndexEntry>();
            var keys=new HashSet<string>();
            foreach(var path in Directory.GetFiles(Base,"source.json",SearchOption.AllDirectories).OrderBy(x=>x,StringComparer.Ordinal))
            {
                var normalized=path.Replace('\\','/');
                if(normalized.StartsWith(Base+"/Generated/",StringComparison.Ordinal))continue;
                var data=RecoveredRoleAssets.ReadSource(File.ReadAllText(path));
                if(data?.stage==null||string.IsNullOrEmpty(data.stage.sid))throw new InvalidDataException("Invalid stage manifest "+path);
                string key=data.stage.sid+"\u001f"+(data.stage.aid??"");
                if(!keys.Add(key))throw new InvalidDataException("Duplicate stage SID/AID "+data.stage.sid);
                var relative=normalized.Substring(Base.Length+1);
                entries.Add(new RecoveredRoleAssets.StageIndexEntry{sid=data.stage.sid,aid=data.stage.aid,resource="RoleStages606/"+relative.Substring(0,relative.Length-5)});
            }
            var pathIndex=Base+"/stage-index.json";
            var json=JsonUtility.ToJson(new RecoveredRoleAssets.StageIndex{entries=entries.ToArray()});
            if(!File.Exists(pathIndex)||File.ReadAllText(pathIndex)!=json)
            {
                File.WriteAllText(pathIndex,json);
                AssetDatabase.ImportAsset(pathIndex,ImportAssetOptions.ForceSynchronousImport);
            }
        }

        private static void Import(string sourcePath,string stamp)
        {
            string json=File.ReadAllText(sourcePath),hash=Key("importer-v3-native-pivots\n"+json);
            var data=RecoveredRoleAssets.ReadSource(json);
            string folder=Base+"/Generated/"+data.stage.bundleMd5;
            // A matching manifest stamp does not prove its imported pivots survived a copied/reset .meta.
            // Read Unity's actual sprites before reusing generated assets (Star/Rayna was still CENTER).
            if(File.Exists(stamp)&&File.ReadAllText(stamp)==hash&&data.prefabs.All(p=>File.Exists(folder+"/"+p.name+".prefab"))&&data.assets.All(HasNativePivot))return;
            Directory.CreateDirectory(folder);AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach(var a in data.assets)
            {
                // Hand-written legacy .meta files were accepted with CENTER pivots. Set the real importer API,
                // then validate Sprite.pivot itself, rather than assuming serialized .meta text took effect.
                string path="Assets/BBPC/Resources/"+a.resource+".png";
                ConfigureSprite(path,new Vector2(a.pivot[0],a.pivot[1]),a.ppu,Vector4.zero);
            }
            var sprites=new Dictionary<string,Sprite>();foreach(var a in data.assets){var sprite=Resources.Load<Sprite>(a.resource);if(sprite==null)throw new InvalidDataException("Missing original sprite "+a.id);sprites.Add(a.id,sprite);}
            var clips=new Dictionary<string,AnimationClip>();
            foreach(var c in data.clips)
            {
                var clip=new AnimationClip{name=c.name,frameRate=c.rate,wrapMode=c.loop?WrapMode.Loop:WrapMode.ClampForever};
                if(c.keys.Length>0)
                {
                    var frames=c.keys.Select(k=>new ObjectReferenceKeyframe{time=k.time,value=sprites[k.sprite]}).ToList();
                    if(frames[frames.Count-1].time<c.duration)frames.Add(new ObjectReferenceKeyframe{time=c.duration,value=frames[frames.Count-1].value});
                    AnimationUtility.SetObjectReferenceCurve(clip,EditorCurveBinding.PPtrCurve("",typeof(SpriteRenderer),"m_Sprite"),frames.ToArray());
                }
                foreach(var f in c.floats)
                {
                    var keys=f.keys.Select(k=>new Keyframe(k.time,k.value,k.inSlope,k.outSlope)).ToList();if(keys[keys.Count-1].time<c.duration)keys.Add(new Keyframe(c.duration,keys[keys.Count-1].value));
                    AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(SpriteRenderer),f.property),new AnimationCurve(keys.ToArray()));
                }
                var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=c.loop;AnimationUtility.SetAnimationClipSettings(clip,settings);
                Save(clip,folder+"/clip-"+Key(c.id)+".anim");clips.Add(c.id,AssetDatabase.LoadAssetAtPath<AnimationClip>(folder+"/clip-"+Key(c.id)+".anim"));
            }
            var controllers=new Dictionary<string,AnimatorController>();
            foreach(var c in data.controllers)
            {
                string path=folder+"/controller-"+Key(c.id)+".controller";
                // Replace only these generated controllers; source evidence and user-authored assets stay untouched.
                if(File.Exists(path))AssetDatabase.DeleteAsset(path);
                var controller=AnimatorController.CreateAnimatorControllerAtPath(path);var machine=controller.layers[0].stateMachine;
                foreach(var s in c.states){var state=machine.AddState(s.name);state.motion=clips[s.clip];state.speed=s.speed;state.mirror=s.mirror;state.writeDefaultValues=true;if(s.name=="idle")machine.defaultState=state;}
                controllers.Add(c.id,controller);
            }
            var material=new Material(Shader.Find("Sprites/Default")){name="Recovered original Sprites-Default"};Save(material,folder+"/Sprites-Default.mat");material=AssetDatabase.LoadAssetAtPath<Material>(folder+"/Sprites-Default.mat");
            foreach(var prefab in data.prefabs)
            {
                var go=Node(prefab.root,sprites,controllers,material);
                try{PrefabUtility.SaveAsPrefabAsset(go,folder+"/"+prefab.name+".prefab");}finally{UnityEngine.Object.DestroyImmediate(go);}
            }
            AssetDatabase.SaveAssets();File.WriteAllText(stamp,hash);AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("BBPC reconstructed original roles: "+data.prefabs.Length+" prefabs, "+clips.Count+" clips, "+sprites.Count+" sprites; bundle="+data.stage.bundleMd5);
        }
        internal static void ConfigureSprite(string path,Vector2 pivot,float ppu,Vector4 border)
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
            settings.textureType=TextureImporterType.Sprite;settings.spriteMode=(int)SpriteImportMode.Single;settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=pivot;settings.spritePixelsPerUnit=ppu;settings.spriteBorder=border;settings.spriteMeshType=SpriteMeshType.FullRect;settings.mipmapEnabled=false;settings.alphaIsTransparency=true;
            importer.SetTextureSettings(settings);importer.wrapMode=TextureWrapMode.Clamp;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=8192;importer.SaveAndReimport();
        }
        private static bool HasNativePivot(RecoveredRoleSprite asset)
        {
            var sprite=Resources.Load<Sprite>(asset.resource);
            return sprite!=null&&Mathf.Abs(sprite.pixelsPerUnit-asset.ppu)<.0001f&&Vector2.Distance(sprite.pivot,new Vector2(asset.pivot[0]*sprite.rect.width,asset.pivot[1]*sprite.rect.height))<.001f;
        }
        private static void Save(UnityEngine.Object asset,string path){var old=AssetDatabase.LoadMainAssetAtPath(path);if(old!=null){EditorUtility.CopySerialized(asset,old);UnityEngine.Object.DestroyImmediate(asset);EditorUtility.SetDirty(old);}else AssetDatabase.CreateAsset(asset,path);}
        private static GameObject Node(RecoveredRoleNode n,Dictionary<string,Sprite> sprites,Dictionary<string,AnimatorController> controllers,Material material)
        {
            var go=new GameObject(n.name);go.transform.localPosition=new Vector3(n.position[0],n.position[1],n.position[2]);go.transform.localRotation=new Quaternion(n.rotation[0],n.rotation[1],n.rotation[2],n.rotation[3]);go.transform.localScale=new Vector3(n.scale[0],n.scale[1],n.scale[2]);
            if(n.hasRenderer){var r=n.renderer;var sr=go.AddComponent<SpriteRenderer>();sr.sprite=sprites[r.sprite];sr.color=new Color(r.color[0],r.color[1],r.color[2],r.color[3]);sr.flipX=r.flipX;sr.flipY=r.flipY;sr.enabled=r.enabled;sr.sortingLayerID=r.sortingLayer;sr.sortingOrder=r.sortingOrder;sr.sharedMaterial=material;}
            if(!string.IsNullOrEmpty(n.controller)){var animator=go.AddComponent<Animator>();animator.runtimeAnimatorController=controllers[n.controller];animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.speed=0;}
            foreach(var child in n.children){var g=Node(child,sprites,controllers,material);g.transform.SetParent(go.transform,false);}go.SetActive(n.active);return go;
        }
    }
}
