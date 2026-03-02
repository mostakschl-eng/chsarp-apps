using System;
using System.Windows;
using SCHLStudio.App.Services.Api;
using SCHLStudio.App.ViewModels.Search;

namespace SCHLStudio.App.Views.Search
{
    public partial class SearchView
    {
        public SearchView()
        {
            InitializeComponent();
            IApiClient? api = null;
            try
            {
                api = (System.Windows.Application.Current as SCHLStudio.App.App)
                    ?.ServiceProvider
                    ?.GetService(typeof(IApiClient)) as IApiClient;
            }
            catch
            {
                api = null;
            }

            DataContext = new SearchViewModel(api);
        }
    }
}
