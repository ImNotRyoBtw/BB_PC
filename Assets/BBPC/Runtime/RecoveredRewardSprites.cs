using System;
using UnityEngine;

namespace BBPC.UnityRuntime
{
    internal static class RecoveredRewardSprites
    {
        // BFTicketHelper.GetSpriteName RVA 0x291FF8C; DiamondSpriteHelper RVA 0x2D8E938.
        internal static Sprite Ticket(int quantity,bool background)=>Load((background?"bfticketiconwithbg/":"bfticketicon/")+(quantity<2?1:2));
        internal static Sprite Diamond(int quantity)
        {
            int band=quantity<=25?25:quantity<=50?50:quantity<=75?75:quantity<=100?100:quantity<=150?150:quantity<=250?250:quantity<=1500?1500:quantity<=3000?3000:quantity<=7000?7000:quantity<=12000?12000:quantity<=30000?30000:60000;
            return Load("diamondicon/"+band);
        }
        // DecorModel.GetDecorLocationInfo RVA 0x28B49D8 orders by location, preserving duplicates.
        internal static string DecorLocationKey(RecoveredDecorLocation[] locations)
        {
            if(locations==null||locations.Length==0)return "";
            var ordered=(RecoveredDecorLocation[])locations.Clone();
            foreach(var item in ordered)if(item==null)return "";
            Array.Sort(ordered,(a,b)=>a.location.CompareTo(b.location));
            string key="";
            foreach(var item in ordered)key+=item.location==2?"T":item.location==7?"L":item.location==9?"R":"";
            return key;
        }
        internal static Sprite DecorLocation(RecoveredServerDecor decor)
        {
            string key=DecorLocationKey(decor?.locations);
            return key.Length==0?null:Resources.Load<Sprite>("RecoveredDecorSprites606/decoricon/"+key.ToLowerInvariant());
        }
        private static Sprite Load(string path)
        {
            var sprite=Resources.Load<Sprite>("RecoveredRewardSprites606/"+path);
            if(sprite==null)throw new InvalidOperationException("Missing original reward sprite: "+path);
            return sprite;
        }
    }
}
