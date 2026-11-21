using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
namespace BBPC.EditorTools
{
    // Local evidence validator only. These files are neither imported into the port nor assigned a SID.
    public static class CapturedContentValidation
    {
        [Serializable] private sealed class AudioMetadata { public string md5 = null; }
        [Serializable] private sealed class ChartAction { public int ts = 0, key = 0, duration = 0, node_type = 0, action_type = 0; }
        [Serializable] private sealed class Chart { public ChartAction[] actions = null; public AudioMetadata audio = null; }
        [Serializable] private sealed class Receipt
        {
            public bool characterized, jsonParsed, audioDecoded, waveformNonZero, chartAudioMd5Matches, sidAssociationVerified, readyForGameplay;
            public int actions, durationPositiveActions, lastActionEndMs, audioChannels, audioSamples, audioFrequency;
            public float audioSeconds;
            public string editorVersion, chartSha256, audioSha256, expectedAudioMd5, actualAudioMd5, error;
        }
        private static string Hash(byte[] bytes, HashAlgorithm algorithm)
        { using(algorithm) return BitConverter.ToString(algorithm.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        public static void RunIfAvailable()
        {
            string workspace=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));
            string directory=Path.Combine(workspace,"analysis/p2-content");
            string chartPath=Path.Combine(directory,"payload-bpm.bin"), audioPath=Path.Combine(directory,"payload-bpmd.bin");
            if(!File.Exists(chartPath) || !File.Exists(audioPath)) return;
            const string temporary="Assets/BBPC_CapturedAudioValidation.mp3";
            if(File.Exists(temporary)) throw new IOException("Content validation temporary asset already exists; refusing to overwrite");
            var result=new Receipt { editorVersion=Application.unityVersion };
            bool created=false;
            try
            {
                byte[] chartBytes=File.ReadAllBytes(chartPath), audioBytes=File.ReadAllBytes(audioPath);
                result.chartSha256=Hash(chartBytes,SHA256.Create()); result.audioSha256=Hash(audioBytes,SHA256.Create());
                var chart=JsonUtility.FromJson<Chart>(System.Text.Encoding.UTF8.GetString(chartBytes));
                if(chart==null || chart.actions==null || chart.actions.Length==0 || chart.audio==null) throw new FormatException("Incomplete captured chart");
                result.actions=chart.actions.Length; result.jsonParsed=true;
                foreach(var action in chart.actions)
                {
                    if(action==null || action.ts<0 || action.duration<0 || action.key<0 || action.key>3) throw new FormatException("Captured action outside observed scalar domains");
                    if(action.duration>0) result.durationPositiveActions++;
                    result.lastActionEndMs=Math.Max(result.lastActionEndMs,checked(action.ts+action.duration));
                }
                result.expectedAudioMd5=chart.audio.md5; result.actualAudioMd5=Hash(audioBytes,MD5.Create());
                result.chartAudioMd5Matches=string.Equals(result.expectedAudioMd5,result.actualAudioMd5,StringComparison.OrdinalIgnoreCase);
                File.WriteAllBytes(temporary,audioBytes); created=true;
                AssetDatabase.ImportAsset(temporary,ImportAssetOptions.ForceSynchronousImport);
                var importer=AssetImporter.GetAtPath(temporary) as AudioImporter;
                if(importer==null) throw new InvalidOperationException("Unity did not create AudioImporter");
                var settings=importer.defaultSampleSettings; settings.loadType=AudioClipLoadType.DecompressOnLoad;
                importer.defaultSampleSettings=settings; importer.SaveAndReimport();
                var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(temporary);
                if(clip==null || clip.samples<=0 || clip.channels<=0 || clip.frequency<=0) throw new InvalidOperationException("Unity audio decoding failed");
                result.audioChannels=clip.channels; result.audioSamples=clip.samples; result.audioFrequency=clip.frequency; result.audioSeconds=clip.length;
                float[] samples=new float[Math.Min(32768,clip.samples)*clip.channels];
                foreach(int offset in new[]{0,clip.samples/3,clip.samples*2/3})
                {
                    if(!clip.GetData(samples,offset)) throw new InvalidOperationException("Unity could not read decoded audio samples");
                    foreach(float sample in samples) { if(float.IsNaN(sample)||float.IsInfinity(sample)) throw new FormatException("Non-finite decoded sample"); if(Math.Abs(sample)>0.00001f) result.waveformNonZero=true; }
                }
                result.audioDecoded=true; result.characterized=result.waveformNonZero;
                if(!result.characterized) throw new InvalidOperationException("Decoded audio has no nonzero samples in inspected ranges");
                // BeatmapModel ignores audio.md5. Native cache checks RemoteLocation.md5 against encrypted bytes.
                // This pair still lacks an associated SID and the authoritative RemoteLocation values.
                result.sidAssociationVerified=false; result.readyForGameplay=false;
            }
            catch(Exception error) { result.error=error.GetType().Name+": "+error.Message; throw; }
            finally
            {
                if(created && !AssetDatabase.DeleteAsset(temporary)) throw new IOException("Could not remove owned temporary audio validation asset");
                File.WriteAllText(Path.Combine(directory,"unity-content-results.json"),JsonUtility.ToJson(result,true));
            }
        }
    }
}
