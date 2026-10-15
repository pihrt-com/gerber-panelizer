using System.ComponentModel;
using System.Windows;

namespace GerberPanelizer.App;

public sealed class LocalizationService : INotifyPropertyChanged
{
    private ResourceDictionary strings = new();
    private readonly ResourceDictionary fallbackStrings = new() { Source = new Uri("/Resources/Strings.en.xaml", UriKind.Relative) };
    public static LocalizationService Instance { get; } = new();
    public string Language { get; private set; } = "cs";
    public event PropertyChangedEventHandler? PropertyChanged;

    public string this[string key] => strings[key] as string ?? fallbackStrings[key] as string ?? key;

    public void SetLanguage(string? language)
    {
        Language = language?.ToLowerInvariant() switch
        {
            "de" or "fr" => language.ToLowerInvariant(),
            "en" => "en",
            _ => "cs",
        };
        strings = new ResourceDictionary { Source = new Uri($"/Resources/Strings.{Language}.xaml", UriKind.Relative) };
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
    }
}
