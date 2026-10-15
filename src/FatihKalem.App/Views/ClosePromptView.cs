using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace FatihKalem.App.Views;

/// <summary>"Close the board?" shown inline (a separate dialog window would fight the always-on-top overlay).</summary>
public sealed class ClosePromptView : Border
{
    public ClosePromptView()
    {
        Background = Brushes.White;
        BorderBrush = new SolidColorBrush(Color.FromRgb(0x9A, 0xA3, 0xB5));
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(10);
        Padding = new Thickness(16);
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;

        var yes = new Button { Name = "CloseYes", Content = "Evet, kapat", Margin = new Thickness(0, 0, 8, 0) };
        var no = new Button { Name = "CloseNo", Content = "Vazgeç" };
        yes.Click += (_, _) => Confirmed?.Invoke();
        no.Click += (_, _) => Cancelled?.Invoke();

        Child = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = "Fatih Kalem kapatılsın mı?", FontSize = 18 },
                new StackPanel { Orientation = Orientation.Horizontal, Children = { yes, no } },
            },
        };
    }

    public event Action? Confirmed;

    public event Action? Cancelled;
}
