using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GoogleTasks
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private MainWindow? _window;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();
        }

        private async Task EnableStartupAsync()
        {
            var startupTask = await StartupTask.GetAsync("GoogleTasksStartup");

            await startupTask.RequestEnableAsync();
        }

        private async Task DisableStartupAsync()
        {
            var startupTask = await StartupTask.GetAsync("GoogleTasksStartup");

            startupTask.Disable();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            await LaunchApplicationAsync();
        }

        private async Task LaunchApplicationAsync()
        {
            _window = new MainWindow();

            await _window.StartApplicationAsync();

            var state = _window.LoadWindowState();

            if (state.HubOpen)
            {
                _window.Activate();
            }

            foreach (var listId in state.OpenStickyListIds)
            {
                _window.OpenOrFocusSticky(listId);
            }

            if (!state.HubOpen &&
                state.OpenStickyListIds.Count == 0)
            {
                _window.Activate();
            }
        }

        public ElementTheme AppTheme { get; set; } = ElementTheme.Default;

        public void ApplyThemeToWindow(Window window)
        {
            if (window.Content is FrameworkElement root)
            {
                root.RequestedTheme = AppTheme;
            }
        }
    }
}
