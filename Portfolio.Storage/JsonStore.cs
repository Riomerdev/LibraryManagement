using System.Text.Json;

namespace Portfolio.Storage;

/// <summary>Small atomic JSON persistence adapter for single-instance portfolio services.</summary>
public sealed class JsonStore<TState> where TState : class, new()
{
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public JsonStore(string path) => _path = path;

    public async Task<TResult> ReadAsync<TResult>(
        Func<TState, TResult> reader,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return reader(await LoadAsync(cancellationToken));
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<TResult> UpdateAsync<TResult>(
        Func<TState, TResult> update,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var state = await LoadAsync(cancellationToken);
            var result = update(state);
            var directory = Path.GetDirectoryName(_path)!;
            Directory.CreateDirectory(directory);
            var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await using (var stream = new FileStream(
                temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, state, Options, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
            File.Move(temporary, _path, overwrite: true);
            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<TState> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path)) return new TState();
        await using var stream = File.OpenRead(_path);
        return await JsonSerializer.DeserializeAsync<TState>(stream, Options, cancellationToken)
            ?? new TState();
    }
}