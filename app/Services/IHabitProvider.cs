using System.Collections.Generic;
using System.Threading.Tasks;
using app.Models;

namespace app.Services
{
    public interface IHabitProvider
    {
        public IAsyncEnumerable<HabitModel> GetHabits();
        public Task UpdateHabits();
    }
}
