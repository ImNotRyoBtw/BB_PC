using System;
using System.Collections.Generic;
using System.Linq;
namespace BBPC.UnityRuntime
{
    // Public snapshots carry no account progress/favorites. Preserve server order and SID+AID.
    public sealed class RecoveredCatalogIndex
    {
        public readonly SongCatalog Catalog;
        public readonly SongItem[] Entries;
        public readonly SongItem[] MainEntries;
        public RecoveredCatalogIndex(SongCatalog catalog){SongCatalog.Validate(catalog);Catalog=catalog;MainEntries=catalog.items??new SongItem[0];Entries=SongCatalog.Enumerate(MainEntries).ToArray();}
        public SongItem[] Query(string text="",int genre=0,int difficulty=0,ISet<string> favorites=null,bool onlyNew=false)
        {
            string query=(text??"").Trim();
            return MainEntries.Where(i=>
                i!=null&&i.stage!=null&&
                (genre==0||i.stage.genre==genre||(i.stage.genres!=null&&Array.IndexOf(i.stage.genres,genre)>=0))&&
                (difficulty==0||i.stage.CurrentDifficulty==difficulty)&&
                (!onlyNew||i.stage.sub_mark!=null&&i.stage.sub_mark.tag==1)&&
                (favorites==null||favorites.Contains(i.stage.sid)||favorites.Contains(i.stage.Key)||(i.more!=null&&i.more.Any(v=>v!=null&&v.stage!=null&&(favorites.Contains(v.stage.sid)||favorites.Contains(v.stage.Key)))))&&
                (query.Length==0||Contains(i.stage.title,query)||Contains(i.stage.artist,query)||Contains(i.stage.version_title,query)||Contains(i.stage.sid,query)||(i.stage.keywords!=null&&i.stage.keywords.Any(k=>Contains(k,query))))).ToArray();
        }
        private static bool Contains(string value,string query)=>value!=null&&value.IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0;
    }
    public sealed class StoryCatalog
    {
        public StoryModel[] stories;
        public static StoryCatalog ParseEnvelope(string json)
        {
            var response=SongCatalog.Read<StoryEnvelope>(json);if(response==null||response.status!=1||response.data?.stories==null)throw new FormatException("Story envelope rejected");
            var ids=new HashSet<string>();foreach(var story in response.data.stories)
            {
                if(story==null||string.IsNullOrWhiteSpace(story.story_id)||!ids.Add(story.story_id)||story.chapters==null)throw new FormatException("Invalid story/chapter list");
                foreach(var chapter in story.chapters)if(chapter?.stage_info?.stage==null||string.IsNullOrWhiteSpace(chapter.stage_info.stage.sid))throw new FormatException("Missing chapter stage_info");
            }
            return response.data;
        }
    }
    public sealed class StoryEnvelope {public int status;public StoryCatalog data;}
    public sealed class StoryModel {public string story_id,title;public SongImage cover;public SongRestriction restriction;public ChapterModel[] chapters;}
    public sealed class ChapterModel {public SongItem stage_info;public RemoteSongFile begin_animation,end_animation;}
}
