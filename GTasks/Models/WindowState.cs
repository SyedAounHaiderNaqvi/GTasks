using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GTasks.Models
{
    public class WindowState
    {
        public bool HubOpen { get; set; }

        public List<string> OpenStickyListIds { get; set; } = new();
    }
}
