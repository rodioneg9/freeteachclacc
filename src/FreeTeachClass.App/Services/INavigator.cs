namespace FreeTeachClass.App;

/// <summary>
/// Decoupled page navigation for view models. Implemented by the shell window.
/// </summary>
public interface INavigator
{
    void Navigate(PageKey key);

    /// <summary>Re-applies the current theme to the whole window.</summary>
    void RefreshTheme();
}
