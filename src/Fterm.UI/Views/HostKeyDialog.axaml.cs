using Avalonia.Controls;
using Avalonia.Interactivity;
using Fterm.Protocols.Ssh;

namespace Fterm.UI.Views;

public partial class HostKeyDialog : Window
{
    public HostKeyDialog()
    {
        InitializeComponent();
    }

    public static HostKeyDialog Create(HostKeyPrompt prompt)
    {
        var dlg = new HostKeyDialog();
        var header = dlg.FindControl<TextBlock>("HeaderText")!;
        var host = dlg.FindControl<TextBlock>("HostText")!;
        var fp = dlg.FindControl<TextBlock>("FingerprintText")!;
        var detail = dlg.FindControl<TextBlock>("DetailText")!;

        header.Text = prompt.Result switch
        {
            KnownHostResult.Unknown => "未知のホスト鍵です",
            KnownHostResult.Mismatch => "⚠ ホスト鍵が以前と異なります",
            _ => "ホスト鍵を確認してください",
        };
        host.Text = $"{prompt.Host}:{prompt.Port}";
        fp.Text = prompt.Fingerprint;
        detail.Text = prompt.Result == KnownHostResult.Mismatch
            ? "中間者攻撃の可能性があります。心当たりがない場合は拒否してください。"
            : "信頼できる経路でフィンガープリントを確認してから許可してください。";
        return dlg;
    }

    private void OnReject(object? sender, RoutedEventArgs e) => Close(HostKeyDecision.Reject);
    private void OnAccept(object? sender, RoutedEventArgs e) => Close(HostKeyDecision.Accept);
    private void OnAcceptAndTrust(object? sender, RoutedEventArgs e) => Close(HostKeyDecision.AcceptAndTrust);
}
