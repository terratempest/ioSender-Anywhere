using CNC.App;
using ioSender.Services;

namespace ioSender.QuickAccess;

public static class QuickAccessSidebarService
{
    public static QuickAccessSidebarConfig Config
    {
        get
        {
            var cfg = AppHostContext.AppConfig.Base.QuickAccessSidebar;
            if (cfg is null)
            {
                cfg = new QuickAccessSidebarConfig();
                AppHostContext.AppConfig.Base.QuickAccessSidebar = cfg;
            }

            return cfg;
        }
    }

    public static void Persist() => AppHostContext.AppConfig.Save();
}
