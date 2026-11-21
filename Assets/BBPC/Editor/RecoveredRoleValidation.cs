using System;
using System.IO;
using System.Linq;
using BBPC.UnityRuntime;
using BBPC.Gameplay;
using UnityEngine;
namespace BBPC.EditorTools
{
    public static class RecoveredRoleValidation
    {
        [Serializable] private sealed class Receipt {public bool success,clipsSampled,rolesDispatched,scenarioRendered,noAnimationAbsent,holdAnimated;public int checks,prefabs,clips,sprites,captures,cameraEvents,ambientEvents,score,perfect,holdSkipped,holdBfSprites,holdBossSprites;public string revision,bundleMd5,error;}
        public static void Run(RecoveredFrontend frontend,string output)
        {
            Directory.CreateDirectory(output);var r=new Receipt{revision=RecoveredFrontend.Revision,bundleMd5=RecoveredRoleAssets.Source.stage.bundleMd5};
            Action<bool,string> check=(ok,message)=>{r.checks++;if(!ok)throw new InvalidOperationException(message);};
            try
            {
                var data=RecoveredRoleAssets.Source;var world=new GameObject("Original clip verification");world.transform.SetParent(frontend.transform,false);
                // Every static pack, not just Elastic: a valid first pack hid the reset Star pivots.
                var manifests=Directory.GetFiles(Path.Combine(Application.dataPath,"BBPC/Resources/RoleStages606"),"source.json",SearchOption.AllDirectories);
                foreach(var asset in manifests.Where(p=>!p.Replace('\\','/').Contains("/Generated/")).SelectMany(p=>RecoveredRoleAssets.ReadSource(File.ReadAllText(p)).assets))
                {
                    var sprite=Resources.Load<Sprite>(asset.resource);check(sprite!=null,"Original sprite missing: "+asset.id);
                    check(Mathf.Abs(sprite.pixelsPerUnit-asset.ppu)<.0001f,"Original PPU changed: "+asset.id);
                    check(Vector2.Distance(sprite.pivot,new Vector2(asset.pivot[0]*sprite.rect.width,asset.pivot[1]*sprite.rect.height))<.01f,"Original pivot changed: "+asset.id+" actual="+sprite.pivot+" expected="+asset.pivot[0]+","+asset.pivot[1]);
                }
                try
                {
                    foreach(var prefab in data.prefabs)
                    {
                        var go=RecoveredRoleAssets.Instantiate(data.stage.bundleMd5,prefab.name,world.transform);r.prefabs++;check(go!=null,"Prefab missing "+prefab.name);
                        foreach(var animator in go.GetComponentsInChildren<Animator>())
                        {
                            // Controller identity is recovered from this prefab node, avoiding ambiguous shared animation names.
                            var indices=new System.Collections.Generic.Stack<int>();
                            for(var t=animator.transform;t!=go.transform;t=t.parent)indices.Push(t.GetSiblingIndex());
                            var node=prefab.root;foreach(int index in indices)node=node.children[index];
                            string id=node.controller;var controller=Array.Find(data.controllers,c=>c.id==id);check(controller!=null,"Original controller mapping absent: "+prefab.name+"/"+node.name);
                            animator.Rebind();animator.Update(0);
                            foreach(var state in controller.states)
                            {
                                var clip=Array.Find(data.clips,c=>c.id==state.clip);r.clips++;
                                foreach(var k in clip.keys)
                                {
                                    float t=Mathf.Min(k.time+.00001f,Mathf.Max(0,clip.duration-.00001f));animator.Play(Animator.StringToHash("Base Layer."+state.name),0,t/clip.duration);animator.Update(0);
                                    var expected=Resources.Load<Sprite>(Array.Find(data.assets,a=>a.id==k.sprite).resource);check(animator.GetComponent<SpriteRenderer>().sprite==expected,"Original sprite curve differs: "+prefab.name+"/"+state.name+" t="+k.time);
                                }
                            }
                        }
                        go.SetActive(false);UnityEngine.Object.DestroyImmediate(go);
                    }
                }
                finally{UnityEngine.Object.DestroyImmediate(world);}
                r.sprites=data.assets.Length;r.clipsSampled=true;
                frontend.ShowMainRoom();frontend.MainRoom.Root.SetActive(false);frontend.PlayRoom.View.Root.SetActive(true);
                var roles=frontend.PlayRoom.Roles;var definition=RecoveredTutorialContent.Definition("elastic");var chart=RecoveredTutorialContent.ReadChart(definition);
                roles.Open(definition.sid,definition.aid,chart.bpm);check(roles.Available&&roles.BfIndex==0&&roles.BossIndex==0&&roles.BackgroundIndex==0,"Initial index 0 not instantiated");
                check(RecoveredRoleAssets.Instantiate(data.stage.bundleMd5,"NoAnimation",frontend.transform)==null,"NoAnimation produced a fabricated GF");r.noAnimationAbsent=true;
                var session=new RecoveredStageSession(chart,definition.declaredDurationMs,definition.playType,RecoveredHealth.Stage(definition.hp));session.Ambient=roles.Apply;session.Judged=(n,q)=>roles.Judged(n,q,session.TimeMs);
                var input=new PersistentLaneInput();var replay=new RecoveredStageReplay(session,input,(c,t)=>{session.Input(c,t);roles.InputSampled(c);});int next=0;int[] samples={0,30000,75000,130000,175000};
                var lastHold=session.Notes.Last(n=>n.Hold);var bfSprites=new System.Collections.Generic.HashSet<string>();var bossSprites=new System.Collections.Generic.HashSet<string>();
                var frames=new System.Collections.Generic.List<string>{"musicalMs,bfState,bfSprite,bossState,bossSprite,bfRequests,bossRequests,skipped"};
                for(int time=0;time<=185520;time+=10)
                {
                    replay.AdvanceTo(time);session.ComponentsUpdate(time);session.LateUpdate(time);roles.Sample(time,false);
                    if(time>=lastHold.Action.ts+500&&time<lastHold.Action.ts+lastHold.Action.duration-200)
                    {
                        bfSprites.Add(roles.BfSprite);bossSprites.Add(roles.BossSprite);
                        frames.Add(time+","+roles.BfState+","+roles.BfSprite+","+roles.BossState+","+roles.BossSprite+","+roles.BfRequests+","+roles.BossRequests+","+roles.HoldRefreshSkipped);
                        if(time>=179000&&time<=179200&&time%40==0){roles.Sample(time);frontend.CaptureCurrentView(Path.Combine(output,"last-hold-"+time+".png"),900,1600);r.captures++;}
                    }
                    if(next<samples.Length&&time==samples[next])
                    {
                        roles.Sample(time);frontend.CaptureCurrentView(Path.Combine(output,"elastic-t"+(time/1000)+"-9x16.png"),900,1600);frontend.CaptureCurrentView(Path.Combine(output,"elastic-t"+(time/1000)+"-16x9.png"),1600,900);r.captures+=2;
                        File.WriteAllText(Path.Combine(output,"elastic-t"+(time/1000)+".txt"),"musicalMs="+time+" bf="+roles.BfName+"/"+roles.BfState+" boss="+roles.BossName+"/"+roles.BossState+" bgIndex="+roles.BackgroundIndex+" camera="+roles.SceneCamera.transform.localPosition+" bundle="+roles.BundleMd5);next++;
                    }
                }
                check(session.Finished&&!session.Dead&&session.Summary().score==4734,"Role integration changed authentic chart judgement");check(roles.CameraChanges==17&&session.AmbientCursor==308,"Ambient/camera cursor differs");
                File.WriteAllLines(Path.Combine(output,"last-hold-frames.csv"),frames);
                r.holdSkipped=roles.HoldRefreshSkipped;r.holdBfSprites=bfSprites.Count;r.holdBossSprites=bossSprites.Count;
                check(lastHold.Action.ts==177667&&lastHold.Action.duration==5250,"Authentic last hold changed");
                check(roles.HoldRefreshSkipped>0,"Hold checkpoints still restart every pose");
                int role=lastHold.RoleAnimation;
                if(role==0||role==2||role==6||role==7)check(bfSprites.Count>1,"BF froze during authentic last hold");
                if(role==0||role==1||role==5||role==7)check(bossSprites.Count>1,"Boss froze during authentic last hold");
                check(roles.BfState=="idle"&&roles.BossState=="idle","Roles did not return to idle after final hold");r.holdAnimated=true;
                r.cameraEvents=roles.CameraChanges;r.ambientEvents=session.AmbientCursor;r.score=session.Summary().score;r.perfect=session.Summary().perfect;r.rolesDispatched=r.scenarioRendered=true;r.success=true;
                frontend.ShowMainRoom();
            }
            catch(Exception e){r.error=e.ToString();throw;}
            finally{File.WriteAllText(Path.Combine(output,"unity-role-results.json"),JsonUtility.ToJson(r,true));}
        }
    }
}
