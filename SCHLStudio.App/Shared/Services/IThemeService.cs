namespace SCHLStudio.App.Services
{
    public enum AppTheme
    {
        Dark,
        Light
    }

    public interface IThemeService
    {
        AppTheme CurrentTheme { get; }
        void Initialize();
        void SetUserTheme(AppTheme theme);
    }
}
