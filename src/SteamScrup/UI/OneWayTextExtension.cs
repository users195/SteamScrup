using System.Windows.Data;
using System.Windows.Markup;

namespace SteamScrup.UI;

/// <summary>
/// A one-way text binding usable from XAML: <c>Text="{ui:OneWayText SomeProperty}"</c>.
///
/// Some targets bind two-way by default (<see cref="System.Windows.Documents.Run"/>.Text,
/// ProgressBar.Value, TextBox.Text). Pointing one of those at a read-only property throws
/// "A TwoWay or OneWayToSource binding cannot work on the read-only property ..." during
/// Show(), which takes the whole window down.
///
/// XAML markup has no <c>Mode=OneWay</c> shorthand, so this extension builds the Binding
/// object explicitly. Use it for any text target that is read-only at the source.
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class OneWayTextExtension : MarkupExtension
{
    public OneWayTextExtension()
    {
    }

    public OneWayTextExtension(string path) => Path = path;

    [ConstructorArgument("path")]
    public string? Path { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding(Path) { Mode = BindingMode.OneWay };

        // When the target is not a DependencyProperty, hand the binding object straight
        // back so a parent (such as a Setter) can use it.
        if (serviceProvider?.GetService(typeof(System.Windows.Markup.IProvideValueTarget))
            is System.Windows.Markup.IProvideValueTarget target &&
            target.TargetProperty is System.Windows.DependencyProperty)
        {
            return binding.ProvideValue(serviceProvider);
        }

        return binding;
    }
}
