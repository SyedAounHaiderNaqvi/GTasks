using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GTasks.Services
{
    public sealed class SyncManager
    {
        private readonly GoogleTasksService _googleTasks;

        public SyncManager(GoogleTasksService googleTasks)
        {
            _googleTasks = googleTasks;
        }

        public GoogleTasksService GoogleTasks => _googleTasks;
    }
}
