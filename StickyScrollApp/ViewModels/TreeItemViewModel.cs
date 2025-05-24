using System.Collections.ObjectModel;
using System.ComponentModel;

namespace StickyScrollApp.ViewModels
{
    public abstract class TreeItemViewModel : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public ObservableCollection<TreeItemViewModel> Children { get; } = new ObservableCollection<TreeItemViewModel>();

        private bool _isExpanded = true;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public override string ToString()
        {
            return this.Name;
        }
    }

    public class FolderViewModel : TreeItemViewModel { }
    public class FileViewModel : TreeItemViewModel { }
}