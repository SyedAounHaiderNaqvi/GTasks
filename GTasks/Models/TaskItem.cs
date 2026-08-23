using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace GTasks;

public sealed class TaskItem : INotifyPropertyChanged
{
    private string _title = "";
    private string? _notes;
    private DateTime? _due;
    private bool _completed;

    // Experimental below:
    private DateTime? _completedDate;
    private string? _position;

    private bool _isStarred;



    // Possible variables (type is from api):
    // Overall status (redundant)- string: "needsAction" or "Completed"
    // Deleted - boolean
    // hidden - boolean (TRUE if completed or list was marked clear)
    // parent - string: Output only apparently.

    public bool IsStarred
    {
        get => _isStarred;
        set
        {
            if (_isStarred == value) return;
            _isStarred = value;
            OnPropertyChanged(nameof(IsStarred));
        }
    }

    public string Id { get; set; } = "";

    public string Title
    {
        get => _title;
        set
        {
            if (_title == value) return;
            _title = value;
            OnPropertyChanged(nameof(Title));
        }
    }

    public string? Position
    {
        get => _position;
        set
        {
            // USE MOVE METHOD GIVEN BY API TO MOVE TASKS
            if (_position == value) return;
            _position = value;
            OnPropertyChanged(nameof(_position));
        }
    }

    public string? Notes
    {
        get => _notes;
        set
        {
            if (_notes == value) return;
            _notes = value;
            OnPropertyChanged(nameof(Notes));
        }
    }

    public DateTime? DueDate
    {
        get => _due;
        set
        {
            if (_due == value) return;
            _due = value;
            OnPropertyChanged(nameof(DueDate));
            OnPropertyChanged(nameof(DueDate_UIFriendlyText));
            OnPropertyChanged(nameof(IsOverdue));
        }
    }

    public string DueDate_UIFriendlyText
    {
        get
        {
            if (!DueDate.HasValue)
                return "";

            var date = DueDate.Value.Date;
            var dateTime = DueDate.Value;
            var today = DateTime.UtcNow.Date; //this is good as long as we dont us its time component for comparison

            var daysAgo = (today - date).Days;

            #region PAST
            if (daysAgo > 0)
            {
                // Less than 1 week ago
                if (daysAgo < 7)
                    return $"{daysAgo} day{(daysAgo == 1 ? "" : "s")} ago";

                // 1–51 weeks ago
                int weeksAgo = daysAgo / 7;

                if (weeksAgo < 52)
                    return $"{weeksAgo} week{(weeksAgo == 1 ? "" : "s")} ago";

                // 1–5 years ago
                int yearsAgo = today.Year - date.Year;

                // Correct for dates where the anniversary hasn't happened yet
                if (date > today.AddYears(-yearsAgo))
                    yearsAgo--;

                if (yearsAgo <= 5)
                    return $"{yearsAgo} year{(yearsAgo == 1 ? "" : "s")} ago";

                // More than 5 years
                return "5+ years ago";
            }
            #endregion

            bool hasTime = dateTime.TimeOfDay != TimeSpan.Zero;
            string timeText = hasTime ? dateTime.ToString("h:mm tt").Replace(":00", "") : "";

            if (date == today)
                return hasTime ? $"Today, {timeText}" : "Today";

            if (date == today.AddDays(1))
                return hasTime ? $"Tomorrow, {timeText}" : "Tomorrow";

            // INDEFINITE FUTURE
            return hasTime ? $"{dateTime:ddd, MMM d}, {timeText}" : dateTime.ToString("ddd, MMM d");
        }
    }

    public DateTime? CompletedDate
    {
        get => _completedDate;
        set
        {
            if (_completedDate == value) return;
            _completedDate = value;
            OnPropertyChanged(nameof(CompletedDate));
            OnPropertyChanged(nameof(CompletedDate_UIFriendlyText));
        }
    }

    public string CompletedDate_UIFriendlyText
    {
        get
        {
            if (!CompletedDate.HasValue)
                return "";

            var date = CompletedDate.Value.Date;

            string formattedText = $"Completed: {date.ToString("ddd, MMM dd", CultureInfo.InvariantCulture)}";

            return formattedText;
        }
    }

    

    public bool IsOverdue =>
        DueDate.HasValue &&
        DueDate.Value.Date < DateTime.Today &&
        !Completed;

    public bool Completed
    {
        get => _completed;
        set
        {
            if (_completed == value) return;
            _completed = value;
            OnPropertyChanged(nameof(Completed));
            OnPropertyChanged(nameof(IsOverdue));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
        => PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
}