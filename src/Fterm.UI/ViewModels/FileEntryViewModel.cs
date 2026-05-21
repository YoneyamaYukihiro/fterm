using Fterm.Core.Sessions;

namespace Fterm.UI.ViewModels;

public sealed class FileEntryViewModel
{
    public RemoteEntry Entry { get; }
    public bool IsParentLink { get; }
    public string DisplayName => IsParentLink ? ".." : (Entry.IsDirectory ? Entry.Name + "/" : Entry.Name);
    public string SizeText => Entry.IsDirectory ? "" : FormatSize(Entry.Size);
    public string ModifiedText => Entry.LastWriteTime == DateTimeOffset.MinValue ? "" : Entry.LastWriteTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm");
    public string PermissionsText => Entry.Permissions ?? "";
    public bool IsDirectory => Entry.IsDirectory;
    public string FullPath => Entry.FullPath;

    public FileEntryViewModel(RemoteEntry entry, bool isParentLink = false)
    {
        Entry = entry;
        IsParentLink = isParentLink;
    }

    private static string FormatSize(long size)
    {
        string[] units = ["B", "KiB", "MiB", "GiB", "TiB"];
        var s = (double)size;
        var i = 0;
        while (s >= 1024 && i < units.Length - 1) { s /= 1024; i++; }
        return i == 0 ? $"{size} {units[i]}" : $"{s:0.##} {units[i]}";
    }
}
