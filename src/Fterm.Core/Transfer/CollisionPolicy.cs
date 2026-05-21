namespace Fterm.Core.Transfer;

public enum CollisionPolicy
{
    /// <summary>無条件で上書き。</summary>
    Overwrite,
    /// <summary>転送をスキップ。</summary>
    Skip,
    /// <summary>「_2」「_3」のように接尾辞をつけてリネーム。</summary>
    Rename,
    /// <summary>追記モードで開いて差分のみ転送（レジューム）。</summary>
    Resume,
    /// <summary>都度ユーザに尋ねる（コールバック側で 1 つを選ぶ）。</summary>
    Ask,
}

public sealed record CollisionPrompt(string SourcePath, string DestinationPath, long SourceSize, long ExistingSize);

public interface ICollisionPolicyResolver
{
    /// <summary>Ask モード時に呼ばれる。返り値は Ask 以外のいずれか。</summary>
    Task<CollisionPolicy> ResolveAsync(CollisionPrompt prompt, CancellationToken ct);
}
