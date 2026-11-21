using System;
using System.IO;
using BBPC.Gameplay;
using BBPC.UnityRuntime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace BBPC.EditorTools
{
    public static class P3Validation
    {
        [MenuItem("BB_PC/P3/Prepare original local tutorial in Play Mode")]
        public static void OpenLocalTutorial()
        { if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play Mode first");UnityEngine.Object.FindObjectOfType<RecoveredFrontend>().PrepareLocalTutorial("normal"); }
        public static int Check(RecoveredFrontend frontend)
        {
            int count=0;
            foreach(var variant in new[]{"normal","fnf"})
            {
                var definition=RecoveredTutorialContent.Definition(variant);var chart=RecoveredTutorialContent.ReadChart(definition);
                if(definition.sid!="beginner_guider" || chart.actions.Length!=(variant=="normal"?46:56))throw new Exception("Native tutorial binding failed");count++;
                string audio=Path.Combine(Application.streamingAssetsPath,"Recovered606/Tutorial",definition.audioFile);
                using(var hash=System.Security.Cryptography.SHA256.Create())if(BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(audio))).Replace("-","").ToLowerInvariant()!=definition.audioSha256)throw new Exception("Original tutorial audio integrity failed");count++;
                if(variant=="normal") { chart.RequireNormalTutorialDomain();int points=0;foreach(var action in chart.actions)if(action.Kind==CellKind.SingleHold)points+=HoldCheckpointSchedule.Create(action.ts,action.duration,action.Bpm).Length;if(points!=28)throw new Exception("Native normal checkpoint schedule failed");count++; }
            }
            if(RecoveredLocalization.Text("bundle_title","")!="Súper paquete" || RecoveredLocalization.Text("lucky_draw","")!="Sorteo" || RecoveredLocalization.Text("vs_start_battle","")!="¡¡Batalla en línea!!")throw new Exception("Original Spanish table lookup failed");count++;
            frontend.ShowMainRoom();frontend.Navigation.Select(MainPage.Shop);
            var title=frontend.Navigation.Pages[MainPage.Shop].Objects["sharedassets3.assets:1404"];
            var text=title.GetComponent<Text>();var shadow=title.GetComponent<RecoveredUiShadow>();
            if(text.text!="Súper paquete" || text.color!=Color.black || shadow==null || shadow.style!=3 || shadow.effectColor!=Color.white || shadow.effectDistance!=new Vector2(3,-3))throw new Exception("Shop title native style differs");count++;
            using(var helper=new VertexHelper())
            {
                var vertex=UIVertex.simpleVert;vertex.color=new Color32(100,120,140,128);vertex.position=Vector3.zero;helper.AddVert(vertex);vertex.position=Vector3.right;helper.AddVert(vertex);vertex.position=Vector3.up;helper.AddVert(vertex);helper.AddTriangle(0,1,2);
                shadow.ModifyMesh(helper);
                if(helper.currentVertCount!=27)throw new Exception("Native Outline8 geometry count failed");
                var original=UIVertex.simpleVert;helper.PopulateUIVertex(ref original,26);if(original.color.a!=128)throw new Exception("Outline replaced original glyph alpha");count++;
            }
            frontend.Navigation.Select(MainPage.Versus);var vs=frontend.Navigation.Pages[MainPage.Versus].Objects["sharedassets3.assets:1806"].GetComponent<RecoveredUiShadow>();
            if(vs.effectColor!=Color.black || vs.effectDistance!=new Vector2(5,-5))throw new Exception("VS text native outline differs");count++;
            frontend.Navigation.Select(MainPage.Home);frontend.MainRoom.Find("StoryModeButton").GetComponent<Button>().onClick.Invoke();frontend.DismissNotice();count++;
            return count;
        }
    }
}
