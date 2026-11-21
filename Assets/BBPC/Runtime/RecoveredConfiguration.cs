using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Xml;
using System.Runtime.Serialization.Json;
namespace BBPC.UnityRuntime
{
    // Native managers store separate Dictionary<string,object> instances. Use the standard JSON/XML reader,
    // retaining nested dictionaries and arrays without introducing a second handwritten JSON tokenizer.
    public sealed class RecoveredConfiguration
    {
        public readonly Dictionary<string,object> Values;
        private RecoveredConfiguration(Dictionary<string,object> values){Values=values;}
        public static RecoveredConfiguration Parse(string json)
        {
            if(string.IsNullOrWhiteSpace(json)||json.Length>4*1024*1024)throw new FormatException("Invalid configuration size");
            try
            {
                var quotas=new XmlDictionaryReaderQuotas{MaxDepth=48,MaxStringContentLength=4*1024*1024,MaxArrayLength=200000};
                using(var reader=JsonReaderWriterFactory.CreateJsonReader(Encoding.UTF8.GetBytes(json),quotas))
                {
                    var document=new XmlDocument{XmlResolver=null};document.Load(reader);
                    var envelope=Read(document.DocumentElement) as Dictionary<string,object>;object status,data,err;
                    if(envelope==null||!envelope.TryGetValue("status",out status)||!(status is long)||(long)status!=1||!envelope.TryGetValue("data",out data)||!(data is Dictionary<string,object>))throw new FormatException("Configuration envelope rejected");
                    if(envelope.TryGetValue("err_code",out err)&&(!(err is long)||(long)err!=0))throw new FormatException("Configuration application error");
                    return new RecoveredConfiguration((Dictionary<string,object>)data);
                }
            }
            catch(XmlException e){throw new FormatException("Invalid configuration JSON",e);}
        }
        private static object Read(XmlElement element)
        {
            switch(element.GetAttribute("type"))
            {
                case "object":var map=new Dictionary<string,object>(StringComparer.Ordinal);foreach(XmlNode child in element.ChildNodes)if(child is XmlElement item)map.Add(item.HasAttribute("item")?item.GetAttribute("item"):XmlConvert.DecodeName(item.LocalName),Read(item));return map;
                case "array":var list=new List<object>();foreach(XmlNode child in element.ChildNodes)if(child is XmlElement item)list.Add(Read(item));return list;
                case "number":long integer;if(long.TryParse(element.InnerText,NumberStyles.Integer,CultureInfo.InvariantCulture,out integer))return integer;return double.Parse(element.InnerText,CultureInfo.InvariantCulture);
                case "boolean":return bool.Parse(element.InnerText);
                case "null":return null;
                default:return element.InnerText;
            }
        }
        public bool TryBoolean(string key,out bool value){object item;value=false;if(!Values.TryGetValue(key,out item)||!(item is bool))return false;value=(bool)item;return true;}
        public bool TryInteger(string key,out int value){object item;value=0;if(!Values.TryGetValue(key,out item)||!(item is long)||(long)item<int.MinValue||(long)item>int.MaxValue)return false;value=(int)(long)item;return true;}
    }
    public sealed class RecoveredOwnRolesPayload
    {
        public RecoveredServerRole default_bf,default_gf,default_revive,default_bf_singleplayer,default_gf_singleplayer;
        public RecoveredCustomRole[] bfs,gfs,revives;
        public RecoveredOwnedDecor[] decors;
    }
    // OwnRolesModel contains CustomBfModel in all three lists, never bare RoleModel.
    public sealed class RecoveredCustomRole
    {public RecoveredServerRole role;public SongRestriction restriction;public RecoveredArrangeState arrange_state;}
    public sealed class RecoveredArrangeState {public int state;public long ts;}
    public sealed class RecoveredOwnedDecor {public RecoveredServerDecor decor;public SongRestriction restriction;}
    public sealed class RecoveredServerDecor {public string did,title;public int type,depth;public RecoveredDecorLocation[] locations;public SongImage icon;public RemoteSongFile animation;}
    public sealed class RecoveredDecorLocation {public int location;public float[] anchor;}
}
