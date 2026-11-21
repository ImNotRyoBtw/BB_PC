using System;
using System.Collections;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Networking;
namespace BBPC.UnityRuntime
{
    [Serializable] public sealed class TutorialDefinition
    {
        public string variant, sid, aid, title, artist, chartResource, chartSha256, audioFile, audioSha256, audioRelativePath;
        public int hp,difficulty,actions,playType,declaredDurationMs;
    }
    [Serializable] public sealed class TutorialManifest { public string version; public TutorialDefinition[] items; }
    public sealed class PreparedTutorial
    {
        public readonly TutorialDefinition Definition;
        public readonly RecoveredBeatmap Chart;
        public readonly AudioClip Audio;
        public PreparedTutorial(TutorialDefinition definition,RecoveredBeatmap chart,AudioClip audio) { Definition=definition;Chart=chart;Audio=audio; }
    }
    public static class RecoveredTutorialContent
    {
        private static string Hash(byte[] bytes) { using(var hash=SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant(); }
        public static TutorialDefinition Definition(string variant)
        {
            if(variant!="normal"&&variant!="fnf")return RecoveredStageContent.LocalDefinition(variant);
            var source=Resources.Load<TextAsset>("Tutorial606/manifest");
            if(source==null)throw new InvalidOperationException("Original tutorial manifest missing");
            var manifest=JsonUtility.FromJson<TutorialManifest>(source.text);
            if(manifest.version!="6.0.6"||manifest.items==null)throw new FormatException("Wrong tutorial manifest");
            return Array.Find(manifest.items,x=>x.variant==variant)??throw new FormatException("Tutorial variant missing");
        }
        public static RecoveredBeatmap ReadChart(TutorialDefinition definition)
        {
            var source=Resources.Load<TextAsset>(definition.chartResource);
            if(source==null || Hash(System.Text.Encoding.UTF8.GetBytes(source.text))!=definition.chartSha256) throw new InvalidDataException("Packaged chart SHA256 mismatch");
            var chart=RecoveredBeatmap.Parse(source.text);
            if(chart.actions.Length!=definition.actions) throw new FormatException("Tutorial action count mismatch");
            return chart;
        }
        public static IEnumerator Prepare(string variant,Action<PreparedTutorial> ready,Action<string> failed,Action<float> progress)
        {
            TutorialDefinition definition=null;RecoveredBeatmap chart=null;string path=null;Exception invalid=null;
            try
            {
                definition=Definition(variant); chart=ReadChart(definition);
                string relative=definition.audioRelativePath;
                if(string.IsNullOrEmpty(relative))relative=definition.variant=="dunk"?"Recovered606/Dunk/"+definition.audioFile:definition.variant=="elastic"?"Recovered606/Elastic/"+definition.audioFile:"Recovered606/Tutorial/"+definition.audioFile;
                path=Path.GetFullPath(Path.Combine(Application.streamingAssetsPath,relative));
                if(!path.StartsWith(Path.GetFullPath(Application.streamingAssetsPath)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new FormatException("Audio path outside packaged content");
                if(Hash(File.ReadAllBytes(path))!=definition.audioSha256) throw new InvalidDataException("Packaged audio SHA256 mismatch");
            }
            catch(Exception error) { invalid=error; }
            if(invalid!=null) { Debug.LogException(invalid);failed(invalid.Message);yield break; }
            yield return PrepareAudio(definition,chart,path,ready,failed,progress);
        }
        internal static IEnumerator PrepareAudio(TutorialDefinition definition,RecoveredBeatmap chart,string path,Action<PreparedTutorial> ready,Action<string> failed,Action<float> progress)
        {
            progress(0);
            // Local file only. No API/CDN request, Android bundle or captured identity is used here.
            using(var request=UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri,AudioType.MPEG))
            {
                var operation=request.SendWebRequest();
                while(!operation.isDone) { progress(Mathf.Clamp(request.downloadProgress,0,0.99f)); yield return null; }
                if(request.result!=UnityWebRequest.Result.Success) { Debug.LogError(request.error);failed(request.error);yield break; }
                var audio=DownloadHandlerAudioClip.GetContent(request);
                if(audio==null || audio.samples<=0 || audio.channels<=0 || audio.length*1000<chart.LastEndMs)
                { Debug.LogError("Packaged audio did not cover its native chart");failed("Packaged audio did not cover its native chart");yield break; }
                progress(1);ready(new PreparedTutorial(definition,chart,audio));
            }
        }
    }
}
