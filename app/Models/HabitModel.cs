using System;

namespace app.Models
{
    public record HabitModel(string Id, string Title)
    {
        public string Classes => "habit";
    }
}
