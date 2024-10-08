using System.ComponentModel;

namespace CanTraceDecoder.Models
{
    public class DecodedSignal : INotifyPropertyChanged
    {
        private bool _isSelected;
        public string? Name { get; set; }
        public double Value { get; set; }
        public string? Description { get; set; }
        public string DisplayValue { get; set; } // Textuelle Darstellung basierend auf Enum
        public string Unit { get; set; }

        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged("IsSelected");
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class DecodedCanMessage
    {
        public double Timestamp { get; set; } // Relativ in Millisekunden seit Start
        public string? MessageName { get; set; }
        public string? Description { get; set; }
        public byte[]? Data { get; set; }
        public int CanId { get; set; } // CAN-ID als Ganzzahl
        public List<DecodedSignal> Signals { get; set; } = new List<DecodedSignal>();
    }
}