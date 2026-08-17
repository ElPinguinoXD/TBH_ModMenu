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

        if (
            !ModInstaller.EnsureInstalled()
        )
        {
            return;
        }


        // ============================================================
        // NO INTENTAR ACTIVAR EL MOD SOBRE UNA PARTIDA ABIERTA
        // ============================================================

        if (
            ModInstaller.IsGameRunning()
        )
        {
            // Asegurarnos de que el próximo inicio sea vanilla.

            ModInstaller.SetDoorstopEnabled(
                false
            );


            MessageBox.Show(
                "Taskbar Hero ya está abierto.\n\n" +
                "Cierra el juego y después abre TBHModMenu.exe " +
                "para iniciar Taskbar Hero con el mod.",
                "Taskbar Hero Mod",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );


            return;
        }


        // ============================================================
        // ACTIVAR BEPINEX SOLO PARA ESTE INICIO
        // ============================================================

        ModInstaller.SetDoorstopEnabled(
            true
        );


        try
        {

            // ========================================================
            // RESTAURAR VANILLA PARA EL SIGUIENTE INICIO
            // ========================================================

            _ =
                ModInstaller
                    .DisableBepInExAfterGameStartsAsync();


            // ========================================================
            // ABRIR MENU
            // ========================================================

            Application.Run(
                new Form1()
            );
        }
        finally
        {
            // ========================================================
            // SEGURIDAD EXTRA
            // ========================================================

            ModInstaller.SetDoorstopEnabled(
                false
            );
        }
    }
}