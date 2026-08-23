using Google.Apis.Tasks.v1.Data;
using GTasks.Models;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Graphics;

namespace GTasks;

public sealed partial class MainWindow : Window
{
    private bool _hubWasOpen = true;
    private string? _defaultListId;
    //private bool _loadingStartupSetting;
    private readonly string _windowStatePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GTasks", "windowstate.json");

    private readonly GoogleTasksService _googleTasks;

    public ObservableCollection<TaskListDisplay> Lists { get; } = new();
    public ObservableCollection<TaskListDisplay> FilteredLists { get; } = new();

    private readonly Dictionary<string, StickyWindow> _openStickyWindows = new();

    public MainWindow()
    {
        InitializeComponent();

        var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        AppWindow.SetTaskbarIcon(iconPath);

        if (Application.Current is App app &&
        Content is FrameworkElement root)
        {
            root.RequestedTheme = app.AppTheme;
        }

        _ = LoadStartupSettingAsync();

        _googleTasks = new GoogleTasksService();

        var presenter = AppWindow.Presenter as OverlappedPresenter;
        presenter?.SetBorderAndTitleBar(true, false);
        if (presenter != null)
        {
            presenter.PreferredMinimumWidth = 700;
            presenter.PreferredMinimumHeight = 500;
        }

        DragRegion.Loaded += DragRegion_Loaded;

        var appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)));

        appWindow.Resize(new global::Windows.Graphics.SizeInt32(600, 900));

        this.SizeChanged += MainWindow_SizeChanged;
        AppWindow.Changed += AppWindow_Changed;
        this.Closed += MainWindow_Closed;
        this.Activated += MainWindow_Activated;


        CenterWindow();

        AppWindow.Title = "GTasks";

        //_ = StartApplicationAsync();
    }

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            HideSettingsOverlay();
        }
    }

    public async void Sync_CalledFromStickyWindow()
    {
        // Sync again
        await SyncListsAsync();
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        HideSettingsOverlay();
        SaveWindowState();
    }

    public void SetHubOpenState(bool isOpen)
    {
        _hubWasOpen = isOpen;
        SaveWindowState();
    }

    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidPresenterChange || args.DidSizeChange)
        {
            // SetTitleBar(DragRegion);
            SetupDragRegion();
        }
    }

    private void MainWindow_SizeChanged(object sender, WindowSizeChangedEventArgs args)
    {
        SetupDragRegion();
    }

    private void DragRegion_Loaded(object sender, RoutedEventArgs e)
    {
        SetupDragRegion();
    }

    private void SetupDragRegion()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);

        var nonClientInput = InputNonClientPointerSource.GetForWindowId(windowId);

        var xamlRoot = DragRegion.XamlRoot;

        if (xamlRoot == null)
            return;

        var scale = xamlRoot.RasterizationScale;

        var rect = new RectInt32
        {
            X = 0,
            Y = 0,
            Width = (int)(DragRegion.ActualWidth * scale),
            Height = (int)(DragRegion.ActualHeight * scale)
        };

        nonClientInput.SetRegionRects(NonClientRegionKind.Caption, new[] { rect });

        var passthroughRects = new List<RectInt32>();

        AddPassthroughButton(nonClientInput, CloseButton, passthroughRects);
        AddPassthroughButton(nonClientInput, SettingsButton, passthroughRects);
        AddPassthroughButton(nonClientInput, SyncButton, passthroughRects);
        AddPassthroughButton(nonClientInput, AddButton, passthroughRects);

        nonClientInput.SetRegionRects(NonClientRegionKind.Passthrough, passthroughRects.ToArray());
    }

    private void CenterWindow()
    {
        var appWindow = this.AppWindow;

        var displayArea =
            Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(
                appWindow.Id,
                Microsoft.UI.Windowing.DisplayAreaFallback.Primary);

        var workArea = displayArea.WorkArea;

        var x = workArea.X + (workArea.Width - appWindow.Size.Width) / 2;
        var y = workArea.Y + (workArea.Height - appWindow.Size.Height) / 2;

        appWindow.Move(new global::Windows.Graphics.PointInt32(x, y));
    }

    private void AddPassthroughButton(InputNonClientPointerSource nonClientInput, FrameworkElement button, List<RectInt32> rects)
    {
        var xamlRoot = button.XamlRoot;

        if (xamlRoot == null)
            return;

        var scale = xamlRoot.RasterizationScale;

        var position = button.TransformToVisual(null).TransformPoint(new global::Windows.Foundation.Point(0, 0));

        rects.Add(new RectInt32
        {
            X = (int)(position.X * scale),
            Y = (int)(position.Y * scale),
            Width = (int)(button.ActualWidth * scale),
            Height = (int)(button.ActualHeight * scale)
        });
    }

    public async System.Threading.Tasks.Task StartApplicationAsync()
    {
        try
        {
            await _googleTasks.InitializeAsync();
            await SyncListsAsync();
        }
        catch (Exception ex)
        {
            ContentDialog dialog = new ContentDialog
            {
                Title = "GTasks",
                Content = ex.Message,
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot,
                RequestedTheme = ((App)Application.Current).AppTheme
            };

            await dialog.ShowAsync();
        }
    }

    public void SaveWindowState()
    {
        var state = new WindowState
        {
            HubOpen = _hubWasOpen,
            OpenStickyListIds = _openStickyWindows.Keys.ToList()
        };

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_windowStatePath)!);

        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(_windowStatePath, json);
    }

    public WindowState LoadWindowState()
    {
        if (!File.Exists(_windowStatePath))
            return new WindowState();

        try
        {
            var json = File.ReadAllText(_windowStatePath);

            return JsonSerializer.Deserialize<WindowState>(json)
                   ?? new WindowState();
        }
        catch
        {
            return new WindowState();
        }
    }

    private void RefreshFilteredLists(string searchText)
    {
        FilteredLists.Clear();

        if (string.IsNullOrWhiteSpace(searchText))
        {
            foreach (var list in Lists)
                FilteredLists.Add(list);

            return;
        }

        var matches = Lists.Where(list =>
            list.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase));

        foreach (var list in matches)
            FilteredLists.Add(list);
    }

    private async System.Threading.Tasks.Task SyncListsAsync()
    {
        try
        {
            ProgressBar.Visibility = Visibility.Visible;
            var lists = await _googleTasks.GetListsAsync();

            var defaultList = await _googleTasks.GetListAsync("@default");
            _defaultListId = defaultList.Id;

            var listItems = await System.Threading.Tasks.Task.WhenAll(lists.Where(l => !string.IsNullOrEmpty(l.Id)).Select(async list => new TaskListDisplay { List = list, PendingCount = await _googleTasks.GetPendingTaskCountAsync(list.Id) }));

            Lists.Clear();

            // sort by last modified
            listItems = listItems.OrderByDescending(t => t.Updated).ToArray();

            foreach (var item in listItems)
            {
                Lists.Add(item);

                if (_openStickyWindows.TryGetValue(item.Id, out var sticky))
                {
                    // update list name in sticky window
                    await sticky.LoadListNameAsync();
                }
            }

            // refresh all currently open windows
            //foreach (var sticky in _openStickyWindows.Values)
            //{
            //    await sticky.LoadTasksAsync_CalledFromHub();
            //}

            var syncTasks = _openStickyWindows.Values.Select(sticky => sticky.LoadTasksAsync_CalledFromHub());

            await System.Threading.Tasks.Task.WhenAll(syncTasks);

            RefreshFilteredLists(SearchBox.Text);


            //StatusText.Text = "Synced";
            ProgressBar.Visibility = Visibility.Collapsed;
        }
        catch
        {
            //StatusText.Text = "Sync failed";
        }
    }

    private async void AddList(object sender, RoutedEventArgs e)
    {
        HideSettingsOverlay();
        await AddNewListAsync();
    }

    private async System.Threading.Tasks.Task AddNewListAsync()
    {
        var box = new TextBox
        {
            PlaceholderText = "List name",
            AcceptsReturn = false,
            MaxLength = 1024
        };

        var dialog = new ContentDialog
        {
            Title = "Create new list",
            PrimaryButtonText = "Create",
            CloseButtonText = "Cancel",
            Content = box,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot,
            RequestedTheme = ((App)Application.Current).AppTheme
        };

        dialog.PrimaryButtonClick += async (_, _) =>
        {
            var title = box.Text.Trim();

            if (string.IsNullOrWhiteSpace(title))
                return;

            try
            {
                var result = await _googleTasks.AddListAsync(title);

                await SyncListsAsync();

                box.Text = "";


                if (_openStickyWindows.TryGetValue(result.Id, out var existingSticky))
                {
                    existingSticky.Activate();
                    return;
                }
                // navigate to that newly created list
                var sticky = new StickyWindow(_googleTasks, result.Id, this);
                _openStickyWindows[result.Id] = sticky;
                sticky.Activate();
            }
            catch (Exception ex)
            {
                ContentDialog errorDialog = new ContentDialog
                {
                    Title = "Couldn't create list",
                    Content = ex.Message,
                    CloseButtonText = "OK",
                    XamlRoot = Content.XamlRoot,
                    RequestedTheme = ((App)Application.Current).AppTheme
                };

                await errorDialog.ShowAsync();
            }
        };

        await dialog.ShowAsync();
    }

    private void LaunchSettings(object sender, RoutedEventArgs e)
    {
        //SettingsOverlay.Translation = new Vector3((float)ActualWidth, 0, 0);
        // Actually lets just toggle it
        if (SettingsOverlay.Visibility == Visibility.Visible)
        {
            HideSettingsOverlay();
        }
        else
        {
            SettingsOverlay.Visibility = Visibility.Visible;
            SettingsOverlay.Translation = Vector3.Zero;
        }
    }

    public void HideSettingsOverlay()
    {
        SettingsOverlay.Translation = new Vector3(0, 5000, 0);
        SettingsOverlay.Visibility = Visibility.Collapsed;
    }

    private void CloseApp(object sender, RoutedEventArgs e)
    {
        HideSettingsOverlay();
        SetHubOpenState(false);
        this.AppWindow.Hide();
    }

    //public void ShowOrFocusHub()
    //{
    //    // Hub window is already alive
    //    if (this.AppWindow != null)
    //    {
    //        this.Activate();
    //    }
    //}

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
            return;

        string query = sender.Text.Trim();

        RefreshFilteredLists(query);

        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }
    }

    private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        string query = args.QueryText.Trim();

        var list = Lists.FirstOrDefault(x => x.Title.Equals(query, StringComparison.OrdinalIgnoreCase));

        if (list != null)
        {
            OpenOrFocusSticky(list);
        }
    }

    public void OpenOrFocusSticky(TaskListDisplay list)
    {
        if (string.IsNullOrEmpty(list.Id))
            return;

        if (_openStickyWindows.TryGetValue(list.Id, out var existingSticky))
        {
            existingSticky.Activate();
            return;
        }

        var sticky = new StickyWindow(
            _googleTasks,
            list.Id,
            this);

        _openStickyWindows[list.Id] = sticky;

        //sticky.Closed += (s, e) =>
        //{
        //    _openStickyWindows.Remove(list.Id);
        //};
        SaveWindowState();

        sticky.Activate();
    }

    public void OpenOrFocusSticky(string listId)
    {
        if (string.IsNullOrEmpty(listId))
            return;

        var list = Lists.FirstOrDefault(x => x.Id == listId);

        if (list != null)
        {
            OpenOrFocusSticky(list);
        }
    }


    private async void SyncLists(object sender, RoutedEventArgs e)
    {
        HideSettingsOverlay();
        await SyncListsAsync();

        // await RefreshOpenStickyWindowsAsync();
    }

    public async System.Threading.Tasks.Task RefreshListCountAsync(string listId)
    {
        var item = Lists.FirstOrDefault(x => x.Id == listId);


        if (item == null)
            return;

        item.PendingCount =
            await _googleTasks.GetPendingTaskCountAsync(listId);
    }

    private void PopOutList(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is TaskListDisplay list && !string.IsNullOrEmpty(list.Id))
        {
            // is already open? if so, just activate it
            //if (_openStickyWindows.TryGetValue(list.Id, out var existingSticky))
            //{
            //    existingSticky.Activate();
            //    return;
            //}

            //// Create new StickyWindow
            //var sticky = new StickyWindow(
            //    _googleTasks,
            //    list.Id,
            //    this);

            //_openStickyWindows[list.Id] = sticky;
            //sticky.Activate();
            OpenOrFocusSticky(list);
        }
    }
    public void RemoveStickyWindow(string listId)
    {
        _openStickyWindows.Remove(listId);
    }


    private async void RenameList(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is TaskListDisplay list)
        {
            var textBox = new TextBox
            {
                Text = list.Title,
                PlaceholderText = "List name"
            };

            var dialog = new ContentDialog
            {
                Title = "Rename list",
                Content = textBox,
                PrimaryButtonText = "Rename",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = Content.XamlRoot,
                RequestedTheme = ((App)Application.Current).AppTheme
            };

            var result = await dialog.ShowAsync();

            if (result != ContentDialogResult.Primary)
                return;

            var newTitle = textBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(newTitle))
                return;

            if (newTitle == list.Title)
                return;

            try
            {
                await _googleTasks.RenameListAsync(
                    list.Id,
                    newTitle);

                await SyncListsAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Couldn't rename list", ex.Message);
            }
        }
    }

    private async void DeleteList(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is TaskListDisplay list)
        {
            if (list.Id == _defaultListId)
            {
                var dialog = new ContentDialog
                {
                    Title = "Limitation",
                    Content = "Cannot delete default list.",
                    CloseButtonText = "OK",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = Content.XamlRoot,
                    RequestedTheme = ((App)Application.Current).AppTheme
                };

                await dialog.ShowAsync();
            }
            else
            {
                var dialog = new ContentDialog
                {
                    Title = "Delete list?",
                    Content = $"Are you sure you want to delete this list?\n\nAll tasks in this list will also be deleted.",
                    PrimaryButtonText = "Delete",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = Content.XamlRoot,
                    RequestedTheme = ((App)Application.Current).AppTheme
                };

                var result = await dialog.ShowAsync();

                if (result != ContentDialogResult.Primary)
                    return;

                try
                {
                    await _googleTasks.DeleteListAsync(list.Id);

                    // close window if it's open
                    if (_openStickyWindows.TryGetValue(list.Id, out var sticky))
                    {
                        sticky.Close();
                    }

                    await SyncListsAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Couldn't delete list", ex.Message);
                }
            }
        }
    }

    private void ListCollectionItem_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is Button button)
        {
            var colorStrip = FindChildByName<Border>(button, "ColorStrip");

            if (colorStrip != null)
            {
                colorStrip.Opacity = 0;
            }
        }
    }

    private void ListCollectionItem_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is Button button)
        {
            var colorStrip = FindChildByName<Border>(button, "ColorStrip");

            if (colorStrip != null)
            {
                colorStrip.Opacity = 0.8f;
            }
        }
    }

    private T? FindChildByName<T>(DependencyObject parent, string name) where T : FrameworkElement
    {
        if (parent == null)
            return null;

        int childCount = VisualTreeHelper.GetChildrenCount(parent);

        for (int i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            if (child is T element && element.Name == name)
                return element;

            var result = FindChildByName<T>(child, name);

            if (result != null)
                return result;
        }

        return null;
    }

    private async System.Threading.Tasks.Task LoadStartupSettingAsync()
    {
        try
        {
            //_loadingStartupSetting = true;
            var startupTask = await StartupTask.GetAsync("GTasksStartup");
            Debug.WriteLine("On startup: Startup state: ", startupTask.State.ToString());

            StartupToggle.IsOn =
                startupTask.State == StartupTaskState.Enabled ||
                startupTask.State == StartupTaskState.EnabledByPolicy;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Couldn't get startup state: {ex.Message}");

            StartupToggle.IsOn = false;
        }
        //finally { _loadingStartupSetting = false; }
    }

    private async void StartupSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        //if (_loadingStartupSetting)
        //    return;

        try
        {
            var startupTask = await StartupTask.GetAsync("GTasksStartup");

            if (StartupToggle.IsOn)
            {
                var result = await startupTask.RequestEnableAsync();

                if (result != StartupTaskState.Enabled)
                {
                    // Windows refused / user didn't grant permission
                    StartupToggle.IsOn = false;
                    Debug.WriteLine($"Startup enable result: {result}");
                }
                else
                {
                    Debug.WriteLine("Windows/user had allowed, and startup toggle is on");
                }
            }
            else
            {
                Debug.WriteLine("Disabling startup functionality, and toggle is off");
                startupTask.Disable();
            }
        }
        catch (Exception ex)
        {
            StartupToggle.IsOn = false;
            Debug.WriteLine($"Couldn't change startup setting: {ex.Message}");
        }
    }

    public ElementTheme CurrentTheme { get; private set; } = ElementTheme.Default;

    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var theme = ThemeComboBox.SelectedIndex switch
        {
            0 => ElementTheme.Default,
            1 => ElementTheme.Light,
            2 => ElementTheme.Dark,
            _ => ElementTheme.Default
        };

        if (Application.Current is App app)
        {
            app.AppTheme = theme;
        }

        RootGrid.RequestedTheme = theme;

        foreach (var sticky in _openStickyWindows.Values)
        {
            sticky.SetTheme(theme);
        }
    }
}