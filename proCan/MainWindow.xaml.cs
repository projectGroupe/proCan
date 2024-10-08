using CanTraceDecoder.Decoder;
using CanTraceDecoder.Models;
using CanTraceDecoder.Parsers;
using LiveCharts;
using LiveCharts.Wpf;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace CanTraceDecoder
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private SymParser _symParser;
        private List<CanMessageDefinition> _definitions;
        private List<EnumDefinition> _enums;
        private List<CanTraceEntry> _traceEntries;
        private List<DecodedCanMessage> _decodedMessages;

        public List<DecodedCanMessage> DecodedMessages
        {
            get { return _decodedMessages; }
            set
            {
                _decodedMessages = value;
                OnPropertyChanged("DecodedMessages");
            }
        }

        public Func<double, string> Formatter { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            // Formatter für die X-Achse: relative Zeit in Sekunden
            Formatter = value => $"{value:0.###} s";

            DecodedMessages = new List<DecodedCanMessage>();
            _symParser = new SymParser();
            string filePath = "Resources/2022-04-04_Invenox_CAN_29bit_1v23_extern.sym";
            _symParser.Parse(filePath);
            _definitions = _symParser.Messages;
            _enums = _symParser.Enums;

            MessageBox.Show("SYM-Datei erfolgreich geladen.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void LoadSymButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "SYM files (*.sym)|*.sym|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                _symParser = new SymParser();
                _symParser.Parse(openFileDialog.FileName);
                _definitions = _symParser.Messages;
                _enums = _symParser.Enums;

                MessageBox.Show("SYM-Datei erfolgreich geladen.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void LoadTraceButton_Click(object sender, RoutedEventArgs e)
        {
            if (_definitions == null || !_definitions.Any())
            {
                MessageBox.Show("Bitte laden Sie zuerst eine .sym-Datei.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Trace files (*.trc)|*.trc|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                var traceParser = new TraceParser();
                _traceEntries = traceParser.Parse(openFileDialog.FileName);

                var decoder = new proDecoder(_definitions, _enums);
                _decodedMessages = decoder.Decode(_traceEntries);

                DecodedMessages = _decodedMessages;

                DisplayChart();
            }
        }

        private void ClearChartButton_Click(object sender, RoutedEventArgs e)
        {
            CanChart.Series.Clear();
        }
        private void DisplayChart()
        {
            CanChart.Series.Clear();

            // Beispiel: Visualisierung bestimmter Signale
            // Hier könnten Sie zusätzliche Logik hinzufügen, um nur ausgewählte Signale darzustellen
            // Zum Beispiel alle Signale bestimmter Nachrichten oder anhand einer Auswahl
        }

        // Event-Handler für CheckBox aktiviert
        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            var signal = checkBox.DataContext as DecodedSignal;
            if (signal != null)
            {
                AddSignalToChart(signal);
            }
        }

        // Event-Handler für CheckBox deaktiviert
        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            var signal = checkBox.DataContext as DecodedSignal;
            if (signal != null)
            {
                RemoveSignalFromChart(signal);
            }
        }

        private void AddSignalToChart(DecodedSignal signal)
        {
            // Überprüfen, ob die Serie bereits existiert
            if (CanChart.Series.Any(s => s.Title == signal.Name))
                return;

            var signalData = _decodedMessages
                .SelectMany(m => m.Signals.Where(s => s.Name == signal.Name).Select(s => new { Time = m.Timestamp, SignalValue = s.Value }))
                .OrderBy(d => d.Time)
                .ToList();

            var chartValues = new ChartValues<LiveCharts.Defaults.ObservablePoint>();
            foreach (var dataPoint in signalData)
            {
                chartValues.Add(new LiveCharts.Defaults.ObservablePoint(dataPoint.Time, dataPoint.SignalValue));
            }

            var lineSeries = new LineSeries
            {
                Title = signal.Name,
                Values = chartValues,
                PointGeometry = null, // Entfernt die Punkte aus der Darstellung
                StrokeThickness = 2
            };

            CanChart.Series.Add(lineSeries);

            // Aktualisieren der X-Achse, falls notwendig
            if (signalData.Any())
            {
                var minTime = signalData.Min(d => d.Time);
                var maxTime = signalData.Max(d => d.Time);

                CanChart.AxisX[0].MinValue = Math.Min(CanChart.AxisX[0].MinValue, minTime);
                CanChart.AxisX[0].MaxValue = Math.Max(CanChart.AxisX[0].MaxValue, maxTime);
            }
        }

        private void RemoveSignalFromChart(DecodedSignal signal)
        {
            var series = CanChart.Series.FirstOrDefault(s => s.Title == signal.Name);
            if (series != null)
            {
                CanChart.Series.Remove(series);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}