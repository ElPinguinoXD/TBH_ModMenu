using System.Threading;

namespace TBHModMenu;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();


        // ========================================================
        // INSTALL / STARTUP SCREEN
        // ========================================================

        using InstallProgressForm installer =
            new InstallProgressForm();


        installer.Show();


        Application.DoEvents();


        // ========================================================
        // INSTALAR / VERIFICAR ARCHIVOS
        // ========================================================

        bool installed =
            ModInstaller.EnsureInstalled(
                (
                    message,
                    percent
                ) =>
                {
                    installer.SetProgress(
                        message,
                        percent
                    );


                    Application.DoEvents();
                }
            );


        if (!installed)
        {
            installer.Close();

            return;
        }


        // ========================================================
        // EL JUEGO NO DEBE ESTAR YA ABIERTO
        // ========================================================

        if (
            ModInstaller.IsGameRunning()
        )
        {
            ModInstaller.SetDoorstopEnabled(
                false
            );


            installer.Close();


            MessageBox.Show(
                "Taskbar Hero ya está abierto.\n\n" +
                "Ciérralo, abre primero TBHModMenu.exe " +
                "y luego abre Taskbar Hero desde Steam.",
                "Taskbar Hero Mod",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );


            return;
        }


        // ========================================================
        // PREPARAR BEPINEX PARA ESTE INICIO
        // ========================================================

        ModInstaller.SetDoorstopEnabled(
            true
        );


        installer.SetProgress(
            "Instalación completa. Abre Taskbar Hero desde Steam.",
            0
        );


        Application.DoEvents();


        // ========================================================
        // ESPERAR JUEGO + BEPINEX + PLUGIN
        // ========================================================

        bool modReady =
            false;


        try
        {
            Task<bool> waitTask =
                ModInstaller.WaitForModReadyAsync(
                    (
                        message,
                        percent
                    ) =>
                    {
                        installer.SetProgress(
                            message,
                            percent
                        );
                    }
                );


            // ====================================================
            // MANTENER WINFORMS RESPONSIVO MIENTRAS ESPERAMOS
            // ====================================================

            while (
                !waitTask.IsCompleted
            )
            {
                Application.DoEvents();

                Thread.Sleep(
                    15
                );
            }


            modReady =
                waitTask
                    .GetAwaiter()
                    .GetResult();
        }
        catch (Exception ex)
        {
            ModInstaller.SetDoorstopEnabled(
                false
            );


            installer.Close();


            MessageBox.Show(
                "Ocurrió un error durante la carga del mod:\n\n" +
                ex.Message,
                "Taskbar Hero Mod",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );


            return;
        }


        // ========================================================
        // NO CARGÓ
        // ========================================================

        if (!modReady)
        {
            installer.Close();

            return;
        }


        // ========================================================
        // MOD ACTIVO
        // ========================================================

        installer.SetProgress(
            "✓ MOD ACTIVO",
            100
        );


        Application.DoEvents();


        Thread.Sleep(
            900
        );


        installer.Close();


        // ========================================================
        // ABRIR MENU PRINCIPAL
        // ========================================================

        try
        {
            Application.Run(
                new Form1()
            );
        }
        finally
        {
            // Próximo inicio directamente desde Steam:
            // vanilla.

            ModInstaller.SetDoorstopEnabled(
                false
            );
        }
    }
}