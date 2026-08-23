using Google.Apis.Tasks.v1.Data;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GoogleTasks.Models
{
    public class TaskListDisplay : INotifyPropertyChanged
    {
        public TaskList List { get; set; }

        public string Title => List.Title;

        public string Id => List.Id;

        public string Updated => List.Updated;

        private int _pendingCount;

        public int PendingCount
        {
            get => _pendingCount;
            set
            {
                if (_pendingCount == value)
                    return;

                _pendingCount = value;

                PropertyChanged?.Invoke(
                    this,
                    new PropertyChangedEventArgs(nameof(PendingCount)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
