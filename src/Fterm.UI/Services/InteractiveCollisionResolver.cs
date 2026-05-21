using Avalonia.Controls;
using Avalonia.Threading;
using Fterm.Core.Transfer;
using Fterm.UI.Views;

namespace Fterm.UI.Services;

/// <summary>
/// 衝突時に <see cref="CollisionDialog"/> を表示するリゾルバ。
/// 「今後の衝突にも適用」にチェックがあった場合、その後は同じ方針を即時返す。
/// </summary>
public sealed class InteractiveCollisionResolver : ICollisionPolicyResolver
{
    private readonly Window _owner;
    private CollisionPolicy? _sticky;
    private readonly object _lock = new();

    public InteractiveCollisionResolver(Window owner)
    {
        _owner = owner;
    }

    public async Task<CollisionPolicy> ResolveAsync(CollisionPrompt prompt, CancellationToken ct)
    {
        lock (_lock)
        {
            if (_sticky is { } s) return s;
        }

        var result = await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var dlg = CollisionDialog.Create(prompt);
            return await dlg.ShowDialog<CollisionDialogResult?>(_owner);
        });
        var decision = result?.Policy ?? CollisionPolicy.Skip;
        if (result?.ApplyToAll == true)
        {
            lock (_lock) _sticky = decision;
        }
        return decision;
    }
}
