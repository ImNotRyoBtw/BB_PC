using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using System.Collections.Generic;

namespace BBPC.UnityRuntime
{
    // Observed GET envelopes only. These DTOs do not create accounts or grant ownership/rewards.
    public sealed class RecoveredReadEnvelope<T> { public int status; public int? err_code; public T data; }
    public sealed class RecoveredProfilePayload
    {public RecoveredServerUser user;public int? change_name_price;public RecoveredAdventureProgress adventure_progress;public int play_count;public long user_ts;public RemoteSongFile user_skin;}
    public sealed class RecoveredAdventureProgress {public int chapter_id,section_id;}
    public sealed class RecoveredServerUser
    {
        public string auid,screen_name,player_id;public long account_expiration;
        public SongImage avatar;public RecoveredServerBox name_box;
        public RecoveredServerRole bf,gf,revive;public RecoveredServerRole[] battle_bfs;
        public RecoveredServerEntryEffect[] entry_effects;
        public RecoveredServerArrow arrow;public RecoveredServerClickEffect click_effect;
    }
    public sealed class RecoveredServerRole
    {public string rid,title;public int type,rarity;public int? rarity_v2,ex_level;public SongImage icon,head_icon,head_icon_dark;public RemoteSongFile animation;public Dictionary<string,RecoveredRoleDecorLocation> decor_info;}
    public sealed class RecoveredRoleDecorLocation {public long mflag;public float[] anchor;}
    public sealed class RecoveredServerEntryEffect {public string efid,title;public int type,rarity;public SongImage cover;public RemoteSongFile animation;}
    public sealed class RecoveredServerArrow {public string arr_id;public RemoteSongFile animation;}
    public sealed class RecoveredServerClickEffect {public string ceff_id;public RemoteSongFile animation;}
    public sealed class RecoveredServerBox
    {public string box_id,box_title,color,unlock_text;public SongImage fore_box;public bool unlock_status;public long unlock_ts;}
    public sealed class RecoveredWealthPayload
    {
        public int? diamond;
        public int bf_draw_ticket,challenge_prize,vip;
        public long expiration,pass_expiration,role_ts;
        // OBSERVED: the server keeps its own record of paid entitlements (no-ads, extra health).
        public Dictionary<string,int> purchases;
    }
    // Observed 6.0.6 favorite list model: CollectionFavListRespModel.items -> List<StageModel>.
    public sealed class RecoveredFavoritePayload { public SongItem[] items; }
    // Observed 6.0.6 favorite change model: CollectionChangeFavStatusRespModel.sids -> string[].
    public sealed class RecoveredFavoriteChangePayload { public string[] sids; }
    // OBSERVED (user LogF.txt 2026-10-30): GET /beatmusic/leaderboard/{region,world} answers
    // {id,title,items:[{auid,avatar,screen_name,score,rank}]}. Other players' auid is never
    // read into a DTO, so it can neither be displayed nor written to a log or receipt.
    public sealed class RecoveredLeaderboardPayload { public string id,title; public RecoveredLeaderboardItem[] items; public RecoveredLeaderboardItem me; public long expiration,update_time; public int timezone,seats; public string region_name; public RecoveredLeaderboardOpenCondition open_condition; public RecoveredLeaderboardPrize prize; public RecoveredLeaderboardSection[] section; }
    // Observed 6.0.6 ChallengeModel fields; no prize claim request is implied by this read model.
    public sealed class RecoveredLeaderboardOpenCondition { public int need_round,current_round,need_score,current_score,status; }
    public sealed class RecoveredLeaderboardPrize { public string id; public int rank; public RecoveredTaskReward[] rewards; }
    public sealed class RecoveredLeaderboardSection { public int rank,diamond; }
    // 6.0.6 Pages.MainRoom.LeaderModel (2447), GuildPlayerIntroModel (4432).
    // Keep only public presentation data; guild/player identifiers are deliberately omitted.
    public sealed class RecoveredLeaderboardGuild { public string guild_name; }
    public sealed class RecoveredLeaderboardItem { public string screen_name; public int rank; public long score; public double percent; public SongImage avatar; public RecoveredServerBox name_box; public RecoveredLeaderboardGuild guild; }
    public sealed class RecoveredLeaderboardRules { public string content; }
    // OBSERVED (user LogReport.txt 2026-10-30): POST /beatmusic/result/report envelope.
    public sealed class RecoveredReportResultData { public bool? new_record; public int? gain_diamond, point, multiple_factor; public float[] multiple_factors; public RecoveredRewardModel[] rewards; }
    // OBSERVED: POST /beatmusic/result/reward envelope.
    public sealed class RecoveredRewardResultData { public int? gain_diamond; }
    // OBSERVED (user purchase log): POST /beatmusic/shop/goods/purchase envelope.
    public sealed class RecoveredPurchaseResultData { public int? cost; public string[] rids; public RecoveredRewardModel[] rewards; }
    public sealed class RecoveredRewardModel { public int type, quantity; public RecoveredServerRole role; }
    // OBSERVED (user own shop response 6.0.6): GET /beatmusic/shop/goods envelope.
    public sealed class RecoveredShopPayload
    {
        public RecoveredRaffleZone[] lottery_zone_v2;
        public RecoveredBanner[] banners;
        public RecoveredRaffleZone[] themed_lottery_zone;
        public RecoveredRaffleZone[] vs_legacy_zone;
        public RecoveredRaffleZone[] vs_legacy_premium_zone;
        public RecoveredShopStore bf_store;
        public RecoveredDiamondSku[] diamond_zone;
    }
    public sealed class RecoveredShopStore { public RecoveredRoleGoods[] role_goods_list; public RecoveredShopDiscount discount; }
    public sealed class RecoveredShopDiscount { public int? off, available; public long expiration, refresh_ts; }
    public sealed class RecoveredRoleGoods { public RecoveredServerRole role; public int original_price; public int? discount_price; public bool owned; }
    public sealed class RecoveredRaffleZone { public string id; public int type, pop_type; public long expiration; public RemoteSongFile cover_res_pack, resource_pack; public RecoveredZoneColors config; public bool can_use_bf_ticket; public RecoveredProbability[] probabilities; public string display_probability_up; public int probability_up_style; public RecoveredPayment[] payments; public RecoveredRotateInfo rotate; public RecoveredProgressInfo[] progresses; public RecoveredPidInfo[] pid_infos; }
    public sealed class RecoveredZoneColors { public string bg_color, edge_color, title_color; }
    public sealed class RecoveredProbability { public string pid, display_probability; public int pos; public double probability; public RecoveredRewardModel[] rewards; }
    public sealed class RecoveredPayment { public int guess, ptype, quantity; public long? last_ts, interval; }
    public sealed class RecoveredRotateInfo { public string rotate_id; public long? expiration; public RecoveredLotteryReward[] all_rewards; public string[] wins; public string[] display_rewards; }
    public sealed class RecoveredLotteryReward { public string reward_id; public int category, tag; public RecoveredRewardModel reward; }
    public sealed class RecoveredProgressInfo { public int guarantee, progress, status; public SongImage icon; }
    public sealed class RecoveredBanner { public string id; public int type; public string title; public SongImage background; public long expiration, s_ts; public int enable, review_enable; public RecoveredRoleGoods[] role_goods_list; }
    public sealed class RecoveredDiamondSku { public string id, product_id; public int ptype, quantity; }
    // The report body is assembled from a finished stage result and the own profile. Every field name
    // below matches the observed LogReport.txt body; values come from recovered types only.
    public sealed class RecoveredReportInput
    {
        public string client_id, sid, aid, role, bf, gf, revive, arrow, click_effect, actual_bf, actual_gf, game_mode;
        public int progress, score, perfect, great, good, miss, blood, full_score, c_count;
        public bool has_full_combo;
        public string[] rsids;
        // OBSERVED (user LogReport.txt vs LogAll.txt 2026-10-31): extra_blood is entitlement-driven.
        // The account that owns +2/+5/+10 extra health sends all four keys; an account that owns none
        // sends only {"ab_extra_hp":0}. The map is rebuilt from Wealth.purchases, never hardcoded.
        public RecoveredWealthData wealth;
    }
    // OBSERVED (user LogShopown.txt 2026-10-31): POST /beatmusic/shop/goods/lottery body and answer.
    // {"client_id":"<32hex>","id":"<lottery id>","guess":1,"ptype":1} -> {rewards[],progresses[],need_refresh}.
    public sealed class RecoveredLotteryResultData { public RecoveredRewardModel[] rewards; public RecoveredProgressInfo[] progresses; public bool need_refresh; }
    // OBSERVED (user LogAll.txt 2026-10-31): POST /beatmusic/task/period/reward body and answer.
    // {"client_id":"<32hex>","tid":"<task id>","type":"1"|"2"} -> {rewards:[{type,quantity}]}.
    // type "1" = sign-in task (reward type 1 = diamond). BattlePass has its own native claim route.
    public sealed class RecoveredTaskRewardResultData { public RecoveredTaskReward[] rewards; }
    // Observed local task/battle response; historical task IDs never establish current eligibility.
    public sealed class RecoveredBattlePassData
    { public string bid; public int point,point_by_diamond; public long expiration; public bool has_purchased; public RecoveredBattlePassLevel[] level_tasks; public RecoveredTaskPeriodItem[] bonus; }
    public sealed class RecoveredBattlePassLevel { public int level; public RecoveredTaskPeriodItem free,vip; }
    public sealed class RecoveredTaskPeriodList { public RecoveredSignInBlock signin; public RecoveredTaskPeriodBlock daily,weekly; public long refresh_ts; }
    public sealed class RecoveredSignInBlock { public int signin_task_type; public RecoveredTaskPeriodItem[] tasks; }
    public sealed class RecoveredTaskPeriodBlock { public long expiration; public RecoveredTaskPeriodItem[] reward,point; }
    public sealed class RecoveredTaskPeriodItem { public string tid,desc; public int type,target,finish,status,need_ad; public bool need_progress; public RecoveredTaskReward[] rewards; }
    public sealed class RecoveredTaskReward { public int type,quantity; public SongImage cover; public RecoveredServerRole role; public RecoveredServerBox name_box; public RecoveredServerDecor decor; }
    // OBSERVED: GET /beatmusic/task/period/list answers {signin:{tasks:[{tid,type,target,finish,status}]}}.
    public sealed class RecoveredSignInPayload { public RecoveredSignInTask[] tasks; }
    public sealed class RecoveredSignInTask { public string tid,desc; public int type,target,finish,status,need_ad; public bool need_progress; public RecoveredTaskReward[] rewards; }
    // OBSERVED: GET /beatmusic/stage/free answers {stage:{sid,aid,cover,difficulty,...}}.
    public sealed class RecoveredFreeStagePayload { public SongMetadata stage; }
    public static class RecoveredReadContracts
    {
        public const string Identity="/common/unique_device_id",Time="/common/time/sync",Catalog="/beatmusic/stage/list";
        public const string Profile="/beatmusic/user/profile/v2",Wealth="/beatmusic/user/wealth",StageInfo="/beatmusic/stage/info";
        public const string Config="/app_config",DynamicConfig="/app_config/dynamic",Advertise="/app_config/advertise",OwnRoles="/beatmusic/role/own";
        // Routes recovered from the 6.0.6 APIConst table (end.txt / analysis/m0/dumps/6.0.6/dump.cs).
        // AppConfigManager stores one flat key->value dictionary; /common/init/config shares that shape.
        public const string InitConfig="/common/init/config";
        public const string FavoriteList="/beatmusic/favorite/list";
        public const string LeaderboardRegion="/beatmusic/leaderboard/region",LeaderboardWorld="/beatmusic/leaderboard/world",LeaderboardRules="/beatmusic/leaderboard/rules",LeaderboardChallenge="/beatmusic/leaderboard/challenge";
        public const string TaskPeriodList="/beatmusic/task/period/list",TaskBattle="/beatmusic/task/battle",SignIn="/beatmusic/task/signin";
        public const string ShopGoods="/beatmusic/shop/goods",StageFree="/beatmusic/stage/free",UserBoxList="/beatmusic/user/box/list";
        public const string RoleCollection="/beatmusic/role/collection",RoleConfig="/beatmusic/role/config";
        public const string EmoticonList="/beatmusic/emoticon/list",GamepadList="/beatmusic/gamepad/list",AdventureInfo="/beatmusic/adventure/info";
        public const string StoryList="/beatmusic/story/list",StoryInfo="/beatmusic/story/info";
        // OBSERVED (user LogReport.txt / purchase log 2026-10-30): write routes. Each is user-initiated only.
        public const string ResultReport="/beatmusic/result/report",ResultReward="/beatmusic/result/reward";
        // ShopGoodsLottery is DEMONSTRATED: user LogShopown.txt 2026-10-31 captured the body
        // {"client_id":"<32hex>","id":"<lottery id>","guess":1,"ptype":1} and its answer
        // {rewards[],progresses[],need_refresh}. The route itself was already in the 6.0.6 APIConst table.
        public const string ShopGoodsPurchase="/beatmusic/shop/goods/purchase",ShopGoodsLottery="/beatmusic/shop/goods/lottery";
        // OBSERVED (live capture RealLoguwu 2026-11): POST shop/goods/discount {"client_id"} -> {discount:{off,available,expiration,refresh_ts}};
        // POST role/action {"action":1,"state":0|1|2,"ids":[rid]} (no client_id; state 1 pinned, 2 hidden; verified against role/own.arrange_state).
        public const string ShopGoodsDiscount="/beatmusic/shop/goods/discount",RoleAction="/beatmusic/role/action";
        // POST /beatmusic/task/period/reward. Body {"client_id","tid","type"} is DEMONSTRATED by user
        // LogAll.txt 2026-10-31. The GET sibling TaskPeriodList is a read route; this is a write route.
        public const string TaskPeriodReward="/beatmusic/task/period/reward";
        // The only observed write used here: the player's own favorite list. Purchases, rewards and
        // battle endpoints stay out of this allowlist.
        public const string FavoriteAdd="/beatmusic/favorite/add",FavoriteDelete="/beatmusic/favorite/delete";
        private static readonly HashSet<string> ReadRoutes=new HashSet<string>
        {
            Identity,Time,Catalog,Profile,Wealth,StageInfo,Config,DynamicConfig,Advertise,OwnRoles,InitConfig,
            FavoriteList,LeaderboardRegion,LeaderboardWorld,LeaderboardRules,LeaderboardChallenge,TaskPeriodList,TaskBattle,SignIn,
            ShopGoods,StageFree,UserBoxList,RoleCollection,RoleConfig,EmoticonList,GamepadList,AdventureInfo,StoryList,StoryInfo
        };
        public static bool IsAllowed(string route)=>route!=null&&ReadRoutes.Contains(route);
        // Player-initiated writes only. Every route here has a captured body; none is ever called
        // automatically and no reward/ownership is granted locally.
        public static bool IsAllowedWrite(string route)=>route==FavoriteAdd||route==FavoriteDelete||route==ResultReport||route==ResultReward||route==ShopGoodsPurchase||route==ShopGoodsLottery||route==ShopGoodsDiscount||route==RoleAction||route==TaskPeriodReward||route==BattlePassReward;
        // RankPanel.IntroduceButtonOnClick passes the selected leaderboard ID to rules.
        public static bool AllowsParameter(string route,string key)=>(route==StageInfo&&(key=="sid"||key=="aid"||key=="game_mode"))||(route==LeaderboardRules&&key=="id");

