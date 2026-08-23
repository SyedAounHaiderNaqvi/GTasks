using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace GTasks.Services
{
    public sealed class StartupService
    {
        private const string RunKey =
            @"Software\Microsoft\Windows\CurrentVersion\Run";

        private const string AppName = "GTasks";

        public bool IsEnabled()
        {
            using var key =
                Registry.CurrentUser.OpenSubKey(RunKey);

            return key?.GetValue(AppName) is not null;
        }

        public void SetEnabled(bool enabled)
        {
            using var key =
                Registry.CurrentUser.OpenSubKey(
                    RunKey,
                    writable: true);

            if (key is null)
                return;

            if (enabled)
            {
                key.SetValue(
                    AppName,
                    Environment.ProcessPath ?? "");
            }
            else
            {
                key.DeleteValue(AppName, false);
            }
        }
    }
}
