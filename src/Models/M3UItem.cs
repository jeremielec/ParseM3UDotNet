using ParseM3UNet.Helpers;

public record M3UItem(string Name, string FileName, string GroupName, M3UItemTypeEnum ItemType, string Url, string? Season)
{

};