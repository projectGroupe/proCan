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
			var extension = Path.GetExtension(traceFilePath).ToLowerInvariant();
			if (extension == ".txt")
			{
				return ParseTxt(traceFilePath);
			}
			// default to existing .trc behavior
			return ParseTrc(traceFilePath);
		}

		private List<CanTraceEntry> ParseTrc(string traceFilePath)
		{
			var entries = new List<CanTraceEntry>();
			DateTime startTime = GetStartTime(traceFilePath);

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

		private List<CanTraceEntry> ParseTxt(string traceFilePath)
		{
			var entries = new List<CanTraceEntry>();
			DateTime? firstTimestamp = null;

			foreach (var rawLine in File.ReadAllLines(traceFilePath))
			{
				var line = rawLine.Trim();
				if (string.IsNullOrWhiteSpace(line))
					continue;

				// Skip header line if present
				if (line.StartsWith("Timestamp;", StringComparison.InvariantCultureIgnoreCase))
					continue;

				var parts = line.Split(';');
				if (parts.Length < 4)
					continue;

				string timestampToken = parts[0].Trim();
				string typeToken = parts[1].Trim(); // 0 = standard, 1 = extended (not used here)
				string idToken = parts[2].Trim();
				string dataToken = parts[3].Trim();

				try
				{
					if (!TryParseTxtTimestamp(timestampToken, out DateTime absoluteTime))
						continue;

					if (!firstTimestamp.HasValue)
						firstTimestamp = absoluteTime;

					double relativeMs = (absoluteTime - firstTimestamp.Value).TotalMilliseconds;

					// Parse CAN ID as hex (supports decimal-looking hex as well)
					if (idToken.StartsWith("0x", StringComparison.InvariantCultureIgnoreCase))
						idToken = idToken.Substring(2);
					int canId = Convert.ToInt32(idToken, 16);

					// Parse data as continuous hex string
					var hex = dataToken.Replace(" ", string.Empty);
					if (hex.Length % 2 != 0)
						continue;
					var dataBytes = HexStringToByteArray(hex);

					entries.Add(new CanTraceEntry
					{
						Timestamp = relativeMs,
						CanId = canId,
						Data = dataBytes
					});
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Fehler beim Parsen der Zeile (TXT): {line}. Fehler: {ex.Message}");
				}
			}

			return entries;
		}

		private bool TryParseTxtTimestamp(string token, out DateTime timestamp)
		{
			timestamp = default;
			if (string.IsNullOrWhiteSpace(token))
				return false;

			int tIndex = token.IndexOf('T');
			if (tIndex <= 0)
				return false;

			string dayPart = token.Substring(0, tIndex);
			string timePart = token.Substring(tIndex + 1);

			if (!int.TryParse(dayPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out int day) || day <= 0)
				return false;

			// Expect HHMMSSmmm (e.g., 130858662 => 13:08:58.662)
			if (timePart.Length < 6)
				return false;

			int hours = int.Parse(timePart.Substring(0, 2), CultureInfo.InvariantCulture);
			int minutes = int.Parse(timePart.Substring(2, 2), CultureInfo.InvariantCulture);
			int seconds = int.Parse(timePart.Substring(4, 2), CultureInfo.InvariantCulture);
			int milliseconds = 0;
			if (timePart.Length > 6)
			{
				string msPart = timePart.Substring(6);
				// normalize to 3 digits
				if (msPart.Length > 3) msPart = msPart.Substring(0, 3);
				if (msPart.Length < 3) msPart = msPart.PadRight(3, '0');
				milliseconds = int.Parse(msPart, CultureInfo.InvariantCulture);
			}

			// Construct a synthetic date. Day part is treated as day-of-month.
			// Using a fixed month/year is sufficient because we only compute relative differences.
			timestamp = new DateTime(2000, 1, 1, hours, minutes, seconds, milliseconds).AddDays(day - 1);
			return true;
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