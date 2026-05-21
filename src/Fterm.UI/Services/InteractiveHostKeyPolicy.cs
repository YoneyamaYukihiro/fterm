using Avalonia.Controls;
using Avalonia.Threading;
using Fterm.Protocols.Ssh;
using Fterm.UI.Views;

namespace Fterm.UI.Services;

public sealed class InteractiveHostKeyPolicy : IHostKeyPolicy
{
    private readonly Window _owner;

    public InteractiveHostKeyPolicy(Window owner)
    {
        _owner = owner;
    }

    public async Task<HostKeyDecision> DecideAsync(HostKeyPrompt prompt, CancellationToken ct)
    {
        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var dlg = HostKeyDialog.Create(prompt);
            return await dlg.ShowDialog<HostKeyDecision>(_owner);
        });
    }
}
