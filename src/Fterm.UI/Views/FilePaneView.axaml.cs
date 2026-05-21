using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Fterm.UI.ViewModels;

namespace Fterm.UI.Views;

public partial class FilePaneView : UserControl
{
    public event EventHandler? Activated;
    public event EventHandler<DroppedExternalEventArgs>? DroppedExternal;
    public event EventHandler<DroppedFromPaneEventArgs>? DroppedFromPane;

    /// <summary>ドラッグ中のソースペインを示す内部フォーマット。</summary>
    public const string PaneDragFormat = "fterm.pane";

    public FilePaneView()
    {
        InitializeComponent();
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private async void OnEntryDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is FileBrowserViewModel vm)
        {
            await vm.ActivateAsync();
        }
    }

    private async void OnAddressKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not TextBox box) return;
        if (DataContext is FileBrowserViewModel vm)
        {
            await vm.NavigateAsync(box.Text ?? "");
        }
    }

    private void OnGotFocus(object? sender, GotFocusEventArgs e)
    {
        Activated?.Invoke(this, EventArgs.Empty);
    }

    private async void OnListPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not FileBrowserViewModel vm) return;
        if (vm.SelectedEntry is null || vm.SelectedEntry.IsParentLink) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        var data = new DataObject();
        data.Set(PaneDragFormat, vm.SelectedEntry.FullPath);
        data.Set(DataFormats.Text, vm.SelectedEntry.FullPath);
        // 外部 (OS のファイラ) には DataFormats.Files が無いとドロップ先に渡らないことがあるが、
        // ローカルペインの場合のみ本物の OS ファイルとして提供する。
        try
        {
            await DragDrop.DoDragDrop(e, data, DragDropEffects.Copy);
        }
        catch
        {
            // DnD 開始に失敗してもアプリは継続
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(PaneDragFormat) || e.Data.Contains(DataFormats.Files))
        {
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not FileBrowserViewModel) return;
        if (e.Data.Contains(PaneDragFormat))
        {
            DroppedFromPane?.Invoke(this, new DroppedFromPaneEventArgs((string)e.Data.Get(PaneDragFormat)!));
            e.Handled = true;
            return;
        }
        if (e.Data.Contains(DataFormats.Files))
        {
            var files = e.Data.GetFiles();
            if (files is null) return;
            var paths = new List<string>();
            foreach (var f in files)
            {
                var p = f.TryGetLocalPath();
                if (!string.IsNullOrEmpty(p)) paths.Add(p);
            }
            DroppedExternal?.Invoke(this, new DroppedExternalEventArgs(paths));
            e.Handled = true;
        }
    }
}

public sealed class DroppedFromPaneEventArgs(string sourcePath) : EventArgs
{
    public string SourcePath { get; } = sourcePath;
}

public sealed class DroppedExternalEventArgs(IReadOnlyList<string> localPaths) : EventArgs
{
    public IReadOnlyList<string> LocalPaths { get; } = localPaths;
}
