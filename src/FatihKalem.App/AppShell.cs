using Avalonia.Controls;
using FatihKalem.App.Controls;
using FatihKalem.App.Library;
using FatihKalem.App.Platform;
using FatihKalem.App.ViewModels;
using FatihKalem.App.Views;
using FatihKalem.App.Windows;
using FatihKalem.Core.Ink;
using FatihKalem.Core.Settings;

namespace FatihKalem.App;

public sealed record AppPaths(string SettingsFile, string AutostartDir, string LibraryBuiltIn, string LibraryUser)
{
    public static AppPaths Default => new(
        SettingsStore.DefaultPath, Autostart.DefaultDirectory, LibraryService.DefaultBuiltInRoot, LibraryService.DefaultUserRoot);
}

/// <summary>Builds the board and wires its parts together: document, tools, layers, toolbar and pickers.</summary>
public sealed class AppShell
{
    private readonly LibraryService _library;
    private Control? _modal;

    public AppShell(AppPaths paths, IInputRegion? region = null)
    {
        Document = new InkDocument();
        Tool = new ToolState();
        var cache = new ImageCache();
        _library = new LibraryService(paths.LibraryBuiltIn, paths.LibraryUser);

        ViewModel = new ToolbarViewModel(Tool, Document, new SettingsStore(paths.SettingsFile), new Autostart(paths.AutostartDir));
        Window = new OverlayWindow(Document, Tool, region);
        BackgroundView = new BackgroundPageView(Document, cache);
        Toolbar = new ToolbarView(ViewModel);

        // bottom to top: background page, pictures, ink, picture handles, toolbar
        Window.Root.Children.Insert(0, BackgroundView);
        Window.Root.Children.Insert(1, new BoardItemsView(Document, cache));
        Window.Root.Children.Add(new ItemHandlesView(Document, cache));
        Window.Root.Children.Add(Toolbar);
        Window.RegisterInteractive(Toolbar);

        ViewModel.ModeChanged += () => Window.Mode = ViewModel.Mode;
        ViewModel.LibraryRequested += () => ShowPicker(LibraryPickerMode.Images);
        ViewModel.BackgroundRequested += () => ShowPicker(LibraryPickerMode.Backgrounds);
        Toolbar.CloseRequested += RequestClose;
    }

    public InkDocument Document { get; }

    public ToolState Tool { get; }

    public ToolbarViewModel ViewModel { get; }

    public OverlayWindow Window { get; }

    public ToolbarView Toolbar { get; }

    public BackgroundPageView BackgroundView { get; }

    public event Action? ExitRequested;

    /// <summary>Brings the board forward, showing it first if it was preloaded hidden.</summary>
    public void Activate()
    {
        if (!Window.IsVisible) Window.Show();
        Window.Activate();
    }

    private void RequestClose()
    {
        if (!ViewModel.Settings.IsCloseConfirmationEnabled)
        {
            ExitRequested?.Invoke();
            return;
        }
        var prompt = new ClosePromptView();
        prompt.Confirmed += () =>
        {
            CloseModal();
            ExitRequested?.Invoke();
        };
        prompt.Cancelled += CloseModal;
        ShowModal(prompt);
    }

    private void ShowPicker(LibraryPickerMode mode)
    {
        var picker = new LibraryPickerView(_library, Document, mode, Placement);
        picker.CloseRequested += CloseModal;
        ShowModal(picker);
    }

    private LibraryPlacement Placement()
    {
        var bounds = Window.Root.Bounds;
        return new LibraryPlacement(bounds.Width / 2, bounds.Height / 2, Math.Min(bounds.Width, bounds.Height) * 0.4);
    }

    private void ShowModal(Control control)
    {
        CloseModal();
        _modal = control;
        Window.Root.Children.Add(control);
        Window.RegisterInteractive(control);
    }

    private void CloseModal()
    {
        if (_modal is null) return;
        Window.UnregisterInteractive(_modal);
        Window.Root.Children.Remove(_modal);
        _modal = null;
    }
}
