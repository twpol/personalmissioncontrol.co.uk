using System.Collections.Generic;
using app.Models;

namespace app.Services
{
    public interface IHabitProvider
    {
        public IAsyncEnumerable<HabitModel> GetHabits();
    }
}
