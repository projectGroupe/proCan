using System.Collections.Generic;

namespace CanTraceDecoder.Models
{
    public class CanMessageDefinition
    {
        public string? Name { get; set; }
        public int CanId { get; set; }
        public string? MessageDescription { get; set; }
        public string? IdFormat { get; set; } // z.B. "29Bit" oder "11Bit"
        public int DLC { get; set; }
        public string? Color { get; set; }
        public List<CanVariableDefinition> Variables { get; set; } = new List<CanVariableDefinition>();
    }
}