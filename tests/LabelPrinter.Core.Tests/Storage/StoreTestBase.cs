using LabelPrinter.Core.Storage;

namespace LabelPrinter.Core.Tests.Storage;

/// <summary>Temp folder with database files, deleted after each test; a controllable clock.</summary>
public abstract class StoreTestBase : IDisposable
{
    private readonly List<LabelTextStore> _stores = [];

    protected StoreTestBase()
    {
        Directory.CreateDirectory(Dir);
    }

    protected string Dir { get; } = Path.Combine(Path.GetTempPath(), "label-printer-tests-" + Guid.NewGuid().ToString("N"));

    protected FakeTime Time { get; } = new(new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero));

    protected string DbPath => Path.Combine(Dir, "sub", "labels.db");

    protected LabelTextStore Open(string? path = null)
    {
        var store = new LabelTextStore(path ?? DbPath, Time);
        _stores.Add(store);
        return store;
    }

    public void Dispose()
    {
        foreach (var store in _stores)
        {
            store.Dispose();
        }

        if (Directory.Exists(Dir))
        {
            Directory.Delete(Dir, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    protected sealed class FakeTime(DateTimeOffset start) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = start;

        public override DateTimeOffset GetUtcNow() => Now;

        public void Advance(TimeSpan by) => Now += by;
    }
}
