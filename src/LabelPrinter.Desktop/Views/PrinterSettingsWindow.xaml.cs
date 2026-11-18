using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LabelPrinter.Core;
using LabelPrinter.Core.Epl;
using LabelPrinter.Desktop.Printing;
using LabelPrinter.Desktop.Rendering;
using Microsoft.Win32;

namespace LabelPrinter.Desktop.Views;

/// <summary>
/// Printer choice and calibration (the stage-1 window): label size, gap, offsets, darkness, speed, ZT/ZB,
/// test print, sensor auto-calibration, printer configuration label, test job to a file.
/// </summary>
public partial class PrinterSettingsWindow : Window
{
    private static readonly string[] PreferredPrinterHints = ["2844", "zebra", "generic", "text only"];

    private readonly AppState _state;
    private bool _ready;

    internal PrinterSettingsWindow(AppState state)
    {
        _state = state;
        InitializeComponent();
        AppIcon.ApplyTo(this);

        for (int speed = PrinterConfig.MinSpeed; speed <= PrinterConfig.MaxSpeed; speed++)
        {
            SpeedCombo.Items.Add(speed);
        }

        var config = state.Config;
        LoadPrinters(config.PrinterName);
        ShowConfig(config);

        _ready = true;
        UpdatePreview();
        if (string.IsNullOrEmpty(config.PrinterName))
        {
            SetStatus(PrinterCombo.SelectedItem is null
                ? "Nie znaleziono żadnej drukarki. Dodaj drukarkę w Windows i kliknij „Odśwież”."
                : "Sprawdź, czy wybrana jest właściwa drukarka, zrób wydruk testowy i zapisz ustawienia.", isError: false);
        }
        else
        {
            SetStatus("Ustawienia: " + AppPaths.SettingsFile, isError: false);
        }

        Closing += Window_Closing;
    }

    private void LoadPrinters(string? preferred)
    {
        PrinterCombo.Items.Clear();
        IReadOnlyList<string> names;
        try
        {
            names = PrinterList.GetInstalled();
        }
        catch (Exception ex)
        {
            SetStatus("Nie udało się pobrać listy drukarek: " + ex.Message, isError: true);
            names = [];
        }

        foreach (var name in names)
        {
            PrinterCombo.Items.Add(name);
        }

        var selected = names.FirstOrDefault(n => n == preferred)
            ?? PreferredPrinterHints
                .Select(hint => names.FirstOrDefault(n => n.Contains(hint, StringComparison.OrdinalIgnoreCase)))
                .FirstOrDefault(n => n != null)
            ?? names.FirstOrDefault();
        PrinterCombo.SelectedItem = selected;
    }

    private void ShowConfig(PrinterConfig config)
    {
        WidthBox.Text = Format(config.LabelWidthMm);
        PrintWidthBox.Text = Format(config.PrintWidthMm);
        RollGuideCombo.SelectedIndex = (int)config.RollGuide;
        HeightBox.Text = Format(config.LabelHeightMm);
        GapBox.Text = Format(config.GapMm);
        OffsetXBox.Text = Format(config.OffsetXMm);
        OffsetYBox.Text = Format(config.OffsetYMm);
        DarknessSlider.Value = config.Darkness;
        DarknessValue.Text = config.Darkness.ToString(CultureInfo.CurrentCulture);
        SpeedCombo.SelectedItem = config.Speed;
        TopFirstCheck.IsChecked = config.PrintTopFirst;
        InvertCheck.IsChecked = config.InvertGraphics;
    }

    private bool TryReadConfig(out PrinterConfig config, out string error)
    {
        config = new PrinterConfig();
        error = string.Empty;

        if (!TryParse(WidthBox.Text, out var width)) { error = "Nieprawidłowa szerokość etykiety."; return false; }
        if (!TryParse(PrintWidthBox.Text, out var printWidth)) { error = "Nieprawidłowa szerokość druku drukarki."; return false; }
        if (width > printWidth) { error = "Etykieta nie może być szersza niż szerokość druku drukarki."; return false; }
        if (!TryParse(HeightBox.Text, out var height)) { error = "Nieprawidłowa wysokość etykiety."; return false; }
        if (!TryParse(GapBox.Text, out var gap)) { error = "Nieprawidłowa przerwa między etykietami."; return false; }
        if (!TryParse(OffsetXBox.Text, out var offsetX)) { error = "Nieprawidłowe przesunięcie w poziomie."; return false; }
        if (!TryParse(OffsetYBox.Text, out var offsetY)) { error = "Nieprawidłowe przesunięcie w pionie."; return false; }

        config.PrinterName = PrinterCombo.SelectedItem as string;
        config.LabelWidthMm = width;
        config.PrintWidthMm = printWidth;
        config.RollGuide = (RollGuide)RollGuideCombo.SelectedIndex;
        config.LabelHeightMm = height;
        config.GapMm = gap;
        config.OffsetXMm = offsetX;
        config.OffsetYMm = offsetY;
        config.Darkness = (int)Math.Round(DarknessSlider.Value);
        config.Speed = SpeedCombo.SelectedItem is int speed ? speed : 2;
        config.PrintTopFirst = TopFirstCheck.IsChecked == true;
        config.InvertGraphics = InvertCheck.IsChecked == true;
        config = config.Normalized();
        return true;
    }

