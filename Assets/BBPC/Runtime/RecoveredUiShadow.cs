using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace BBPC.UnityRuntime
{
    // Equivalent geometry for the recovered Coffee UIShadow Outline/Outline8 styles.
    // 6.0.6 ApplyShadow 0x5171888 and ZeroAlloc 0x5171B44; no DummyDll executes.
    public sealed class RecoveredUiShadow : BaseMeshEffect
    {
        public int style;
        public Color effectColor;
        public Vector2 effectDistance;
        public bool useGraphicAlpha;
        public override void ModifyMesh(VertexHelper helper)
        {
            if(!IsActive() || helper.currentVertCount==0 || style==0 || effectColor.a<=0) return;
            if(style!=2 && style!=3) throw new NotSupportedException("Unrecovered UIShadow style: "+style);
            var source=new List<UIVertex>(); helper.GetUIVertexStream(source);
            float x=effectDistance.x,y=effectDistance.y;
            var offsets=style==2?new[]{new Vector2(x,y),new Vector2(x,-y),new Vector2(-x,y),new Vector2(-x,-y)}:
                new[]{new Vector2(x,y),new Vector2(x,-y),new Vector2(-x,y),new Vector2(-x,-y),new Vector2(-x,0),new Vector2(0,-y),new Vector2(x,0),new Vector2(0,y)};
            var result=new List<UIVertex>(source.Count*(offsets.Length+1));
            // Each native ZeroAlloc call prepends the new shadow; keep the original glyphs last.
            for(int i=offsets.Length-1;i>=0;i--) foreach(var original in source)
            {
                var vertex=original; var color=effectColor;
                if(useGraphicAlpha) color.a*=original.color.a/255f;
                vertex.color=color; vertex.position+=new Vector3(offsets[i].x,offsets[i].y,0); result.Add(vertex);
            }
            result.AddRange(source); helper.Clear(); helper.AddUIVertexTriangleStream(result);
        }
    }
}
