namespace TBHModMenu;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();


        // ========================================================
        // INSTALAR / ACTUALIZAR MOD
        // ========================================================

        if (!ModInstaller.EnsureInstalled())
        {
            return;
        }


        // ========================================================
        // ABRIR TASKBAR HERO
        // ========================================================

        ModInstaller.LaunchGameIfNeeded();


        // ========================================================
        // ABRIR MOD MENU
        // ========================================================

        Application.Run(
            new Form1()
        );
    }
}