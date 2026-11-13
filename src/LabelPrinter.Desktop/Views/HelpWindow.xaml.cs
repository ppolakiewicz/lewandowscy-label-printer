using System.Windows;

namespace LabelPrinter.Desktop.Views;

/// <summary>Short user manual, version and where the data is stored.</summary>
public partial class HelpWindow : Window
{
    public HelpWindow()
    {
        InitializeComponent();
        AppIcon.ApplyTo(this);
        VersionText.Text = "Etykiety, wersja " + AppState.Version;
        DataText.Text =
            $"Ustawienia i baza tekstów są w folderze {AppPaths.DataDirectory} (pliki settings.json i labels.db). "
            + "Kopia zapasowa: zamknij program i skopiuj cały folder, albo wyeksportuj bazę do CSV w oknie „Baza tekstów”.";
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void DataFolder_Click(object sender, RoutedEventArgs e) => MainWindow.OpenDataFolder(this);
}
