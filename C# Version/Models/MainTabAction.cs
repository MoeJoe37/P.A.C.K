using System.ComponentModel;

namespace PACK.Models
{
    public class MainTabAction : INotifyPropertyChanged
    {
        public int    X            { get; set; }
        public int    Y            { get; set; }
        public bool   IsLeftClick  { get; set; }
        public bool   IsRightClick { get; set; }
        public bool   IsScroll     { get; set; }

        private bool _hold;
        public bool Hold
        {
            get => _hold;
            set { _hold = value; OnPropertyChanged(nameof(Hold)); }
        }

        public bool IsMouseClick => IsLeftClick || IsRightClick;

        public string DisplayText =>
            IsScroll     ? $"Scroll at ({X}, {Y})" :
            IsLeftClick  ? $"Left Click at ({X}, {Y})" :
                           $"Right Click at ({X}, {Y})";

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
