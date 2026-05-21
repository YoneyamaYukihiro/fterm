using Avalonia.Controls;
using Avalonia.Interactivity;
using Fterm.Core.Transfer;

namespace Fterm.UI.Views;

public sealed record CollisionDialogResult(CollisionPolicy Policy, bool ApplyToAll);

public partial class CollisionDialog : Window
{
    public CollisionDialog()
    {
        InitializeComponent();
    }

    public static CollisionDialog Create(CollisionPrompt prompt)
    {
        var dlg = new CollisionDialog();
        dlg.FindControl<TextBlock>("SrcPathText")!.Text = prompt.SourcePath;
        dlg.FindControl<TextBlock>("DstPathText")!.Text = prompt.DestinationPath;
        dlg.FindControl<TextBlock>("SrcSizeText")!.Text = FormatSize(prompt.SourceSize);
        dlg.FindControl<TextBlock>("DstSizeText")!.Text = prompt.ExistingSize < 0 ? "(不明)" : FormatSize(prompt.ExistingSize);
        return dlg;
    }

    private bool ApplyToAll => this.FindControl<CheckBox>("ApplyAllCheck")?.IsChecked == true;

    private void OnSkip(object? sender, RoutedEventArgs e) => Close(new CollisionDialogResult(CollisionPolicy.Skip, ApplyToAll));
    private void OnRename(object? sender, RoutedEventArgs e) => Close(new CollisionDialogResult(CollisionPolicy.Rename, ApplyToAll));
    private void OnResume(object? sender, RoutedEventArgs e) => Close(new CollisionDialogResult(CollisionPolicy.Resume, ApplyToAll));
    private void OnOverwrite(object? sender, RoutedEventArgs e) => Close(new CollisionDialogResult(CollisionPolicy.Overwrite, ApplyToAll));

    private static string FormatSize(long size)
    {
        string[] units = ["B", "KiB", "MiB", "GiB"];
        var s = (double)size;
        var i = 0;
        while (s >= 1024 && i < units.Length - 1) { s /= 1024; i++; }
        return i == 0 ? $"{size} {units[i]}" : $"{s:0.##} {units[i]}";
    }
}
