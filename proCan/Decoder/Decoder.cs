using CanTraceDecoder.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CanTraceDecoder.Decoder
{
    public class proDecoder
    {
        private readonly List<CanMessageDefinition> _definitions;
        private readonly List<EnumDefinition> _enums;

        public proDecoder(List<CanMessageDefinition> definitions, List<EnumDefinition> enums)
        {
            _definitions = definitions;
            _enums = enums;
        }

        public List<DecodedCanMessage> Decode(List<CanTraceEntry> traceEntries)
        {
            var decodedMessages = new List<DecodedCanMessage>();
            if (!traceEntries.Any()) return decodedMessages;

            // Bestimmen Sie die Startzeit
            double minTimestamp = traceEntries.Min(e => e.Timestamp);

            foreach (var entry in traceEntries)
            {
                var def = _definitions.FirstOrDefault(d => d.CanId == entry.CanId);
                if (def != null)
                {
                    var decoded = new DecodedCanMessage
                    {
                        // Berechne die Zeitdifferenz in Millisekunden seit Start
                        Timestamp = entry.Timestamp - 0,
                        MessageName = def.Name,
                        Description = def.MessageDescription,
                        CanId = def.CanId,
                        Data = entry.Data
                    };

                    foreach (var variable in def.Variables)
                    {
                        var rawValue = ExtractRawValue(entry.Data, variable.StartBit, variable.Length);
                        double value = rawValue;
                        string displayValue = rawValue.ToString();

                        if (!string.IsNullOrEmpty(variable.EnumType))
                        {
                            var enumDef = _enums.FirstOrDefault(e => e.Name.Equals(variable.EnumType, StringComparison.InvariantCultureIgnoreCase));
                            if (enumDef != null && enumDef.Values.ContainsKey((int)rawValue))
                            {
                                displayValue = enumDef.Values[(int)rawValue];
                            }
                        }
                        else
                        {
                            // Zusätzliche Typkonvertierungen können hier hinzugefügt werden
                            if (variable.Type.Equals("bit", StringComparison.InvariantCultureIgnoreCase))
                            {
                                displayValue = rawValue.ToString();
                            }
                            else if (variable.Type.Equals("unsigned", StringComparison.InvariantCultureIgnoreCase))
                            {
                                if (variable.factor != 0.0)
                                {
                                    value = rawValue * variable.factor + variable.offset;
                                    value = Math.Round(value, 2);
                                    displayValue = value.ToString();
                                }
                            }
                            else if (variable.Type.Equals("signed", StringComparison.InvariantCultureIgnoreCase))
                            {
                                if (variable.factor != 0.0)
                                {
                                    value = rawValue * variable.factor + variable.offset;
                                    value = Math.Round(value, 2);
                                    displayValue = value.ToString();
                                }
                            }
                            else
                            {
                                // Fügen Sie hier weitere Typen hinzu
                                displayValue = rawValue.ToString();
                            }
                        }

                        decoded.Signals.Add(new DecodedSignal
                        {
                            Name = variable.Name,
                            Value = value,
                            Description = variable.Description,
                            DisplayValue = displayValue,
                            Unit = variable.unit
                        });
                    }

                    decodedMessages.Add(decoded);
                }
            }

            return decodedMessages;
        }

        private int ExtractRawValue(byte[] data, int startBit, int length)
        {
            int byteIndex = startBit / 8;
            int bitIndex = startBit % 8;
            int rawValue = 0;

            for (int i = 0; i < length; i++)
            {
                if (byteIndex >= data.Length)
                    break;

                int bit = (data[byteIndex] >> bitIndex) & 1;
                rawValue |= bit << i;

                bitIndex++;
                if (bitIndex >= 8)
                {
                    bitIndex = 0;
                    byteIndex++;
                }
            }

            return rawValue;
        }
    }
}