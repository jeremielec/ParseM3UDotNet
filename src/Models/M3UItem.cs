using Helpers;
using ParseM3UNet.Helpers;

public record M3UItem(string Name, string FileName, string GroupName, M3UItemTypeEnum ItemType, string Url, string? Season)
{


    public string GetStrmPath(KnownDirectory d)
            => ItemType == M3UItemTypeEnum.TVSHOW
                ? Path.Combine(d.pathTvShow, GroupName, Season ?? "ERR", Name.Replace('/', ' ') + ".strm")
                : Path.Combine(d.pathMovie, GroupName, Name.Replace('/', ' ') + ".strm");
};