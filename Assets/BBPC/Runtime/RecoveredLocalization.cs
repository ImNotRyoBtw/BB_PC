using System;
using System.Collections.Generic;
using UnityEngine;
namespace BBPC.UnityRuntime
{
    // Original LocalizedText keys and packaged es/en tables. Spanish is the current port preference.
    public static class RecoveredLocalization
    {
        [Serializable] private sealed class Table { public string[] keys = null, values = null; }
        private static readonly Dictionary<string, Dictionary<string,string>> tables = new Dictionary<string, Dictionary<string,string>>();
        public static string Language { get; set; } = "es";
        public static string Text(string key, string fallback)
        {
            if(string.IsNullOrEmpty(key)) return fallback;
            if(Language!="es" && Language!="en") throw new InvalidOperationException("Unrecovered locale: "+Language);
            if(!tables.TryGetValue(Language,out var table))
            {
                var asset=Resources.Load<TextAsset>("Localization606/"+Language);
                if(asset==null) throw new InvalidOperationException("Original locale table missing");
                var data=JsonUtility.FromJson<Table>(asset.text);
                if(data.keys==null || data.values==null || data.keys.Length!=data.values.Length) throw new FormatException("Invalid original locale projection");
                table=new Dictionary<string,string>();
                for(int i=0;i<data.keys.Length;i++) table.Add(data.keys[i],data.values[i]);
                tables.Add(Language,table);
            }
            return table.TryGetValue(key,out var value)?value:fallback;
        }
    }
}
