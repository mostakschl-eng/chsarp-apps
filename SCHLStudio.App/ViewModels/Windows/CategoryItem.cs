using SCHLStudio.App.ViewModels.Base;

namespace SCHLStudio.App.ViewModels.Windows
{
    public class CategoryItem : ViewModelBase
    {
        private string _name = string.Empty;
        private bool _isSelected;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }
}
