using System.Linq;
using FreeTeachClass.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

namespace FreeTeachClass.App.Views;

/// <summary>Full view of one assignment: description, attachments, links and submission.</summary>
public sealed partial class AssignmentDetailPage : Page
{
    private readonly PageLoader _loader;

    public AssignmentDetailViewModel ViewModel { get; }

    public AssignmentDetailPage(AssignmentDetailViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        _loader = new PageLoader(this, token => ViewModel.LoadSubmissionAsync(token));
    }

    private async void OnAddFilesClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileOpenPicker
            {
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            };
            picker.FileTypeFilter.Add("*");

            var window = AppServices.Current.Provider.GetRequiredService<MainWindow>();
            var handle = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, handle);

            var files = await picker.PickMultipleFilesAsync();
            if (files is not null && files.Count > 0)
            {
                ViewModel.AddFiles(files.Select(file => file.Path));
            }
        }
        catch (Exception ex)
        {
            ViewModel.SubmissionWarning = "Не удалось открыть выбор файлов: " + ex.Message;
        }
    }

    private void OnRemoveFileClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string path })
        {
            ViewModel.RemoveFile(path);
        }
    }

    private void OnSubmissionLinkTextChanged(object sender, TextChangedEventArgs e) =>
        ViewModel.SubmissionLink = SubmissionLinkBox.Text;

    private async void OnSubmitClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Сдать работу?",
            Content = "Работа будет отправлена преподавателю. Прикреплённые файлы " +
                      "перейдут в его владение, а изменить сданную работу можно будет " +
                      "только после возврата её преподавателем.",
            PrimaryButtonText = "Сдать",
            CloseButtonText = "Отмена",
            DefaultButton = ContentDialogButton.Primary,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        await ViewModel.SubmitAsync();
    }
}
