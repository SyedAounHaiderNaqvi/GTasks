using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Tasks.v1;
using Google.Apis.Tasks.v1.Data;
using Google.Apis.Util.Store;
using GTasks.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GTasks;

public sealed class GoogleTasksService
{
    private static readonly string[] Scopes =
    [
        TasksService.Scope.Tasks
    ];

    private readonly string _dataFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                     "GTasksSticky");

    private TasksService? _service;
    private string? _selectedListId;

    public string? SelectedListId => _selectedListId;

    public async System.Threading.Tasks.Task InitializeAsync()
    {
        Directory.CreateDirectory(_dataFolder);

        var credentialPath = Path.Combine(AppContext.BaseDirectory, "credentials.json");
        if (!File.Exists(credentialPath))
            throw new FileNotFoundException(
                "credentials.json was not found. See README.md for Google OAuth setup.", credentialPath);

        using var stream = new FileStream(credentialPath, FileMode.Open, FileAccess.Read);
        var secrets = GoogleClientSecrets.FromStream(stream).Secrets;

        var tokenStore = new FileDataStore(Path.Combine(_dataFolder, "token"));
        var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            secrets, Scopes, "default", CancellationToken.None, tokenStore);

        _service = new TasksService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "GTasks Sticky"
        });
    }

    public async System.Threading.Tasks.Task<IList<TaskList>> GetListsAsync()
    {
        EnsureReady();
        var result = await _service!.Tasklists.List().ExecuteAsync();
        return result.Items ?? [];
    }

    public async Task<TaskList> GetListAsync(string listId)
    {
        return await _service!.Tasklists
            .Get(listId)
            .ExecuteAsync();
    }

    public async System.Threading.Tasks.Task<IList<TaskItem>> GetTasksAsync(string listId)
    {
        EnsureReady();
        _selectedListId = listId;

        var all = new List<TaskItem>();
        string? pageToken = null;

        do
        {
            var request = _service!.Tasks.List(listId);
            request.ShowCompleted = true;
            request.ShowHidden = true; // change to false if thinks break
            request.MaxResults = 100;
            request.PageToken = pageToken; //will be null on first iteration

            var result = await request.ExecuteAsync();

            foreach (var t in result.Items ?? [])
            {
                all.Add(new TaskItem
                {
                    Id = t.Id ?? "",
                    Title = string.IsNullOrWhiteSpace(t.Title) ? "(Untitled)" : t.Title,
                    Notes = t.Notes,
                    DueDate = DateTime.TryParse(t.Due, out var dueDate) ? dueDate.ToUniversalTime().Date : null,
                    Completed = string.Equals(t.Status, "completed", StringComparison.OrdinalIgnoreCase),
                    CompletedDate = DateTime.TryParse(t.Completed, out var completedDate) ? completedDate.ToUniversalTime().Date : null,
                    Position = t.Position
                });
                // DEBUG
                //Debug.WriteLine($"\n Title: {t.Title} + AssignmentInfo: {t.AssignmentInfo} + Completed: {t.Completed} + Deleted: {t.Deleted} + DueDate: {t.Due} + ETag: {t.ETag} + Hidden: {t.Hidden} + Id: {t.Id} + Kind: {t.Kind}+ Links: {t.Links} + Notes: {t.Notes} + Parent: {t.Parent} + Position: {t.Position} + SelfLink: {t.SelfLink} + Status: {t.Status}+ Updated: {t.Updated} + WebViewLink: {t.WebViewLink}");
            }

            pageToken = result.NextPageToken;
        }
        while (!string.IsNullOrEmpty(pageToken));

        return all;
    }

    public async System.Threading.Tasks.Task<TaskItem?> AddTaskAsync(string listId, string title, string? notes = null, DateTime? due = null)
    {
        EnsureReady();

        var newTask = new Google.Apis.Tasks.v1.Data.Task
        {
            Title = title.Trim(),
            Notes = notes
        };

        if (due.HasValue)
        {
            newTask.Due = due.Value
                .ToUniversalTime()
                .ToString("o");
        }

        var task = await _service!
            .Tasks
            .Insert(newTask, listId)
            .ExecuteAsync();

        return new TaskItem
        {
            Id = task.Id ?? "",
            Title = task.Title ?? title.Trim(),
            Notes = task.Notes,
            DueDate = DateTime.TryParse(
                task.Due,
                out var dueDate)
                    ? dueDate
                    : null,
            CompletedDate = DateTime.TryParse(
                task.Completed,
                out var completedDate)
                    ? completedDate
                    : null,
            Completed = false
        };
    }

    public async System.Threading.Tasks.Task<string> GetListNameAsync(string listId)
    {
        EnsureReady();

        var list = await _service!.Tasklists
            .Get(listId)
            .ExecuteAsync();

        return list.Title ?? "GTasks";
    }

    public async System.Threading.Tasks.Task UpdateTaskAsync(
    string listId,
    TaskItem item,
    string? title = null,
    string? notes = null,
    DateTime? due = null)
    {
        EnsureReady();

        var patch = new Google.Apis.Tasks.v1.Data.Task();

        if (title is not null)
            patch.Title = title;

        if (notes is not null)
            patch.Notes = notes;

        if (due.HasValue)
            patch.Due = due.Value.ToUniversalTime().ToString("o");

        await _service!.Tasks
            .Patch(patch, listId, item.Id)
            .ExecuteAsync();
    }

    public async System.Threading.Tasks.Task RemoveDueDateAsync(
    string listId,
    TaskItem item)
    {
        EnsureReady();

        var patch = new Google.Apis.Tasks.v1.Data.Task
        {
            Due = null
        };

        await _service!.Tasks
            .Patch(patch, listId, item.Id)
            .ExecuteAsync();
    }

    public async Task<TaskList> AddListAsync(string title)
    {
        var newList = new TaskList
        {
            Title = title
        };

        return await _service!.Tasklists
            .Insert(newList)
            .ExecuteAsync();
    }

    public async System.Threading.Tasks.Task RenameListAsync(string listId, string newTitle)
    {
        var taskList = await _service!.Tasklists
            .Get(listId)
            .ExecuteAsync();

        taskList.Title = newTitle;

        await _service.Tasklists
            .Update(taskList, listId)
            .ExecuteAsync();
    }

    public async System.Threading.Tasks.Task DeleteListAsync(string listId)
    {
        await _service!.Tasklists
            .Delete(listId)
            .ExecuteAsync();
    }

    public async System.Threading.Tasks.Task SetCompletedAsync(string listId, TaskItem item, bool completed)
    {
        EnsureReady();

        var patch = new Google.Apis.Tasks.v1.Data.Task
        {
            Status = completed ? "completed" : "needsAction"
        };

        await _service!.Tasks.Patch(patch, listId, item.Id).ExecuteAsync();
    }

    public async System.Threading.Tasks.Task DeleteTaskAsync(string listId, TaskItem item)
    {
        EnsureReady();
        await _service!.Tasks.Delete(listId, item.Id).ExecuteAsync();
    }

    private void EnsureReady()
    {
        if (_service is null)
            throw new InvalidOperationException("Google Tasks has not been initialized.");
    }

    public async Task<int> GetPendingTaskCountAsync(string listID)
    {
        var tasks = await GetTasksAsync(listID);

        int count = 0;

        foreach (var task in tasks)
        {
            if (!task.Completed)
                count++;
        }

        return count;
    }
}
