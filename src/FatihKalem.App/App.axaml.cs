using Avalonia;
using Avalonia.Markup.Xaml;

namespace FatihKalem.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);
}
