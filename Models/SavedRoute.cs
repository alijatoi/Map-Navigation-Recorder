using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MauiApp1.Models
{
    internal class SavedRoute
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "New Route";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public List<GpsPoint> Points { get; set; } = new();
    }
}
