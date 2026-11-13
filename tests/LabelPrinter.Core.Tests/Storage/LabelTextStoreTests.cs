using LabelPrinter.Core.Layout;
using LabelPrinter.Core.Storage;
using Microsoft.Data.Sqlite;

namespace LabelPrinter.Core.Tests.Storage;

public sealed class LabelTextStoreTests : StoreTestBase
{
    private static LabelContent Auto(string text) => LabelContent.FromText(text);

    private static LabelContent Manual(params string[] lines) => new() { Mode = LayoutMode.Manual, Lines = lines };

    private static string[] Texts(IEnumerable<SavedLabel> labels) => labels.Select(l => l.Text).ToArray();

    private void Use(LabelTextStore store, string text, int times)
    {
        for (var i = 0; i < times; i++)
        {
            store.RecordUse(Auto(text));
            Time.Advance(TimeSpan.FromMinutes(1));
        }
    }

    // ---- schema ---------------------------------------------------------------------------------------------

    [Fact]
    public void Open_CreatesFolderFileSchemaVersionAndWal()
    {
        using (Open())
        {
        }

        Assert.True(File.Exists(DbPath));
        using var connection = new SqliteConnection($"Data Source={DbPath};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        Assert.Equal(LabelTextStore.SchemaVersion, Convert.ToInt32(command.ExecuteScalar()));
        command.CommandText = "PRAGMA journal_mode;";
        Assert.Equal("wal", (string)command.ExecuteScalar()!);
    }

    [Fact]
    public void Open_NewerSchema_Throws()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DbPath)!);
        using (var connection = new SqliteConnection($"Data Source={DbPath};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version = 99;";
            command.ExecuteNonQuery();
        }

        Assert.Throws<NotSupportedException>(() => new LabelTextStore(DbPath));
    }

    [Fact]
    public void Reopen_KeepsEverything()
    {
        long id;
        using (var store = new LabelTextStore(DbPath, Time))
        {
            var content = Manual("Żółw", "Łódź; \"ok\"") with
            {
                Alignment = LabelAlignment.Left,
                Bold = true,
                FontSizeDots = 42.5,
                FontFamily = "Verdana",
            };
            store.RecordUse(content);
            store.RecordUse(content);
            id = store.Add(Auto("Drugi"), "notatka").Id;
        }

        using var reopened = Open();
        Assert.Equal(2, reopened.Count());
        var first = reopened.Search("zolw").Single();
        Assert.Equal(LayoutMode.Manual, first.Content.Mode);
        Assert.Equal(new[] { "Żółw", "Łódź; \"ok\"" }, first.Content.Lines);
        Assert.Equal(LabelAlignment.Left, first.Content.Alignment);
        Assert.True(first.Content.Bold);
        Assert.Equal(42.5, first.Content.FontSizeDots);
        Assert.Equal("Verdana", first.Content.FontFamily);
        Assert.Equal(2, first.UseCount);
        Assert.Equal(Time.Now.UtcDateTime, first.LastUsedUtc);
        Assert.Equal(DateTimeKind.Utc, first.LastUsedUtc!.Value.Kind);
        Assert.Equal("notatka", reopened.Get(id)!.Note);
    }

    // ---- RecordUse ------------------------------------------------------------------------------------------

    [Fact]
    public void RecordUse_NewText_CreatesEntry()
    {
        using var store = Open();

        var saved = store.RecordUse(Auto("  Mąka pszenna  "));

        Assert.Equal("Mąka pszenna", saved.Text);
        Assert.Equal(1, saved.UseCount);
        Assert.Equal(Time.Now.UtcDateTime, saved.CreatedUtc);
        Assert.Equal(Time.Now.UtcDateTime, saved.LastUsedUtc);
        Assert.Equal("Mąka pszenna", store.Get(saved.Id)!.Text);
    }

    [Fact]
    public void RecordUse_SameTextIgnoringCaseAndWhitespace_UpdatesOneEntry()
    {
        using var store = Open();
        var first = store.RecordUse(Auto("Mąka pszenna"));
        var created = first.CreatedUtc;
        Time.Advance(TimeSpan.FromHours(1));

        var second = store.RecordUse(Auto("  MĄKA \t pszenna ") with { Bold = true, FontSizeDots = 30, Alignment = LabelAlignment.Left });

        Assert.Equal(1, store.Count());
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(2, second.UseCount);
        Assert.Equal(created, second.CreatedUtc);
        Assert.Equal(Time.Now.UtcDateTime, second.LastUsedUtc);
        Assert.True(second.Content.Bold);
        Assert.Equal(30.0, second.Content.FontSizeDots);
        Assert.Equal(LabelAlignment.Left, second.Content.Alignment);
        Assert.Equal("MĄKA \t pszenna", second.Text); // the latest spelling is shown
    }

    [Fact]
    public void RecordUse_KeepsNote()
    {
        using var store = Open();
        var added = store.Add(Auto("Ser"), "z lodówki");

        var used = store.RecordUse(Auto("ser"));

        Assert.Equal(added.Id, used.Id);
        Assert.Equal("z lodówki", used.Note);
        Assert.Equal(1, used.UseCount);
    }

    [Fact]
    public void RecordUse_SameTextDifferentMode_SeparateEntries()
    {
        using var store = Open();

        store.RecordUse(Auto("Ala\nKot"));
        store.RecordUse(Manual("Ala", "Kot"));

        Assert.Equal(2, store.Count());
    }

    [Fact]
    public void RecordUse_DiacriticsMatter_ForUniqueness()
    {
        using var store = Open();

        store.RecordUse(Auto("Łódź"));
        store.RecordUse(Auto("Lodz"));

        Assert.Equal(2, store.Count());
    }

    [Fact]
    public void RecordUse_EmptyText_Throws()
    {
        using var store = Open();

        Assert.Throws<ArgumentException>(() => store.RecordUse(Auto("   ")));
        Assert.Throws<ArgumentException>(() => store.RecordUse(Manual("", " ")));
    }

    [Fact]
    public void Content_IsNormalised()
    {
        using var store = Open();

        var manual = store.RecordUse(Manual(" A ", "", "B ") with { FontFamily = " ", FontSizeDots = 0 });
        var auto = store.RecordUse(new LabelContent { Mode = LayoutMode.Auto, Lines = ["Jeden", "dwa\r\ntrzy"] });

        Assert.Equal(new[] { "A", "B" }, manual.Content.Lines);
        Assert.Equal(LabelContent.DefaultFontFamily, manual.Content.FontFamily);
        Assert.Null(manual.Content.FontSizeDots);
        Assert.Equal(new[] { "Jeden dwa trzy" }, auto.Content.Lines);
    }

    // ---- Search ---------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("zolta")]
    [InlineData("ŻÓŁTA")]
    [InlineData("żół")]
    [InlineData("LODKA")]
    [InlineData("łódka")]
    [InlineData("a l")]
    [InlineData("  zolta   lodka ")]
    public void Search_IgnoresCaseDiacriticsAndWhitespace(string query)
    {
        using var store = Open();
        store.RecordUse(Auto("Żółta łódka"));
        store.RecordUse(Auto("Niebieski"));

        Assert.Equal(new[] { "Żółta łódka" }, Texts(store.Search(query)));
    }

    [Fact]
    public void Search_MatchesAcrossManualLines()
    {
        using var store = Open();
        store.RecordUse(Manual("Ser żółty", "plastry"));

        Assert.Single(store.Search("zolty plas"));
    }

    [Fact]
    public void Search_RanksPrefixThenWordStartThenSubstringThenAnyOrder()
    {
        using var store = Open();
        Use(store, "Deser lodowy", 9);       // substring
        Use(store, "Pyszny ser-mix", 1);     // word start after a space
        Use(store, "Biały ser", 2);          // word start
        Use(store, "Ser żółty", 1);          // prefix
        Use(store, "Masło", 20);             // no match
        Use(store, "Mus z serwatki", 1);     // word start
        Use(store, "Etykieta-ser", 1);       // word start after '-'

        var result = Texts(store.Search("ser"));

        Assert.Equal("Ser żółty", result[0]);
        Assert.Equal("Deser lodowy", result[^1]);
        Assert.Equal(6, result.Length);
        Assert.Equal("Biały ser", result[1]); // most used among word-start matches
    }

    [Fact]
    public void Search_AnyOrderWordsComeLast()
    {
        using var store = Open();
        Use(store, "Pszenna mąka typ 650", 5);
        Use(store, "Mąka pszenna", 1);

        var result = Texts(store.Search("mąka psz"));

        Assert.Equal(new[] { "Mąka pszenna", "Pszenna mąka typ 650" }, result);
    }

    [Fact]
    public void Search_WithinGroup_ByUseCountThenLastUsed()
    {
        using var store = Open();
        Use(store, "Ser A", 1);
        Use(store, "Ser B", 3);
        Use(store, "Ser C", 1);
        store.Add(Auto("Ser D"));

        Assert.Equal(new[] { "Ser B", "Ser C", "Ser A", "Ser D" }, Texts(store.Search("ser")));
    }

    [Fact]
    public void Search_EmptyQuery_MostUsedAndRecentFirst_WithLimit()
    {
        using var store = Open();
        Use(store, "Jeden", 1);
        Use(store, "Dwa", 2);
        Use(store, "Trzy", 1);
        Use(store, "Cztery", 5);

        Assert.Equal(new[] { "Cztery", "Dwa", "Trzy" }, Texts(store.Search("", limit: 3)));
        Assert.Equal(4, store.Search(null).Count);
        Assert.Empty(store.Search("x", 0));
    }

    [Fact]
    public void Search_RespectsLimit()
    {
        using var store = Open();
        for (var i = 0; i < 15; i++)
        {
            store.RecordUse(Auto($"Etykieta {i}"));
        }

        Assert.Equal(10, store.Search("etyk").Count);
        Assert.Equal(15, store.GetAll("etyk").Count);
    }

    [Fact]
    public void Search_NoMatch_Empty()
    {
        using var store = Open();
        store.RecordUse(Auto("Ser"));

        Assert.Empty(store.Search("chleb"));
    }

    // ---- CRUD -----------------------------------------------------------------------------------------------

    [Fact]
    public void Add_ThenGet()
    {
        using var store = Open();

        var added = store.Add(Manual("Linia 1", "Linia 2") with { Bold = true }, "  uwaga  ");

        var loaded = store.Get(added.Id)!;
        Assert.Equal("Linia 1\nLinia 2", loaded.Text);
        Assert.True(loaded.Content.Bold);
        Assert.Equal("uwaga", loaded.Note);
        Assert.Equal(0, loaded.UseCount);
        Assert.Null(loaded.LastUsedUtc);
        Assert.Equal(Time.Now.UtcDateTime, loaded.CreatedUtc);
    }

    [Fact]
    public void Add_Duplicate_ThrowsWithExistingId()
    {
        using var store = Open();
        var existing = store.RecordUse(Auto("Ser"));

        var ex = Assert.Throws<DuplicateLabelException>(() => store.Add(Auto(" SER ")));

        Assert.Equal(existing.Id, ex.ExistingId);
        Assert.Equal(1, store.Count());
    }

    [Fact]
    public void Get_Missing_Null()
    {
        using var store = Open();

        Assert.Null(store.Get(12345));
    }

    [Fact]
    public void Update_ChangesContentAndNoteKeepsStats()
    {
        using var store = Open();
        var original = store.RecordUse(Auto("Ser"));
        store.RecordUse(Auto("Ser"));
        Time.Advance(TimeSpan.FromDays(1));

        var updated = store.Update(original.Id, Manual("Ser", "żółty") with { Alignment = LabelAlignment.Left }, "nowa");

        Assert.Equal(original.Id, updated.Id);
        Assert.Equal("Ser\nżółty", updated.Text);
        Assert.Equal(LayoutMode.Manual, updated.Content.Mode);
        Assert.Equal(LabelAlignment.Left, updated.Content.Alignment);
        Assert.Equal("nowa", updated.Note);
        Assert.Equal(2, updated.UseCount);
        Assert.Equal(original.CreatedUtc, updated.CreatedUtc);
        Assert.Equal(original.LastUsedUtc, updated.LastUsedUtc);
        Assert.Single(store.Search("zolty"));
        Assert.Equal(1, store.Count());
    }

    [Fact]
    public void Update_OnlyCaseChange_IsNotACollision()
    {
        using var store = Open();
        var saved = store.Add(Auto("ser"));

        var updated = store.Update(saved.Id, Auto("SER"), null);

        Assert.Equal("SER", updated.Text);
    }

    [Fact]
    public void Update_Collision_ThrowsAndChangesNothing()
    {
        using var store = Open();
        var ser = store.RecordUse(Auto("Ser"));
        var mleko = store.RecordUse(Auto("Mleko"));

        var ex = Assert.Throws<DuplicateLabelException>(() => store.Update(mleko.Id, Auto("ser"), null));

        Assert.Equal(ser.Id, ex.ExistingId);
        Assert.Equal("Mleko", store.Get(mleko.Id)!.Text);
        Assert.Equal(2, store.Count());
    }

    [Fact]
    public void Update_CollisionWithMerge_CombinesEntries()
    {
        using var store = Open();
        var ser = store.Add(Auto("Ser"), "stara");
        Use(store, "Ser", 2);
        var lastSerUse = store.Get(ser.Id)!.LastUsedUtc;
        Time.Advance(TimeSpan.FromDays(1));
        var mleko = store.RecordUse(Auto("Mleko"));

        var merged = store.Update(mleko.Id, Auto("SER") with { Bold = true }, null, mergeIfDuplicate: true);

        Assert.Equal(ser.Id, merged.Id);
        Assert.Null(store.Get(mleko.Id));
        Assert.Equal(1, store.Count());
        Assert.Equal("SER", merged.Text);
        Assert.True(merged.Content.Bold);
        Assert.Equal("stara", merged.Note);
        Assert.Equal(3, merged.UseCount);
        Assert.Equal(ser.CreatedUtc, merged.CreatedUtc);
        Assert.Equal(mleko.LastUsedUtc, merged.LastUsedUtc);
        Assert.True(merged.LastUsedUtc > lastSerUse);
    }

    [Fact]
    public void Update_Missing_Throws()
    {
        using var store = Open();

        Assert.Throws<KeyNotFoundException>(() => store.Update(99, Auto("x"), null));
    }

    [Fact]
    public void Delete_RemovesEntry()
    {
        using var store = Open();
        var saved = store.RecordUse(Auto("Ser"));

        Assert.True(store.Delete(saved.Id));
        Assert.False(store.Delete(saved.Id));
        Assert.Null(store.Get(saved.Id));
        Assert.Empty(store.Search("ser"));
        Assert.Equal(0, store.Count());
    }

    [Fact]
    public void GetAll_FiltersLikeSearch()
    {
        using var store = Open();
        Use(store, "Żółty ser", 1);
        Use(store, "Mleko", 3);
        Use(store, "Masło", 2);

        Assert.Equal(new[] { "Mleko", "Masło", "Żółty ser" }, Texts(store.GetAll()));
        Assert.Equal(new[] { "Żółty ser" }, Texts(store.GetAll("ZOLT")));
    }
}
