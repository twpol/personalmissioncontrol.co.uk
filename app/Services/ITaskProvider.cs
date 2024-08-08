using System.Collections.Generic;
using app.Models;

namespace app.Services
{
    public interface ITaskProvider
    {
        public IAsyncEnumerable<TaskListModel> GetTaskLists();
        public IAsyncEnumerable<TaskModel> GetTasks();
        public IAsyncEnumerable<TaskModel> GetTasks(string listId);
    }
}
