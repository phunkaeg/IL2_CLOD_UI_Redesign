using System.Windows;

namespace PlaneLoadoutWpfTest.Services;

/// <summary>
/// Stack-based navigation service for the menu system.
/// Screens are FrameworkElements (UserControls).
/// The shell (MainWindow) subscribes to ScreenChanged and swaps
/// the ContentPresenter with a fade animation.
/// </summary>
public static class NavigationService
{
    private static readonly Stack<FrameworkElement> _stack = new();

    /// <summary>Fired whenever the current screen changes.</summary>
    public static event Action<FrameworkElement>? ScreenChanged;

    /// <summary>Navigate forward to a new screen, pushing the current one onto the stack.</summary>
    public static void GoTo(FrameworkElement screen)
    {
        _stack.Push(screen);
        ScreenChanged?.Invoke(screen);
    }

    /// <summary>Navigate back to the previous screen.</summary>
    public static void Back()
    {
        if (_stack.Count <= 1) return;
        _stack.Pop();
        ScreenChanged?.Invoke(_stack.Peek());
    }

    /// <summary>Replace the entire stack with a single root screen (e.g. after Exit → confirm).</summary>
    public static void ReplaceAll(FrameworkElement screen)
    {
        _stack.Clear();
        _stack.Push(screen);
        ScreenChanged?.Invoke(screen);
    }

    public static bool CanGoBack => _stack.Count > 1;
    public static FrameworkElement? Current => _stack.Count > 0 ? _stack.Peek() : null;
}
