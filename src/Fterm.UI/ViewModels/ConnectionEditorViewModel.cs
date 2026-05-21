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

    public ConnectionEditorViewModel(ICredentialStore credentialStore, Connection? existing)
    {
        _credentialStore = credentialStore;

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