        // OBSERVED (user LogReport.txt 2026-10-30, two captures): POST /beatmusic/result/report body.
        // Field order, names and the decors/extra_blood constants below are byte-for-byte the captured body.
        // The report client_id is a dashed UUID generated once per song completion; result/reward reuses it.
        public static string ReportBody(RecoveredReportInput input)
        {
            if(input==null)throw new ArgumentNullException("input");
            if(string.IsNullOrEmpty(input.client_id)||input.client_id.Length!=36)throw new FormatException("Report client_id must be a dashed UUID");
            RequireToken(input.sid);RequireToken(input.aid);
            var sb=new StringBuilder(640);
            sb.Append('{');
            AppendString(sb,"client_id",input.client_id);
            AppendString(sb,"sid",input.sid);
            AppendString(sb,"aid",input.aid);
            AppendString(sb,"role",input.role);
            AppendInt(sb,"progress",input.progress);
            AppendInt(sb,"score",input.score);
            AppendInt(sb,"perfect",input.perfect);
            AppendInt(sb,"great",input.great);
            AppendInt(sb,"good",input.good);
            AppendInt(sb,"miss",input.miss);
            AppendBool(sb,"has_full_combo",input.has_full_combo);
            AppendInt(sb,"blood",input.blood);
            AppendInt(sb,"full_score",input.full_score);
            AppendInt(sb,"c_count",input.c_count);
            AppendString(sb,"game_mode",input.game_mode);
            AppendString(sb,"bf",input.bf);
            AppendString(sb,"arrow",input.arrow);
            AppendString(sb,"click_effect",input.click_effect);
            AppendString(sb,"gf",input.gf);
            AppendString(sb,"actual_bf",input.actual_bf);
            AppendString(sb,"actual_gf",input.actual_gf);
            AppendString(sb,"revive",input.revive);
            sb.Append(",\"decors\":{}");   // OBSERVED constant in both captures.
            AppendStrings(sb,"rsids",input.rsids);
            sb.Append(',').Append(ExtraBloodBody(input.wealth));
            sb.Append('}');
            return sb.ToString();
        }
        // OBSERVED (user LogReport.txt owns +2/+5/+10; LogAll.txt owns none): extra_blood is rebuilt
        // from the paid entitlements the server recorded in Wealth.purchases. ab_extra_hp is always 0
        // (the A/B experiment flag); each permanent extra-health tier is emitted only when owned.
        public static string ExtraBloodBody(RecoveredWealthData wealth)
        {
            int owned2 = 0, owned5 = 0, owned10 = 0;
            if (wealth != null && wealth.purchases != null)
                foreach (var pair in wealth.purchases)
                    if (pair.Value > 0)
                    {
                        int tier = ExtraHealthTier(pair.Key);
                        if (tier == 2) owned2 = 2; else if (tier == 5) owned5 = 5; else if (tier == 10) owned10 = 10;
                    }
            var sb = new StringBuilder("\"extra_blood\":{\"ab_extra_hp\":0");
            if (owned2 > 0) sb.Append(",\"extra_hp2\":2");
            if (owned5 > 0) sb.Append(",\"extra_hp5\":5");
            if (owned10 > 0) sb.Append(",\"extra_hp10\":10");
            sb.Append('}');
            return sb.ToString();
        }
        private static int ExtraHealthTier(string key)
        {
            if (string.IsNullOrEmpty(key)) return 0;
            if (key.Contains("_10_extra_health_")) return 10;
            if (key.Contains("_5_extra_health_")) return 5;
            if (key.Contains("_2_extra_health_")) return 2;
            return 0;
        }
        // OBSERVED (user LogReport.txt 2026-10-30): POST /beatmusic/result/reward reuses the report client_id.
        public static string RewardBody(string clientId,string sid,string aid,int factorIndex)
        {
            if(string.IsNullOrEmpty(clientId)||clientId.Length!=36)throw new FormatException("Reward client_id must be the report client_id");
            RequireToken(sid);RequireToken(aid);
            var sb=new StringBuilder(160);
            sb.Append('{');
            AppendString(sb,"client_id",clientId);
            AppendString(sb,"sid",sid);
            AppendString(sb,"aid",aid);
            AppendInt(sb,"factor_index",factorIndex);
            sb.Append('}');
            return sb.ToString();
        }
        // OBSERVED (user LogShopown.txt 2026-10-31): POST /beatmusic/shop/goods/lottery body.
// {"client_id":"<32hex>","id":"<lottery id>","guess":1,"ptype":1}. client_id is a GuidN.
public sealed class RecoveredDiscountResult { public RecoveredShopDiscount discount; }
        public static string DiscountBody(string clientId)
        {
            if(string.IsNullOrEmpty(clientId)||clientId.Length!=32)throw new FormatException("Discount client_id must be a GuidN");
            return "{\"client_id\":\""+clientId+"\"}";
        }
        public static RecoveredShopDiscount ReadDiscountResult(string body)=>Data<RecoveredDiscountResult>(body).discount;
        public static string ArrangeBody(int state,string rid)
        {
            if(state<0||state>2)throw new FormatException("Arrange state must be 0, 1 or 2");
            RequireToken(rid);
            return "{\"action\":1,\"state\":"+state.ToString(System.Globalization.CultureInfo.InvariantCulture)+",\"ids\":[\""+rid+"\"]}";
        }
        public static string LotteryBody(string clientId,string id,int guess,int ptype)
        {
            if(string.IsNullOrEmpty(clientId)||clientId.Length!=32)throw new FormatException("Lottery client_id must be a GuidN");
            if(string.IsNullOrEmpty(id))throw new FormatException("Lottery id required");
            var sb=new StringBuilder(128);
            sb.Append('{');
            AppendString(sb,"client_id",clientId);
            AppendString(sb,"id",id);
            AppendInt(sb,"guess",guess);
            AppendInt(sb,"ptype",ptype);
            sb.Append('}');
            return sb.ToString();
        }
        public static RecoveredLotteryResultData ReadLotteryResult(string body)=>Data<RecoveredLotteryResultData>(body);
        // OBSERVED (user LogAll.txt 2026-10-31): POST /beatmusic/task/period/reward body.
        // {"client_id":"<32hex>","tid":"<task id>","type":"1"|"2"}. type is a string, not an int.
        public static string TaskRewardBody(string clientId,string tid,string type)
        {
            if(string.IsNullOrEmpty(clientId)||clientId.Length!=32)throw new FormatException("Task reward client_id must be a GuidN");
            if(string.IsNullOrEmpty(tid))throw new FormatException("Task tid required");
            if(string.IsNullOrEmpty(type))throw new FormatException("Task type required");
            var sb=new StringBuilder(128);
            sb.Append('{');
            AppendString(sb,"client_id",clientId);
            AppendString(sb,"tid",tid);
            AppendString(sb,"type",type);
            sb.Append('}');
            return sb.ToString();
        }
        // Native BattlePassManager async 0x276F87C; GOT relocations resolved against ScriptString.
        public const string BattlePassReward="/beatmusic/task/battle/reward";
        public static string BattlePassRewardBody(string clientId,string bid,string tid,int receiveType)
        {
            if(!Guid.TryParseExact(clientId,"N",out _))throw new FormatException("BattlePass client_id must be GuidN");
            if(string.IsNullOrWhiteSpace(bid)||string.IsNullOrWhiteSpace(tid))throw new FormatException("BattlePass bid/tid required");
            if(receiveType!=0&&receiveType!=1)throw new ArgumentOutOfRangeException(nameof(receiveType));
            var sb=new StringBuilder(160);sb.Append('{');
            AppendString(sb,"client_id",clientId);AppendString(sb,"bid",bid);AppendString(sb,"tid",tid);AppendInt(sb,"type",receiveType);
            sb.Append('}');return sb.ToString();
        }
        // Native GetRankRewarded MoveNext (RVA 0x28D0A1C): POST JSON client_id/id.
        // Serialization only: no write-route authorization or live competition claim.
        public static string LeaderboardPrizeBody(string clientId,string id)
        {
            if(!Guid.TryParseExact(clientId,"N",out _))throw new FormatException("Leaderboard client_id must be GuidN");
            if(string.IsNullOrWhiteSpace(id))throw new FormatException("Leaderboard prize id required");
            var sb=new StringBuilder(100);sb.Append('{');
            AppendString(sb,"client_id",clientId);AppendString(sb,"id",id);sb.Append('}');return sb.ToString();
        }
        public static RecoveredTaskRewardResultData ReadTaskRewardResult(string body)
        {
            var value=Data<RecoveredTaskRewardResultData>(body);
            if(value.rewards==null)throw new FormatException("Task reward response missing rewards");
            foreach(var reward in value.rewards)
                if(reward==null||reward.type<=0||reward.quantity<0)throw new FormatException("Invalid task reward");
            return value;
        }
        public static RecoveredTaskRewardResultData ReadBattlePassRewardResult(string body)
        {
            var reward=Data<RecoveredTaskReward>(body);
            if(reward.type<=0||reward.quantity<0)throw new FormatException("Invalid battle pass reward");
            return new RecoveredTaskRewardResultData{rewards=new[]{reward}};
        }
        public static RecoveredBattlePassData ReadBattlePass(string body)
        {
            var pass=Data<RecoveredBattlePassData>(body);
            if(string.IsNullOrWhiteSpace(pass.bid)||pass.level_tasks==null||pass.point<0||pass.point_by_diamond<0)
                throw new FormatException("Battle pass fields missing or invalid");
            return pass;
        }
        // OBSERVED (user LogReport.txt 2026-10-30): POST /beatmusic/shop/goods/purchase body.
        // {"client_id":"<32hex>","type":2,"id":"<rid>","rids":["<rid>"],"diamond":7800}.
        // client_id is a GuidN here, unlike the dashed UUID of result/report and result/reward.
        public static string PurchaseBody(string clientId,int type,string id,string[] rids,int diamond)
        {
            if(string.IsNullOrEmpty(clientId)||clientId.Length!=32)throw new FormatException("Purchase client_id must be a GuidN");
            var sb=new StringBuilder(160);
            sb.Append('{');
            AppendString(sb,"client_id",clientId);
            AppendInt(sb,"type",type);
            AppendString(sb,"id",id);
            AppendStrings(sb,"rids",rids);
            AppendInt(sb,"diamond",diamond);
            sb.Append('}');
            return sb.ToString();
        }
        // OBSERVED write/read envelopes. result/report and result/reward are the only confirmed write routes.
        public static RecoveredReportResultData ReadReportResult(string body)
        {
            var value=Data<RecoveredReportResultData>(body);
            if(!value.gain_diamond.HasValue||value.gain_diamond.Value<0)throw new FormatException("Report missing gain_diamond");
            if(value.multiple_factors!=null)
            {
                if(value.multiple_factors.Length!=7)throw new FormatException("Report requires seven factors");
                foreach(var factor in value.multiple_factors)if(float.IsNaN(factor)||float.IsInfinity(factor)||factor<=0)throw new FormatException("Invalid reward factor");
            }
            return value;
        }
        public static RecoveredRewardResultData ReadRewardResult(string body)
        {
            var value=Data<RecoveredRewardResultData>(body);
            if(!value.gain_diamond.HasValue||value.gain_diamond.Value<0)throw new FormatException("Reward missing gain_diamond");
            return value;
        }
        public static RecoveredPurchaseResultData ReadPurchaseResult(string body)=>Data<RecoveredPurchaseResultData>(body);
        public static RecoveredShopPayload ReadShopPayload(string body){var payload=Data<RecoveredShopPayload>(body);RecoveredPidWins.Attach(payload,body);return payload;}
        // OBSERVED client_id formats differ per route: dashed UUID for result/report and result/reward,
        // GuidN for favorite/add, role/config and shop/goods/purchase.
        public static string NewReportClientId()=>Guid.NewGuid().ToString();
        public static string NewClientId()=>Guid.NewGuid().ToString("N");
        private static void RequireToken(string value)
        {
            if(string.IsNullOrEmpty(value))throw new FormatException("Missing token");
            foreach(char c in value)if(!char.IsLetterOrDigit(c)&&c!='_'&&c!='-')throw new FormatException("Unexpected token encoding");
        }
        private static void AppendString(StringBuilder sb,string key,string value)
        {
            if(sb.Length>0&&sb[sb.Length-1]!='{')sb.Append(',');
            sb.Append('"').Append(Escape(key)).Append("\":\"").Append(Escape(value??string.Empty)).Append('"');
        }
        private static void AppendInt(StringBuilder sb,string key,int value)
        {
            if(sb.Length>0&&sb[sb.Length-1]!='{')sb.Append(',');
            sb.Append('"').Append(Escape(key)).Append("\":").Append(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        private static void AppendBool(StringBuilder sb,string key,bool value)
        {
            if(sb.Length>0&&sb[sb.Length-1]!='{')sb.Append(',');
            sb.Append('"').Append(Escape(key)).Append("\":").Append(value?"true":"false");
        }
        private static void AppendStrings(StringBuilder sb,string key,IEnumerable<string> values)
        {
            if(sb.Length>0&&sb[sb.Length-1]!='{')sb.Append(',');
            sb.Append('"').Append(Escape(key)).Append("\":[");
            bool first=true;
            if(values!=null)foreach(var v in values){if(!first)sb.Append(',');first=false;sb.Append('"').Append(Escape(v)).Append('"');}
            sb.Append(']');
        }
        private static string Escape(string value)
        {
            if(string.IsNullOrEmpty(value))return string.Empty;
            var sb=new StringBuilder(value.Length+8);
            foreach(char c in value)
            {
                switch(c)
                {
                    case '"':sb.Append("\\\"");break;
                    case '\\':sb.Append("\\\\");break;
                    case '\n':sb.Append("\n");break;
                    case '\r':sb.Append("\r");break;
                    case '\t':sb.Append("\t");break;
                    case '\b':sb.Append("\b");break;
                    case '\f':sb.Append("\f");break;
                    default:
                        if(c<0x20)sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
        public static T Data<T>(string body) where T:class
        {
            var envelope=SongCatalog.Read<RecoveredReadEnvelope<T>>(body);
            if(envelope==null||envelope.status!=1||envelope.err_code.GetValueOrDefault()!=0||envelope.data==null)throw new FormatException("Read envelope rejected");
            return envelope.data;
        }
        public static RecoveredProfilePayload ReadProfile(string body,string ownAuid)
        {
            var payload=Data<RecoveredProfilePayload>(body);var user=payload.user;
            if(string.IsNullOrEmpty(ownAuid)||user==null||user.auid!=ownAuid||(payload.change_name_price.HasValue&&payload.change_name_price.Value<0))
                throw new FormatException("Profile does not match the current installation");
            foreach(var role in new[]{user.bf,user.gf,user.revive})if(role!=null&&string.IsNullOrEmpty(role.rid))throw new FormatException("Incomplete role descriptor");
            return payload;
        }
        public static RecoveredWealthData ReadWealth(string body)
        {
            var value=Data<RecoveredWealthPayload>(body);
            if(!value.diamond.HasValue||value.diamond.Value<0||value.bf_draw_ticket<0||value.challenge_prize<0)
                throw new FormatException("Incomplete/invalid wealth");
            return new RecoveredWealthData{diamond=value.diamond.Value,bf_draw_ticket=value.bf_draw_ticket,challenge_prize=value.challenge_prize,vip=value.vip,expiration=value.expiration,pass_expiration=value.pass_expiration,purchases=value.purchases};
        }
        private static T TryRead<T>(string body) where T:class
        {
            try{return SongCatalog.Read<T>(body);}catch(FormatException){return null;}
        }
        // Observed: /beatmusic/favorite/list answers CollectionFavListRespModel{items:[StageModel]}.
        public static string[] ReadFavoriteList(string body)
        {
            var envelope=TryRead<RecoveredReadEnvelope<RecoveredFavoritePayload>>(body);
            var items=envelope!=null&&envelope.data!=null&&envelope.data.items!=null
                ?envelope.data.items
                :TryRead<RecoveredFavoritePayload>(body)?.items;
            if(items==null)throw new FormatException("Favorite list has no items");
            var sids=new List<string>();
            foreach(var item in items)
            {
                if(item?.stage==null||string.IsNullOrEmpty(item.stage.sid))throw new FormatException("Favorite item without SID");
                sids.Add(item.stage.sid);
            }
            return sids.ToArray();
        }
        // Observed: /beatmusic/favorite/add and /delete answer CollectionChangeFavStatusRespModel{sids}.
        public static string[] ReadFavoriteChange(string body)
        {
            var envelope=TryRead<RecoveredReadEnvelope<RecoveredFavoriteChangePayload>>(body);
            var sids=envelope!=null&&envelope.data!=null&&envelope.data.sids!=null
                ?envelope.data.sids
                :TryRead<RecoveredFavoriteChangePayload>(body)?.sids;
            if(sids==null)throw new FormatException("Favorite change has no sids");
            foreach(var sid in sids)if(string.IsNullOrEmpty(sid))throw new FormatException("Favorite change contains an empty SID");
            return sids;
        }
        // Observed leaderboard list. Only screen_name/rank/score are kept; other players' auid is dropped.
        public static RecoveredLeaderboardPayload ReadLeaderboard(string body)
        {
            var envelope=TryRead<RecoveredReadEnvelope<RecoveredLeaderboardPayload>>(body);
            var payload=envelope!=null&&envelope.data!=null?envelope.data:TryRead<RecoveredLeaderboardPayload>(body);
            if(payload==null||payload.items==null)throw new FormatException("Leaderboard has no items");
            return payload;
        }
        public static string ReadLeaderboardRules(string body)
        {
            var envelope=TryRead<RecoveredReadEnvelope<RecoveredLeaderboardRules>>(body);
            var payload=envelope!=null&&envelope.data!=null?envelope.data:TryRead<RecoveredLeaderboardRules>(body);
            if(payload==null||string.IsNullOrEmpty(payload.content))throw new FormatException("Leaderboard rules missing");
            return payload.content;
        }
        // GET /beatmusic/task/signin, observed in the supplied local response. Read-only until
        // /task/signin/reward request and response contracts are verified.
        public static RecoveredSignInTask[] ReadLegacySignInTasks(string body)
        {
            var payload=Data<RecoveredSignInPayload>(body);
            if(payload.tasks==null)throw new FormatException("Sign-in tasks missing");
            return payload.tasks;
        }
        public static RecoveredTaskPeriodList ReadTaskPeriodList(string body)
        {
            var period=Data<RecoveredTaskPeriodList>(body);
            if(period.signin==null||period.signin.tasks==null||period.daily==null||period.weekly==null)
                throw new FormatException("Periodic task sections missing");
            return period;
        }
        public static RecoveredTaskPeriodItem[] ReadSignInTasks(string body)
        {
            var period=ReadTaskPeriodList(body);
            return period.signin.tasks;
        }
        public static SongMetadata ReadStageFree(string body)
        {
            var envelope=TryRead<RecoveredReadEnvelope<RecoveredFreeStagePayload>>(body);
            var payload=envelope!=null&&envelope.data!=null?envelope.data:TryRead<RecoveredFreeStagePayload>(body);
            if(payload?.stage==null||string.IsNullOrEmpty(payload.stage.sid))throw new FormatException("Free stage missing");
            return payload.stage;
        }
        // OBSERVED (user LogF.txt 2026-10-30): POST /beatmusic/favorite/{add,delete} sends
        // {"client_id":"<32hex>","sids":["<sid>"]}; the server answers {"status":1,"data":{"sids":[...]}}.
        // client_id is a per-operation GuidN, as proven by RecoveredBfOperation.ClientId.
        public static string FavoriteBody(string sid)
        {
            if(string.IsNullOrEmpty(sid)||sid.Length>128)throw new ArgumentException("Favorite SID required");
            foreach(char c in sid)if(!char.IsLetterOrDigit(c)&&c!='_'&&c!='-')throw new ArgumentException("Unexpected SID encoding");
            return "{\"client_id\":\""+Guid.NewGuid().ToString("N")+"\",\"sids\":[\""+sid+"\"]}";
        }
        public static SongItem ReadStageInfo(string body,string sid,string aid)
        {
            if(string.IsNullOrWhiteSpace(sid))throw new ArgumentException("Selected SID required");
            var item=Data<SongItem>(body);
            if(item.stage==null||item.stage.sid!=sid||(item.stage.aid??"")!=(aid??""))throw new FormatException("stage/info does not match the selected SID/AID");
            foreach(var location in new[]{item.stage.beatmap,item.stage.beatmap_data,item.stage.game_config?.asset})
                if(location!=null&&!ValidRemoteFile(location))throw new FormatException("Invalid remote content descriptor");
            return item;
        }
        public static bool ValidRemoteFile(RemoteSongFile file)
        {
            // The original descriptors include HTTP CDN locations. Preserve their URL and require
            // the exact host and transport MD5; this is not authentication or a substitute for TLS.
            Uri uri;if(file==null||!Uri.TryCreate(file.url,UriKind.Absolute,out uri)||(uri.Scheme!="https"&&uri.Scheme!="http")||!string.IsNullOrEmpty(uri.UserInfo)||!uri.IsDefaultPort||uri.Host!="cdn.makeawesomeapps.com"||file.md5==null||file.md5.Length!=32)return false;
            foreach(char c in file.md5)if(!Uri.IsHexDigit(c))return false;return true;
        }
    }

    // Cache policy belongs to the Windows adapter: no invented native TTL, no stale success on network failure.
    // The ETag is used only with an intact body validated for this route and the current own identity.
    public sealed class RecoveredReadCache
    {
        public sealed class Tag { public string etag,bodySha256,ownerSha256; }
        public sealed class Entry { public string Body,Etag; }
        private readonly string directory;
        public RecoveredReadCache(string directory){this.directory=directory??throw new ArgumentNullException(nameof(directory));}
        private static string Hash(string text){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant();}
        private string Stem(string route)=>route==RecoveredReadContracts.Catalog?"nnwc_stages":Hash(route);
        private static void Replace(string path,string text)
        {File.WriteAllText(path+".tmp",text,new UTF8Encoding(false));if(File.Exists(path))File.Replace(path+".tmp",path,null);else File.Move(path+".tmp",path);}
        public Entry Load(string route,string ownIdentity,Action<string> validate)
        {
            if(string.IsNullOrEmpty(ownIdentity))return null;
            var path=Path.Combine(directory,Stem(route));
            if(!File.Exists(path+".json")||!File.Exists(path+"-tag.json"))return null;
            try
            {
                if(new FileInfo(path+".json").Length>16*1024*1024||new FileInfo(path+"-tag.json").Length>16384)return null;
                var body=File.ReadAllText(path+".json");var tag=SongCatalog.Read<Tag>(File.ReadAllText(path+"-tag.json"));
                if(tag==null||tag.ownerSha256!=Hash(ownIdentity)||tag.bodySha256!=Hash(body))return null;
                validate(body);return new Entry{Body=body,Etag=tag.etag};
            }
            catch(FormatException){return null;}catch(ArgumentException){return null;}
            // IO/permission failures are reported by the caller; they are not parser errors.
        }
        public void Store(string route,string ownIdentity,string body,string etag,Action<string> validate)
        {
            if(string.IsNullOrEmpty(ownIdentity))throw new ArgumentException("Own identity required for cache");
            validate(body);Directory.CreateDirectory(directory);var path=Path.Combine(directory,Stem(route));
            Replace(path+".json",body);
            var tag=new Tag{etag=etag,bodySha256=Hash(body),ownerSha256=Hash(ownIdentity)};
            using(var stream=new MemoryStream())
            {new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(Tag)).WriteObject(stream,tag);Replace(path+"-tag.json",Encoding.UTF8.GetString(stream.ToArray()));}
        }
    }
}
