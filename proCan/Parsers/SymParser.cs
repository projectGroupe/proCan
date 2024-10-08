using CanTraceDecoder.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace CanTraceDecoder.Parsers
{
    public class SymParser
    {
        public List<EnumDefinition> Enums { get; private set; } = new List<EnumDefinition>();
        public List<CanMessageDefinition> Messages { get; private set; } = new List<CanMessageDefinition>();

        public void Parse(string symFilePath)
        {
            var lines = File.ReadAllLines(symFilePath);
            EnumDefinition currentEnum = null;
            CanMessageDefinition currentMessage = null;
            bool inEnum = false;
            string enumBuffer = "";

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();

                // Überspringe leere Linien und Kommentarzeilen
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//") || line.StartsWith("#"))
                    continue;

                // Erkennung des Enum-Abschnitts
                if (line.StartsWith("enum", StringComparison.InvariantCultureIgnoreCase) && !inEnum)
                {
                    inEnum = true;
                    enumBuffer += line;
                    // Prüfen, ob das Enum in dieser Zeile abgeschlossen ist
                    if (line.Contains(")"))
                    {
                        inEnum = false;
                        currentEnum = ParseEnum(enumBuffer);
                        if (currentEnum != null)
                            Enums.Add(currentEnum);
                        enumBuffer = "";
                    }
                    continue;
                }

                if (inEnum)
                {
                    enumBuffer += " " + line;
                    if (line.Contains(")"))
                    {
                        inEnum = false;
                        currentEnum = ParseEnum(enumBuffer);
                        if (currentEnum != null)
                            Enums.Add(currentEnum);
                        enumBuffer = "";
                    }
                    continue;
                }

                // Erkennung des Nachrichten-Abschnitts
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    if (currentMessage != null)
                    {
                        Messages.Add(currentMessage);
                    }

                    var messageName = line.Substring(1, line.Length - 2);
                    currentMessage = new CanMessageDefinition { Name = messageName };
                    continue;
                }

                if (currentMessage != null)
                {
                    // Parsing von Eigenschaften innerhalb einer Nachricht
                    if (line.StartsWith("ID=", StringComparison.InvariantCultureIgnoreCase))
                    {
                        var idMatch = Regex.Match(line, @"ID=([0-9A-Fa-f]+)h?");
                        if (idMatch.Success)
                        {
                            string idStr = idMatch.Groups[1].Value;
                            if (idStr.EndsWith("h", StringComparison.InvariantCultureIgnoreCase))
                                idStr = idStr.Substring(0, idStr.Length - 1);
                            currentMessage.CanId = Convert.ToInt32(idStr, 16);
                            currentMessage.IdFormat = ExtractIdFormat(lines);
                        }
                        var discrMatch = Regex.Match(line, @"//.*$");
                        if (discrMatch.Success)
                        {
                            string desStr = discrMatch.Groups[0].Value;
                            currentMessage.MessageDescription = desStr.Substring(3);
                        }
                        continue;
                    }

                    if (line.StartsWith("DLC=", StringComparison.InvariantCultureIgnoreCase))
                    {
                        var dlcMatch = Regex.Match(line, @"DLC=(\d+)");
                        if (dlcMatch.Success)
                        {
                            currentMessage.DLC = int.Parse(dlcMatch.Groups[1].Value);
                        }
                        continue;
                    }

                    if (line.StartsWith("Color=", StringComparison.InvariantCultureIgnoreCase))
                    {
                        var colorMatch = Regex.Match(line, @"Color=([0-9A-Fa-f]+)h?");
                        if (colorMatch.Success)
                        {
                            currentMessage.Color = colorMatch.Groups[1].Value;
                        }
                        continue;
                    }

                    // Parsing von Variablen
                    if (line.StartsWith("Var=", StringComparison.InvariantCultureIgnoreCase))
                    {
                        // Beispiel: Var=Bat0_FAT_OVER_U bit 0,1	// Over Voltage
                        string varPattern = @"Var=(\w+)\s+(unsigned|signed|bit)\s+(\d+),(\d+)(?:\s+/u:(\S+))?(?:\s+/f:(\d+\.\d+))?(?:\s+/o:(-?\d+))?(?:\s*//\s*(.+))?";
                        var varMatch = Regex.Match(line, varPattern);
                        if (varMatch.Success)
                        {
                            var variable = new CanVariableDefinition
                            {
                                Name = varMatch.Groups[1].Value.Trim(),
                                Type = varMatch.Groups[2].Value.Trim(),
                                StartBit = int.Parse(varMatch.Groups[3].Value),
                                Length = int.Parse(varMatch.Groups[4].Value),
                                unit = varMatch.Groups[5].Success ? varMatch.Groups[5].Value : "No unit",
                                factor = varMatch.Groups[6].Success ? double.Parse(varMatch.Groups[6].Value, CultureInfo.InvariantCulture) : 1.0,
                                offset = varMatch.Groups[7].Success ? int.Parse(varMatch.Groups[7].Value) : 0,
                                Description = varMatch.Groups[8].Success ? varMatch.Groups[8].Value.Trim() : string.Empty
                            };

                            // Verbinde mit Enum, wenn möglich
                            // Annahme: Wenn Variable und Enum denselben Namen haben
                            var potentialEnum = Enums.FirstOrDefault(e => e.Name == variable.Name);
                            if (potentialEnum != null)
                            {
                                variable.EnumType = potentialEnum.Name;
                            }

                            currentMessage.Variables.Add(variable);
                        }
                        continue;
                    }
                }
            }

            // Füge die letzte Nachricht hinzu
            if (currentMessage != null)
            {
                Messages.Add(currentMessage);
            }
        }

        private EnumDefinition ParseEnum(string enumText)
        {
            // Beispiel: enum VtSig_BMS_State(0="ERROR", 1="INIT", 2="ON", 3="SHUTDOWN", 4="CHARGE", 5="DRIVE", 6="PRECHARGE", 7="POSTDISCHARGE", 8="OFF", 9="SLEEP", 12="DCDC", 13="FASTCHARGE", 14="SLAVEADDRESSING")
            var enumMatch = Regex.Match(enumText, @"enum\s+(\w+)\s*\((.*)\)");
            if (enumMatch.Success)
            {
                var enumDef = new EnumDefinition
                {
                    Name = enumMatch.Groups[1].Value.Trim()
                };

                var valuesPart = enumMatch.Groups[2].Value;
                ParseEnumValues(valuesPart, enumDef);
                return enumDef;
            }

            return null;
        }

        private void ParseEnumValues(string valuesPart, EnumDefinition enumDef)
        {
            // Entferne überflüssige Leerzeichen und Zeilenumbrüche
            valuesPart = Regex.Replace(valuesPart, @"\s+", " ");

            // Split nach Kommas außerhalb von Anführungszeichen
            var values = SplitEnumValues(valuesPart);

            foreach (var value in values)
            {
                // Match für Schlüssel-Wert-Paare wie 0="ERROR"
                var pairMatch = Regex.Match(value.Trim(), @"(\d+)\s*=\s*""([^""]+)""");
                if (pairMatch.Success)
                {
                    int key = int.Parse(pairMatch.Groups[1].Value);
                    string val = pairMatch.Groups[2].Value;
                    enumDef.Values[key] = val;
                }
                else
                {
                    // Unterstützung für einzelne Werte ohne Anführungszeichen
                    var singleMatch = Regex.Match(value.Trim(), @"(\d+)\s*=\s*(\S+)");
                    if (singleMatch.Success)
                    {
                        int key = int.Parse(singleMatch.Groups[1].Value);
                        string val = singleMatch.Groups[2].Value.Trim('"');
                        enumDef.Values[key] = val;
                    }
                }
            }
        }

        private List<string> SplitEnumValues(string valuesPart)
        {
            var values = new List<string>();
            int lastSplit = 0;
            bool inQuotes = false;

            for (int i = 0; i < valuesPart.Length; i++)
            {
                if (valuesPart[i] == '"')
                    inQuotes = !inQuotes;

                if (valuesPart[i] == ',' && !inQuotes)
                {
                    values.Add(valuesPart.Substring(lastSplit, i - lastSplit));
                    lastSplit = i + 1;
                }
            }

            // Füge den letzten Abschnitt hinzu
            if (lastSplit < valuesPart.Length)
            {
                values.Add(valuesPart.Substring(lastSplit));
            }

            return values;
        }

        private string ExtractIdFormat(string[] lines)
        {
            // Analysiere die Title-Zeile oder andere relevante Informationen, um das ID-Format zu bestimmen
            // Beispiel: Title="18-04-26_Invenox_CAN_29Bit_intern_ 1v13"
            foreach (var line in lines)
            {
                if (line.StartsWith("Title=", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (line.Contains("29Bit"))
                        return "29Bit";
                    else
                        return "11Bit";
                }
            }
            // Standardwert
            return "11Bit";
        }
    }
}