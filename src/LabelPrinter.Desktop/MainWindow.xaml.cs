using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LabelPrinter.Core;
using LabelPrinter.Core.Epl;
using LabelPrinter.Core.Graphics;
using LabelPrinter.Desktop.Printing;
using Microsoft.Win32;

namespace LabelPrinter.Desktop;

public partial class MainWindow : Window
{
    private static readonly string[] PreferredPrinterHints = ["2844", "zebra", "generic", "text only"];

    private bool _ready;

    public MainWindow()
    {
        InitializeComponent();

        for (int speed = PrinterConfig.MinSpeed; speed <= PrinterConfig.MaxSpeed; speed++)
        {
            SpeedCombo.Items.Add(speed);
        }

        var config = PrinterConfigStore.Load(AppPaths.SettingsFile);
        LoadPrinters(config.PrinterName);
        ShowConfig(config);

        _ready = true;
        UpdatePreview();
        SetStatus("Ustawienia: " + AppPaths.SettingsFile, isError: false);
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
        if (!TryParse(HeightBox.Text, out var height)) { error = "Nieprawidłowa wysokość etykiety."; return false; }
        if (!TryParse(GapBox.Text, out var gap)) { error = "Nieprawidłowa przerwa między etykietami."; return false; }
        if (!TryParse(OffsetXBox.Text, out var offsetX)) { error = "Nieprawidłowe przesunięcie w poziomie."; return false; }
        if (!TryParse(OffsetYBox.Text, out var offsetY)) { error = "Nieprawidłowe przesunięcie w pionie."; return false; }

        config.PrinterName = PrinterCombo.SelectedItem as string;
        config.LabelWidthMm = width;
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
            PrinterConfigStore.Save(AppPaths.SettingsFile, config);
            SetStatus($"{action}: wysłano do „{config.PrinterName}”.", isError: false);
        }
        catch (Exception ex)
        {
            SetStatus($"{action}: błąd – {ex.Message}", isError: true);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadConfig(out var config, out var error))
        {
            SetStatus(error, isError: true);
            return;
        }

        try
        {
            PrinterConfigStore.Save(AppPaths.SettingsFile, config);
            ShowConfig(config); // shows values after clamping
            SetStatus("Zapisano ustawienia.", isError: false);
        }
        catch (Exception ex)
        {
            SetStatus("Nie udało się zapisać ustawień: " + ex.Message, isError: true);
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
        if (!_ready || !TryReadConfig(out var config, out _))
        {
            return;
        }

        PreviewImage.Source = ToBitmapSource(EplLabels.TestPattern(config));
    }

    private void SetStatus(string message, bool isError)
    {
        StatusText.Text = message;
        StatusText.Foreground = isError ? Brushes.Firebrick : SystemColors.ControlTextBrush;
    }

    private static BitmapSource ToBitmapSource(MonoBitmap bitmap)
    {
        var source = BitmapSource.Create(
            bitmap.Width, bitmap.Height, 203, 203, PixelFormats.BlackWhite, null,
            bitmap.ToPackedRows(blackIsZero: true), bitmap.BytesPerRow);
        source.Freeze();
        return source;
    }

    private static bool TryParse(string text, out double value) =>
        double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
        && double.IsFinite(value);

    private static string Format(double value) => value.ToString("0.##", CultureInfo.CurrentCulture);
}
