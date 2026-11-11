using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FreeTeachClass.App;

/// <summary>
/// Runs a page's async load when the page is shown and cancels it when the
/// page is hidden. Pages are cached singletons, so Loaded fires on every visit.
/// </summary>
public sealed class PageLoader
{
    private readonly Page _page;
    private readonly Func<CancellationToken, Task> _load;
    private CancellationTokenSource? _cts;

    public PageLoader(Page page, Func<CancellationToken, Task> load)
    {
        _page = page;
        _load = load;
        _page.Loaded += OnLoaded;
        _page.Unloaded += OnUnloaded;
    }

    public CancellationToken Token => _cts?.Token ?? CancellationToken.None;

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        try
        {
            await _load(Token);
        }
        catch (OperationCanceledException)
        {
            // Navigation away or app shutdown — not an error.
        }
        catch (Exception)
        {
            // View models log and surface errors themselves; never crash the UI thread.
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => _cts?.Cancel();
}
