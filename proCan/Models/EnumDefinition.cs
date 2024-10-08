namespace CanTraceDecoder.Models
{
    public class EnumDefinition
    {
        public string? Name { get; set; }
        public Dictionary<int, string> Values { get; set; } = new Dictionary<int, string>();
    }
}