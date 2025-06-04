using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using app.Models;

namespace app.Services
{
    public interface ITaskDataProvider : IDataProvider
    {
        public IAsyncEnumerable<TaskListModel> GetTaskLists();
        public IAsyncEnumerable<TaskModel> GetTasks();
        public IAsyncEnumerable<TaskModel> GetTasks(string listId);
        public Task<TaskModel> CreateTask(string listId, string title, string body, bool isImportant, DateTimeOffset? completed);
        public Task UpdateTask(TaskModel task);
    }
}
