using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GoogleTasks.Models
{
    public sealed class AppSettings
    {
        public bool StartWithWindows { get; set; } = false;

        public bool AlwaysOnTop { get; set; } = true;

        public string Theme { get; set; } = "System";

        public string DefaultAccentColor { get; set; } = "Yellow";

        //public List<StickyWindowState> StickyWindows { get; set; } = [];
    }
}
