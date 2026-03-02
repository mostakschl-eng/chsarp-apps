namespace SCHLStudio.App.ViewModels.Windows
{
    public sealed class AppShellContext
    {
        public AppShellContext(string currentUser, string currentRole)
        {
            CurrentUser = currentUser;
            CurrentRole = currentRole;
        }

        public string CurrentUser { get; }
        public string CurrentDisplayName => Configuration.AppConfig.CurrentDisplayName;
        public string CurrentRole { get; }
    }
}
