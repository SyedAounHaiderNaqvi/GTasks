using GoogleTasks.Models;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using Windows.Graphics;
using Windows.System;
using Windows.UI;
using System.Diagnostics;
using System.Collections.Specialized;

namespace GoogleTasks;

public sealed partial class StickyWindow : Window
{
    // The GoogleTasksService instance is used to interact with the Google Tasks API.
    private readonly GoogleTasksService _googleTasks;

    private ContentDialog? _openDialog;

    // ObservableCollections are used to hold the tasks and completed tasks for data binding in the UI.
    public ObservableCollection<TaskItem> Tasks { get; } = new();
    public ObservableCollection<TaskItem> CompletedTasks { get; } = new();

    // The list ID is used to identify which Google Tasks list this window is displaying. Used for fetching tasks and performing operations on the correct list.
    private readonly string _listId;

    // The MainWindow instance is used to communicate back to the main application window, for example, to refresh the list count or remove this sticky window when it is closed.
    private readonly MainWindow _mainWindow;

    private bool _userClosed = false;

    public StickyWindow(GoogleTasksService googleTasks, string listId, MainWindow mainWindow)
    {
        InitializeComponent();

        if (Application.Current is App app &&
        Content is FrameworkElement root)
        {
            root.RequestedTheme = app.AppTheme;
        }

        #region Initialize singletons
        _googleTasks = googleTasks;
        _listId = listId;
        _mainWindow = mainWindow;
        #endregion

        #region Setting Custom Titlebar
        var presenter = AppWindow.Presenter as OverlappedPresenter;  // Get the presenter for the current window
        presenter?.SetBorderAndTitleBar(true, false);  // Set the window to have a border and no title bar
        DragRegion.Loaded += DragRegion_Loaded;  // Attach an event handler to the Loaded event of the DragRegion to set up the draggable area for the custom title bar
        var appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(WinRT.Interop.WindowNative.GetWindowHandle(this))); // Get the AppWindow instance for the current window
        appWindow.Resize(new global::Windows.Graphics.SizeInt32(600, 700));  // Resize the window to a specific size (600x700 pixels)

        // specifiying the minimum size of the window
        if (presenter != null)
        {
            presenter.PreferredMinimumWidth = 600;
            presenter.PreferredMinimumHeight = 700;
        }

        this.SizeChanged += StickyWindow_SizeChanged;  // Attach an event handler to the SizeChanged event of the window to handle resizing and adjust the draggable area accordingly
        AppWindow.Changed += AppWindow_Changed;  // Attach an event handler to the Changed event of the AppWindow to handle changes in the window's state (e.g., size, presenter) and adjust the draggable area accordingly

        #endregion

        // Attach an event handler to the CollectionChanged event of the CompletedTasks collection to update the visibility of the completed tasks expander based on whether there are any completed tasks
        CompletedTasks.CollectionChanged += CompletedTasks_CollectionChanged;

        // Attach an event handler to the Closed event of the window to notify the main window to remove this sticky window when it is closed
        Closed += StickyWindow_Closed;

        this.Activated += StickyWindow_Activated;

