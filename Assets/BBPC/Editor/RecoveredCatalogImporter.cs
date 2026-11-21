using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace BBPC.EditorTools
{
    public static class RecoveredCatalogImporter
    {
        [Serializable] private sealed class Manifest {public Asset[] assets=null;}
        [Serializable] private sealed class Vector {public float X=0,Y=0,Z=0,W=0;}
        [Serializable] private sealed class Asset {public string type=null,resource=null;public Vector pivot=null,border=null;public float pixelsPerUnit=0;}
        public static void EnsureImported()
        {
            foreach(var path in new[]{"Assets/BBPC/Resources/CatalogUi606/story-ui.json","Assets/BBPC/Resources/FreeplayUi606/ui.json","Assets/BBPC/Resources/FreeplayUi606/dialog.json"})Import(path);
        }
        private static void Import(string path)
        {
            string stamp=path+".imported.txt";
            string json=File.ReadAllText(path);string hash=Hash128.Compute("story-ui-v1:"+json).ToString();if(File.Exists(stamp)&&File.ReadAllText(stamp)==hash)return;
            foreach(var a in JsonUtility.FromJson<Manifest>(json).assets)if(a.type=="Sprite")RecoveredRoleImporter.ConfigureSprite("Assets/BBPC/Resources/"+a.resource+".png",new Vector2(a.pivot.X,a.pivot.Y),a.pixelsPerUnit,new Vector4(a.border.X,a.border.Y,a.border.Z,a.border.W));
            File.WriteAllText(stamp,hash);AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }
    }
}
