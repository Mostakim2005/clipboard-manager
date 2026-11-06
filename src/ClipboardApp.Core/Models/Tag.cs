namespace ClipboardApp.Core.Models
{
    public class Tag
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public string Color { get; set; } = "#808080";
    }
}
