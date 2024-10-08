using CanTraceDecoder.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace CanTraceDecoder.Parsers
{
    public class TraceParser
    {
        public List<CanTraceEntry> Parse(string traceFilePath)
        {
            var entries = new List<CanTraceEntry>();
            DateTime startTime = GetStartTime(traceFilePath);
            bool isHeader = true;

            foreach (var rawLine in File.ReadAllLines(traceFilePath))
            {
                var line = rawLine.Trim();

                // Überspringe leere Linien und Kommentare
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith(";"))
                    continue;

                // Parsing der Trace-Daten
                // Beispielzeile:
                // 1)        57.5  Rx         03A8  8  D1 FD 00 00 00 00 00 00 

                var parts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 6)
                    continue; // Ungültige Zeile

                try
                {
                    // Nachrichtennummer (ignoriert)
                    // parts[0] = "1)"
                    // Time Offset in ms
                    double timeOffsetMs = double.Parse(parts[1], CultureInfo.InvariantCulture);
                    // Typ (Rx/Tx)
                    string type = parts[2];
                    // ID (hex)
                    string idHex = parts[3].Trim().TrimEnd('h', 'H');
                    int canId = Convert.ToInt32(idHex, 16);
                    // DLC
                    int dlc = int.Parse(parts[4]);
                    // Datenbytes
                    var dataBytes = new byte[dlc];
                    for (int i = 0; i < dlc; i++)
                    {
                        dataBytes[i] = Convert.ToByte(parts[5 + i], 16);
                    }

                    // Berechnung des absoluten Timestamps
                    // Zeitdifferenz seit Startzeit
                    double relativeTimeMs = timeOffsetMs;
                    double relativeTimeSeconds = relativeTimeMs / 1000.0;

                    var entry = new CanTraceEntry
                    {
                        Timestamp = relativeTimeMs, // Relativ in Millisekunden
                        CanId = canId,
                        Data = dataBytes
                    };

                    entries.Add(entry);
                }
                catch (Exception ex)
                {
                    // Log Fehler oder ignoriere fehlerhafte Zeilen
                    Console.WriteLine($"Fehler beim Parsen der Zeile: {line}. Fehler: {ex.Message}");
                }
            }

            return entries;
        }

        private DateTime GetStartTime(string traceFilePath)
        {
            // Lese die Startzeit aus der Trace-Datei
            // Beispielzeile:
            // ;   Start time: 10/4/2024 07:46:46.189.0

            foreach (var line in File.ReadAllLines(traceFilePath))
            {
                if (line.StartsWith(";   Start time:", StringComparison.InvariantCultureIgnoreCase))
                {
                    var parts = line.Split(new char[] { ':' }, 2);
                    if (parts.Length == 2)
                    {
                        var dateTimeStr = parts[1].Trim().TrimEnd('.', '0');
                        if (DateTime.TryParseExact(dateTimeStr, "M/d/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime startTime))
                        {
                            return startTime;
                        }
                        else if (DateTime.TryParse(dateTimeStr, out DateTime parsedTime))
                        {
                            return parsedTime;
                        }
                    }
                }
            }

            // Fallback:
            return DateTime.Now;
        }

        private byte[] HexStringToByteArray(string hex)
        {
            int numberChars = hex.Length;
            byte[] bytes = new byte[numberChars / 2];
            for (int i = 0; i < numberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }
    }
}