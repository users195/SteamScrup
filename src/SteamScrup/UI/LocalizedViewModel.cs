using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SteamScrup.UI;

/// <summary>
/// Base class providing change notification plus a localized string indexer so XAML can
/// bind text with <c>{Binding [key.name]}</c> and update instantly on a language switch.
/// </summary>
public abstract class LocalizedViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Localized lookup used by XAML: <c>Text="{Binding [tb.scan]}"</c>.</summary>
    public string this[string key]
    {
        get => Core.L.T(key);

        // Writable on purpose and intentionally ignored. Some control templates
        // (ProgressBar's Track, for instance) bind TwoWay by default and will try to
        // write back into whatever the target is bound to. A get-only indexer turns that
        // into a fatal InvalidOperationException during Show(); accepting and dropping
        // the write keeps the binding engine happy without letting the UI set text.
        set { /* read-only in practice */ }
    }

    /// <summary>Re-evaluates every localized binding in the UI.</summary>
    public void RefreshLocalization()
    {
        OnPropertyChanged("Item[]");
        OnPropertyChanged(string.Empty);
        OnLocalizationChanged();
    }

    protected virtual void OnLocalizationChanged()
    {
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected void Raise(params string[] names)
    {
        foreach (var n in names) OnPropertyChanged(n);
    }
}
