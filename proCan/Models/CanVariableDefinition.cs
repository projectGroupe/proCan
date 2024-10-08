namespace CanTraceDecoder.Models
{
    public class CanVariableDefinition
    {
        public string? Name { get; set; }
        public string? Type { get; set; } // z.B. "bit", "unsigned"
        public int StartBit { get; set; }
        public int Length { get; set; }
        public string unit { get; set; }
        public double factor { get; set; }
        public int offset { get; set; }
        public string? EnumType { get; set; } // Referenz auf ein Enum, falls vorhanden
        public string? Description { get; set; } // Kommentar oder Beschreibung

        // Für Anzeige als Parameter
        public bool IsSelected { get; set; } // Zum Steuern der Auswahl im UI
    }
}