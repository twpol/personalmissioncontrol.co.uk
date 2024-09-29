using System.Collections.Generic;
using app.Models;

namespace app.Services
{
    public interface IHabitDataProvider : IDataProvider
    {
        public IAsyncEnumerable<HabitModel> GetHabits();
    }
}
