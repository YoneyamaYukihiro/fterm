namespace Fterm.Core.Connections;

public interface IConnectionStore
{
    Task<IReadOnlyList<Connection>> LoadAllAsync(CancellationToken ct = default);
    Task SaveAsync(Connection connection, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
