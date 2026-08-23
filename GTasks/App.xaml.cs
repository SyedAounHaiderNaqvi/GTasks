using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.Windows.AppLifecycle;
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

namespace GTasks
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
            var startupTask = await StartupTask.GetAsync("GTasksStartup");

            await startupTask.RequestEnableAsync();
        }

        private async Task DisableStartupAsync()
        {
            var startupTask = await StartupTask.GetAsync("GTasksStartup");

            startupTask.Disable();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            await LaunchApplicationAsync(args);
        }

        private async Task LaunchApplicationAsync(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            // If user didnt open anything prev session, then this session if app autostarts dont open any windows!
            AppActivationArguments lifecycleArgs = Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs();

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

            if (!state.HubOpen && state.OpenStickyListIds.Count == 0)
            {
                if (lifecycleArgs.Kind == ExtendedActivationKind.Launch)
                {
                    // User launched, show window
                    _window.Activate();
                }
                else
                {
                    // Close app now!
                    _window.Close();
                    Application.Current.Exit();
                }
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
