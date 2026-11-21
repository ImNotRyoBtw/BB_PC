using System;
using System.Collections.Generic;
using UnityEngine.Networking;
namespace BBPC.UnityRuntime
{
    // Captures the request object actually handed to Unity; never logs a raw URL, credential or ID.
    // Implicit native transport headers are not observable through GetRequestHeader.
    [Serializable] public sealed class RecoveredRequestEvidence
    {
        [Serializable] public sealed class Parameter {public string key,value;public int length;}
        public string method,host,path,cookie,contentType,userAgent,scope;
        public Parameter[] parameters;
        public static RecoveredRequestEvidence Capture(UnityWebRequest request)
        {
            var uri=new Uri(request.url);var values=new List<Parameter>();
            foreach(var field in uri.Query.TrimStart('?').Split('&'))
            {
                int equals=field.IndexOf('=');if(equals<0)continue;
                string key=Uri.UnescapeDataString(field.Substring(0,equals)),value=Uri.UnescapeDataString(field.Substring(equals+1));
                bool privateValue=key=="auid"||key=="unique_device_id"||key=="install_id"||key=="jms"||key=="rjm"||key=="afuid"||key=="aasqid"||key=="idfa"||key=="client_id";
                values.Add(new Parameter{key=key,length=value.Length,value=privateValue?(key=="install_id"?InstallShape(value):"<redacted>"):value});
            }
            values.Sort((a,b)=>StringComparer.Ordinal.Compare(a.key,b.key));
            return new RecoveredRequestEvidence{method=request.method,host=uri.Host,path=uri.AbsolutePath,parameters=values.ToArray(),cookie=string.IsNullOrEmpty(request.GetRequestHeader("Cookie"))?"absent":"<redacted>",contentType=request.GetRequestHeader("Content-Type"),userAgent=request.GetRequestHeader("User-Agent"),scope="Unity request object; explicit headers only; transport-generated headers unobserved"};
        }
        private static string InstallShape(string value)
        {Guid guid;int split=value.LastIndexOf('_');long stamp;if(split>0&&Guid.TryParseExact(value.Substring(0,split),"D",out guid)&&long.TryParse(value.Substring(split+1),out stamp))return "<UUID-D>_<epoch-ms>";return Guid.TryParseExact(value,"N",out guid)?"<UUID-N legacy preserved>":"<redacted other format>";}
    }
}