    private void TestPrint_Click(object sender, RoutedEventArgs e) =>
        SendToPrinter("Wydruk testowy", EplLabels.TestLabel);

    private void AutoSense_Click(object sender, RoutedEventArgs e) =>
        SendToPrinter("Auto-kalibracja czujnika", _ => EplLabels.AutoSense());

    private void PrinterConfiguration_Click(object sender, RoutedEventArgs e) =>
        SendToPrinter("Konfiguracja drukarki", _ => EplLabels.PrintConfiguration());

    private void SendToPrinter(string action, Func<PrinterConfig, byte[]> buildJob)
    {
        if (!TryReadConfig(out var config, out var error))
        {
            SetStatus(error, isError: true);
            return;
        }

        if (string.IsNullOrEmpty(config.PrinterName))
        {
            SetStatus("Wybierz drukarkę.", isError: true);
            return;
        }

        try
        {
            RawPrinter.Send(config.PrinterName, buildJob(config), "Etykiety – " + action);
        }
        catch (Exception ex)
        {
            SetStatus($"{action}: błąd – {ex.Message}", isError: true);
            return;
        }

        // As in stage 1: settings that printed successfully are kept.
        if (TrySave(config))
        {
            SetStatus($"{action}: wysłano do „{config.PrinterName}”. Ustawienia zapisane.", isError: false);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadConfig(out var config, out var error))
        {
            SetStatus(error, isError: true);
            return;
        }

        if (TrySave(config))
        {
            ShowConfig(config); // shows values after clamping
            SetStatus(string.IsNullOrEmpty(config.PrinterName)
                ? "Zapisano ustawienia, ale nie wybrano drukarki."
                : "Zapisano ustawienia.", isError: string.IsNullOrEmpty(config.PrinterName));
        }
    }

    private bool TrySave(PrinterConfig config)
    {
        try
        {
            _state.SaveConfig(config);
            return true;
        }
        catch (Exception ex)
        {
            SetStatus("Nie udało się zapisać ustawień: " + ex.Message, isError: true);
            return false;
        }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!TryReadConfig(out var config, out var error))
        {
            var discard = MessageBox.Show(this, error + "\n\nZamknąć okno bez zapisywania zmian?", Title,
                MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            e.Cancel = discard != MessageBoxResult.Yes;
            return;
        }

        if (config.SameSettings(_state.Config))
        {
            return;
        }

        var answer = MessageBox.Show(this, "Zapisać zmienione ustawienia drukarki?", Title,
            MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Yes);
        if (answer == MessageBoxResult.Cancel || (answer == MessageBoxResult.Yes && !TrySave(config)))
        {
            e.Cancel = true;
        }
    }

    private void SaveTestFile_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadConfig(out var config, out var error))
        {
            SetStatus(error, isError: true);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "Polecenia EPL (*.epl)|*.epl|Wszystkie pliki (*.*)|*.*",
            FileName = "test-etykiety.epl",
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            File.WriteAllBytes(dialog.FileName, EplLabels.TestLabel(config));
            SetStatus("Zapisano: " + dialog.FileName, isError: false);
        }
        catch (Exception ex)
        {
            SetStatus("Nie udało się zapisać pliku: " + ex.Message, isError: true);
        }
    }

    private void RefreshPrinters_Click(object sender, RoutedEventArgs e) =>
        LoadPrinters(PrinterCombo.SelectedItem as string);

    private void Settings_TextChanged(object sender, TextChangedEventArgs e) => UpdatePreview();

    private void Settings_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdatePreview();

    private void Settings_Click(object sender, RoutedEventArgs e) => UpdatePreview();

    private void DarknessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (DarknessValue != null)
        {
            DarknessValue.Text = ((int)Math.Round(e.NewValue)).ToString(CultureInfo.CurrentCulture);
        }
    }

    private void UpdatePreview()
    {
        if (!_ready)
        {
            return;
        }

        if (!TryReadConfig(out var config, out var error))
        {
            PrintAreaText.Text = error;
            return;
        }

        var rollPosition = config.RollGuide switch
        {
            RollGuide.LeftEdge => "przy lewej krawędzi głowicy",
            RollGuide.RightEdge => "przy prawej krawędzi głowicy",
            _ => "na środku głowicy",
        };
        PrintAreaText.Text = string.Format(CultureInfo.CurrentCulture,
            "Rolka jest {0}, więc program drukuje od {1:0.#} do {2:0.#} mm szerokości druku.",
            rollPosition, config.LeftMarginMm, config.LeftMarginMm + config.LabelWidthMm);
        PreviewImage.Source = EplLabels.TestPattern(config).ToBitmapSource();
    }

    private void SetStatus(string message, bool isError)
    {
        StatusText.Text = message;
        StatusText.Foreground = isError ? Brushes.Firebrick : SystemColors.ControlTextBrush;
    }

    private static bool TryParse(string text, out double value) =>
        double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
        && double.IsFinite(value);

    private static string Format(double value) => value.ToString("0.##", CultureInfo.CurrentCulture);
}
