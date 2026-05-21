using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fterm.Core.Connections;
using Fterm.Core.Security;

namespace Fterm.UI.ViewModels;

/// <summary>接続の新規作成 / 編集ダイアログ用 ViewModel。</summary>
public sealed partial class ConnectionEditorViewModel : ViewModelBase
{
    private readonly ICredentialStore _credentialStore;
    private readonly IConnectionStore? _connectionStore;

    public Guid Id { get; }
    public bool IsNew { get; }

    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSshLike), nameof(IsFileLike), nameof(IsSerial))]
    private ProtocolKind _protocol = ProtocolKind.Ssh;

    [ObservableProperty]
    private string _host = "";

    [ObservableProperty]
    private int _port = 22;

    [ObservableProperty]
    private string _username = "";

    [ObservableProperty]
    private AuthMethod _authMethod = AuthMethod.Password;

    [ObservableProperty]
    private CredentialSummary? _selectedCredential;

    [ObservableProperty]
    private string _newPassword = "";

    [ObservableProperty]
    private string _initialRemoteDirectory = "";

    [ObservableProperty]
    private string _initialLocalDirectory = "";

    [ObservableProperty]
    private string _encoding = "utf-8";

    [ObservableProperty]
    private string _terminalType = "xterm-256color";

    [ObservableProperty]
    private string _tagsText = "";

    [ObservableProperty]
    private string _onConnectMacro = "";

    /// <summary>選択可能な ProxyJump 候補（編集中の接続自身は除外）。</summary>
    public ObservableCollection<Connection> AvailableProxyHops { get; } = [];

    /// <summary>順序付きで選択された踏み台チェーン。</summary>
    public ObservableCollection<Connection> ProxyChain { get; } = [];

    /// <summary>OK 押下後にダイアログを閉じる側へ渡される、構築済みの Connection。null なら未保存。</summary>
    [ObservableProperty]
    private Connection? _result;

    public ObservableCollection<ProtocolKind> Protocols { get; } =
        [ProtocolKind.Ssh, ProtocolKind.Telnet, ProtocolKind.Sftp, ProtocolKind.Ftp, ProtocolKind.Ftps, ProtocolKind.Serial];

    public ObservableCollection<AuthMethod> AuthMethods { get; } =
        [AuthMethod.Password, AuthMethod.PublicKey, AuthMethod.KeyboardInteractive, AuthMethod.Agent];

    public ObservableCollection<CredentialSummary> Credentials { get; } = [];

    public bool IsSshLike => Protocol is ProtocolKind.Ssh or ProtocolKind.Sftp;
    public bool IsFileLike => Protocol is ProtocolKind.Sftp or ProtocolKind.Ftp or ProtocolKind.Ftps;
    public bool IsSerial => Protocol is ProtocolKind.Serial;

    public ConnectionEditorViewModel(ICredentialStore credentialStore, Connection? existing, IConnectionStore? connectionStore = null)
    {
        _credentialStore = credentialStore;
        _connectionStore = connectionStore;

        if (existing is null)
        {
            IsNew = true;
            Id = Guid.NewGuid();
            return;
        }

        IsNew = false;
        Id = existing.Id;
        Name = existing.Name;
        Protocol = existing.Protocol;
        Host = existing.Host;
        Port = existing.Port;
        Username = existing.Username;
        AuthMethod = existing.AuthMethod;
        InitialRemoteDirectory = existing.InitialRemoteDirectory ?? "";
        InitialLocalDirectory = existing.InitialLocalDirectory ?? "";
        Encoding = existing.Encoding;
        TerminalType = existing.TerminalType;
        TagsText = string.Join(", ", existing.Tags);
        OnConnectMacro = existing.OnConnectMacro;
        // ProxyChain は LoadProxyCandidatesAsync で復元
    }

    public async Task LoadCredentialsAsync(Guid? currentCredentialId)
    {
        Credentials.Clear();
        foreach (var s in await _credentialStore.ListAsync())
        {
            Credentials.Add(s);
        }
        if (currentCredentialId is { } id)
        {
            SelectedCredential = Credentials.FirstOrDefault(c => c.Id == id);
        }
    }

    public async Task LoadProxyCandidatesAsync(IReadOnlyList<Guid> currentChain)
    {
        AvailableProxyHops.Clear();
        ProxyChain.Clear();
        if (_connectionStore is null) return;
        var all = (await _connectionStore.LoadAllAsync())
            .Where(c => c.Id != Id && c.Protocol == ProtocolKind.Ssh)
            .ToList();
        var byId = all.ToDictionary(c => c.Id);
        foreach (var id in currentChain)
        {
            if (byId.TryGetValue(id, out var hop)) ProxyChain.Add(hop);
        }
        foreach (var c in all.Where(c => !currentChain.Contains(c.Id)))
        {
            AvailableProxyHops.Add(c);
        }
    }

    [RelayCommand]
    private void AddProxyHop(Connection? hop)
    {
        if (hop is null) return;
        AvailableProxyHops.Remove(hop);
        ProxyChain.Add(hop);
    }

    [RelayCommand]
    private void RemoveProxyHop(Connection? hop)
    {
        if (hop is null) return;
        ProxyChain.Remove(hop);
        AvailableProxyHops.Add(hop);
    }

    [RelayCommand]
    private void MoveProxyHopUp(Connection? hop)
    {
        if (hop is null) return;
        var i = ProxyChain.IndexOf(hop);
        if (i > 0) ProxyChain.Move(i, i - 1);
    }

    [RelayCommand]
    private void MoveProxyHopDown(Connection? hop)
    {
        if (hop is null) return;
        var i = ProxyChain.IndexOf(hop);
        if (i >= 0 && i < ProxyChain.Count - 1) ProxyChain.Move(i, i + 1);
    }

    partial void OnProtocolChanged(ProtocolKind value)
    {
        Port = value switch
        {
            ProtocolKind.Ssh or ProtocolKind.Sftp => 22,
            ProtocolKind.Telnet => 23,
            ProtocolKind.Ftp => 21,
            ProtocolKind.Ftps => 990,
            _ => Port,
        };
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Host))
        {
            return;
        }

        Guid? credentialId = SelectedCredential?.Id;
        if (!string.IsNullOrEmpty(NewPassword))
        {
            var newCred = new Credential
            {
                Id = Guid.NewGuid(),
                Name = $"{Name} ({Username})",
                Kind = CredentialKind.Password,
                Secret = NewPassword,
            };
            await _credentialStore.SaveAsync(newCred);
            credentialId = newCred.Id;
        }

        Result = new Connection
        {
            Id = Id,
            Name = Name.Trim(),
            Protocol = Protocol,
            Host = Host.Trim(),
            Port = Port,
            Username = Username.Trim(),
            AuthMethod = AuthMethod,
            CredentialId = credentialId,
            InitialRemoteDirectory = string.IsNullOrWhiteSpace(InitialRemoteDirectory) ? null : InitialRemoteDirectory,
            InitialLocalDirectory = string.IsNullOrWhiteSpace(InitialLocalDirectory) ? null : InitialLocalDirectory,
            Encoding = Encoding,
            TerminalType = TerminalType,
            Tags = ParseTags(TagsText),
            OnConnectMacro = OnConnectMacro ?? "",
            ProxyJumpConnectionIds = ProxyChain.Select(p => p.Id).ToList(),
        };
    }

    [RelayCommand]
    private void Cancel()
    {
        Result = null;
    }

    private static IReadOnlyList<string> ParseTags(string text) =>
        text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
