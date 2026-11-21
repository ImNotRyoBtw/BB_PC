using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace BBPC.UnityRuntime
{
    // Offline validation has its own storage; it must never mutate the live Windows identity or cache.
    public static class RecoveredStorage
    {
        private static readonly string isolated=ValidationRoot();
        private static string ValidationRoot()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-bbpc-validate");
            if(index<0)index=Array.IndexOf(args,"-bbpc-stage-check");if(index<0||index+1>=args.Length)return null;
            string output=Path.GetFullPath(args[index+1]);Directory.CreateDirectory(output);
            string root=Path.Combine(Path.GetTempPath(),"BBPC-v419-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(output,"isolated-root.txt"),root);return root;
        }
        public static bool Isolated=>isolated!=null;
        public static string Root=>isolated??Application.persistentDataPath;
    }
    public static class RecoveredPreferences
    {
        private static readonly Dictionary<string,string> strings=new Dictionary<string,string>();
        private static readonly Dictionary<string,int> integers=new Dictionary<string,int>();
        public static string GetString(string key,string fallback="")=>RecoveredStorage.Isolated?(strings.TryGetValue(key,out var value)?value:fallback):PlayerPrefs.GetString(key,fallback);
        public static int GetInt(string key,int fallback=0)=>RecoveredStorage.Isolated?(integers.TryGetValue(key,out int value)?value:fallback):PlayerPrefs.GetInt(key,fallback);
        public static void SetString(string key,string value){if(RecoveredStorage.Isolated)strings[key]=value;else PlayerPrefs.SetString(key,value);}
        public static void SetInt(string key,int value){if(RecoveredStorage.Isolated)integers[key]=value;else PlayerPrefs.SetInt(key,value);}
        public static void Save(){if(!RecoveredStorage.Isolated)PlayerPrefs.Save();}
    }
}
