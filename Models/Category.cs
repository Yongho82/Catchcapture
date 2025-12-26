using System;

namespace CatchCapture.Models
{
    public class Category
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = "#8E2DE2"; // Default purple
    }
}
