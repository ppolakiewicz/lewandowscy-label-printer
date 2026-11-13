using System.Diagnostics;
using System.IO;
using System.Reflection;
using LabelPrinter.Core;
using LabelPrinter.Core.Storage;
using LabelPrinter.Desktop.Rendering;

namespace LabelPrinter.Desktop;

/// <summary>
/// Objects shared by all windows for the lifetime of the app: the printer settings (loaded once, replaced when the
/// settings dialog saves), the saved-text database (null when it could not be opened) and the label renderer.
/// </summary>
internal sealed class AppState : IDisposable
{
    private AppState(PrinterConfig config, bool settingsExisted, LabelTextStore? store, string? storeError)
    {
        Config = config;
        SettingsExisted = settingsExisted;
        Store = store;
        StoreError = storeError;
    }

    /// <summary>Raised after <see cref="SaveConfig"/> changed <see cref="Config"/>.</summary>
    public event EventHandler? ConfigChanged;

    /// <summary>Current printer settings (already normalized). Replace only through <see cref="SaveConfig"/>.</summary>
    public PrinterConfig Config { get; private set; }

    /// <summary>False on the very first start (no settings file yet).</summary>
    public bool SettingsExisted { get; }

    /// <summary>The saved-text database, or null when it could not be opened (printing still works).</summary>
    public LabelTextStore? Store { get; }

    /// <summary>Why the database could not be opened (Polish, for the user), or null.</summary>
    public string? StoreError { get; }

    /// <summary>Renders labels for the previews and for printing (UI thread only).</summary>
    public LabelRenderer Renderer { get; } = new();

    /// <summary>Program version shown in the help window, e.g. "0.1.42".</summary>
    public static string Version
    {
        get
        {
            var assembly = Assembly.GetEntryAssembly() ?? typeof(AppState).Assembly;
            var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? assembly.GetName().Version?.ToString()
                ?? "?";
            var plus = version.IndexOf('+');
            return plus > 0 ? version[..plus] : version;
        }
    }

    /// <summary>Loads the settings and opens the database; never throws.</summary>
    public static AppState Load()
    {
        bool existed;
        PrinterConfig config;
        try
        {
            existed = File.Exists(AppPaths.SettingsFile);
            config = PrinterConfigStore.Load(AppPaths.SettingsFile).Normalized();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Trace.WriteLine("Settings could not be read: " + ex);
            existed = true;
            config = new PrinterConfig();
        }

        LabelTextStore? store = null;
        string? error = null;
        try
        {
            store = new LabelTextStore(AppPaths.DatabaseFile);
        }
        catch (Exception ex)
        {
            error = $"Nie udało się otworzyć bazy tekstów ({AppPaths.DatabaseFile}): {ex.Message}";
        }

        return new AppState(config, existed, store, error);
    }

    /// <summary>Saves the settings to disk and makes them current.</summary>
    /// <exception cref="IOException">The file could not be written.</exception>
    public void SaveConfig(PrinterConfig config)
    {
        var normalized = config.Normalized();
        PrinterConfigStore.Save(AppPaths.SettingsFile, normalized);
        Config = normalized;
        ConfigChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => Store?.Dispose();
}