        _ = LoadListNameAsync();
        _ = LoadTasksAsync(_listId);

    }

    private void StickyWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            CloseOpenDialog();
        }
    }

    #region Methods for handling window changes and resizing

    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidPresenterChange || args.DidSizeChange)
        {
            // SetTitleBar(DragRegion);
            SetupDragRegion();
        }
    }

    private void StickyWindow_SizeChanged(object sender, WindowSizeChangedEventArgs args)
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
        AddPassthroughButton(nonClientInput, MenuButton, passthroughRects);
        AddPassthroughButton(nonClientInput, StickyHeader, passthroughRects);
        AddPassthroughButton(nonClientInput, HomeButton, passthroughRects);

        nonClientInput.SetRegionRects(NonClientRegionKind.Passthrough, passthroughRects.ToArray());
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

    #endregion

    #region OnAppClose methods

    private void StickyWindow_Closed(object sender, WindowEventArgs args)
    {
        _mainWindow.RemoveStickyWindow(_listId);

        if(_userClosed)
            _mainWindow.SaveWindowState();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _userClosed = true;
        Close();
    }

    #endregion

    #region Update UI on Visibility of Completed Tasks
    private void CompletedTasks_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        CompletedExpander.Visibility = CompletedTasks.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }
    #endregion

    #region Load Tasks and List Name

    public async System.Threading.Tasks.Task LoadListNameAsync()
    {
        try
        {
            ListTitle.Text = await _googleTasks.GetListNameAsync(_listId);

            // set the title of the window to the list name
            AppWindow.Title = ListTitle.Text;

            await _mainWindow.RefreshListCountAsync(_listId);
        }
        catch
        {
            ListTitle.Text = "Google Tasks";
        }
    }

    private async System.Threading.Tasks.Task LoadTasksAsync(string listID)
    {
        try
        {
            // for the specific window-list-ID, fetch the tasks
            var tasks = await _googleTasks.GetTasksAsync(listID);

            // clear the existing in-app list(not the actual list) # the loop just works, else checkbox state is glitchy
            foreach (var task in Tasks)
            {
                task.Completed = false;
            }
            foreach (var task in CompletedTasks)
            {
                task.Completed = true;
            }

            Tasks.Clear();
            CompletedTasks.Clear();

            // fetch all the active tasks
            var activeTasks = tasks.Where(t => !t.Completed).OrderBy(t => t.Position ?? string.Empty, StringComparer.Ordinal).ToList();

            // fetch all the completed tasks
            var completedTasks = tasks.Where(t => t.Completed).OrderByDescending(t => t.CompletedDate).ToList();


            // add each relevant task to its own observablecollection (better optimization to just
            // add if-else within loop and do different buckets)

            foreach (var task in activeTasks)
            {
                Tasks.Add(task);
            }

            foreach (var task in completedTasks)
            {
                CompletedTasks.Add(task);
            }

            await _mainWindow.RefreshListCountAsync(_listId);

            StatusText.Text = "Sync successful";
        }
        catch (Exception ex)
        {
            Tasks.Clear();
            CompletedTasks.Clear();

            Tasks.Add(new TaskItem() { Title = ex.Message, Id = "", Completed = false });
            CompletedTasks.Add(new TaskItem() { Title = ex.Message, Id = "", Completed = true });

            StatusText.Text = "Sync failed";
        }
    }

    #endregion

    #region TITLE BUTTONS

    private void OpenHubWindow(object sender, RoutedEventArgs e)
    {
        _mainWindow.AppWindow.Show();
        _mainWindow.HideSettingsOverlay();
        _mainWindow.Activate();
        _mainWindow.Sync_CalledFromStickyWindow();
        _mainWindow.SetHubOpenState(true);
    }

    //private void ColorSwatch_Click(object sender, RoutedEventArgs e)
    //{
    //    if (sender is not Button button)
    //        return;

    //    var color = button.Tag.ToString();

    //    byte[] argb = color.Split(',').Select(x => byte.Parse(x)).ToArray();


    //    // Change title bar
    //    DragRegion.Background =
    //        new SolidColorBrush(global::Windows.UI.Color.FromArgb(argb[0], argb[1], argb[2], argb[3]));

    //    // TODO: persist this color
    //}

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadTasksAsync(_listId);
    }

    public async System.Threading.Tasks.Task LoadTasksAsync_CalledFromHub()
    {
        await LoadTasksAsync(_listId);
    }

    #endregion

    #region Task Completion Methods

    private async System.Threading.Tasks.Task CompleteTaskAsync(TaskItem task, bool isCompleted)
    {
        try
        {
            await _googleTasks.SetCompletedAsync(_listId, task, isCompleted);
            await LoadTasksAsync(_listId);
        }
        catch
        {
        }
    }

    private async void TaskCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox && checkBox.DataContext is TaskItem task)
        {
            checkBox.IsChecked = false;
            var temp = task;
            Tasks.Remove(task);
            await CompleteTaskAsync(task, true);
        }
    }

    #endregion

    private async System.Threading.Tasks.Task DeleteTaskAsync(TaskItem task)
    {
        try
        {
            await _googleTasks.DeleteTaskAsync(_listId, task);
            await LoadTasksAsync(_listId);
        }
        catch
        {
        }
    }


    #region Creating new task

    private async void AddTaskButton_Click(object sender, RoutedEventArgs e)
    {
        await ShowAddTaskDialogAsync();  // Show the dialog to add a new task when the AddTaskButton is clicked
    }

    private async System.Threading.Tasks.Task ShowAddTaskDialogAsync()
    {
        var titleBox = new TextBox { PlaceholderText = "Task name", AcceptsReturn = false, MaxLength = 1024 };

        var detailsBox = new TextBox { PlaceholderText = "Details (optional)", AcceptsReturn = false, TextWrapping = TextWrapping.Wrap, MaxLength = 8192 };

        var dueDatePicker = new CalendarDatePicker { PlaceholderText = "DueDate date (optional)" };

        var panel = new StackPanel { Spacing = 12 };

        panel.Children.Add(titleBox);
        panel.Children.Add(detailsBox);
        panel.Children.Add(dueDatePicker);

        var dialog = new ContentDialog
        {
            Title = "New task",
            Content = panel,

            PrimaryButtonText = "Create",
            CloseButtonText = "Cancel",

            DefaultButton = ContentDialogButton.Primary,

            XamlRoot = Content.XamlRoot,
            RequestedTheme = ((App)Application.Current).AppTheme,
        };

        _openDialog = dialog;

        dialog.PrimaryButtonClick += async (_, _) =>
        {
            var title = titleBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(title))
            {
                // Don't allow an empty task
                return;
            }

            string? notes = string.IsNullOrWhiteSpace(detailsBox.Text) ? null : detailsBox.Text.Trim();

            DateTime? due = null;

            if (dueDatePicker.Date.HasValue)
            {
                due = dueDatePicker.Date.Value.DateTime;
            }

            try
            {
                await _googleTasks.AddTaskAsync(_listId, title, notes, due);
                await LoadTasksAsync(_listId);
            }
            catch
            {
                // Add any error handling
            }
        };

        try
        {
            await dialog.ShowAsync();
            titleBox.Focus(FocusState.Programmatic);
        }
        finally
        {
            _openDialog = null;
        }
    }

    private void CloseOpenDialog()
    {
        _openDialog?.Hide();
        _openDialog = null;
    }

    #endregion

    public void SetTheme(ElementTheme theme)
    {
        if (Content is FrameworkElement root)
            root.RequestedTheme = theme;
    }
}