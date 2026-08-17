using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

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
        // NO CONTINUAR SI EL JUEGO YA ESTÁ ABIERTO
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
        // PREPARAR MOD
        // ========================================================

        ModInstaller.SetDoorstopEnabled(
            true
        );


        installer.SetProgress(
            "Instalación completa. Abre Taskbar Hero desde Steam.",
            0
        );


        // Ahora sí permitimos desinstalar.

        installer.ShowUninstallButton(
            true
        );


        Application.DoEvents();


        // ========================================================
        // CANCELACIÓN DEL MONITOR
        // ========================================================

        using CancellationTokenSource startupCancellation =
            new CancellationTokenSource();


        bool uninstalling =
            false;


        // ========================================================
        // UNINSTALL DESDE LA PANTALLA DE ESPERA
        // ========================================================

        installer.UninstallRequested +=
            (_, _) =>
            {
                if (uninstalling)
                {
                    return;
                }


                DialogResult confirmation =
                    MessageBox.Show(
                        "¿Quieres eliminar el Mod Menu de la " +
                        "instalación de Taskbar Hero?",
                        "Desinstalar mod",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning
                    );


                if (
                    confirmation !=
                    DialogResult.Yes
                )
                {
                    return;
                }


                uninstalling =
                    true;


                installer.ShowUninstallButton(
                    false
                );


                installer.SetProgress(
                    "Preparando desinstalación...",
                    0
                );


                Application.DoEvents();


                // Detener la espera de Taskbar Hero.

                startupCancellation.Cancel();


                // Importantísimo:
                // apagar Doorstop antes de borrar archivos.

                ModInstaller.SetDoorstopEnabled(
                    false
                );


                bool removed =
                    ModInstaller.UninstallMod();


                if (removed)
                {
                    installer.SetProgress(
                        "Mod eliminado correctamente.",
                        100
                    );


                    Application.DoEvents();


                    Thread.Sleep(
                        700
                    );


                    installer.Close();
                }
                else
                {
                    uninstalling =
                        false;


                    installer.SetProgress(
                        "Desinstalación cancelada.",
                        0
                    );


                    installer.ShowUninstallButton(
                        true
                    );
                }
            };


        // ========================================================
        // ESPERAR GAME + BEPINEX + PLUGIN
        // ========================================================

        Task<bool> waitTask =
            ModInstaller.WaitForModReadyAsync(
                (
                    message,
                    percent
                ) =>
                {
                    if (
                        startupCancellation
                            .IsCancellationRequested
                    )
                    {
                        return;
                    }


                    // Cuando el juego aparece ya no permitimos
                    // desinstalar archivos mientras están cargados.

                    if (
                        ModInstaller.IsGameRunning()
                    )
                    {
                        installer.ShowUninstallButton(
                            false
                        );
                    }


                    installer.SetProgress(
                        message,
                        percent
                    );
                },

                startupCancellation.Token
            );


        // ========================================================
        // MANTENER WINFORMS RESPONSIVO
        // ========================================================

        while (
            !waitTask.IsCompleted &&
            !uninstalling
        )
        {
            Application.DoEvents();


            Thread.Sleep(
                15
            );
        }


        // ========================================================
        // DESINSTALÓ EL MOD
        // ========================================================

        if (uninstalling)
        {
            return;
        }


        // ========================================================
        // RESULTADO
        // ========================================================

        bool modReady;


        try
        {
            modReady =
                waitTask
                    .GetAwaiter()
                    .GetResult();
        }
        catch (
            OperationCanceledException
        )
        {
            return;
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


        if (!modReady)
        {
            ModInstaller.SetDoorstopEnabled(
                false
            );


            installer.Close();

            return;
        }


        // ========================================================
        // MOD ACTIVO
        // ========================================================

        installer.ShowUninstallButton(
            false
        );


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
        // ABRIR MENU NORMAL
        // ========================================================

        try
        {
            Application.Run(
                new Form1()
            );
        }
        finally
        {
            ModInstaller.SetDoorstopEnabled(
                false
            );
        }
    }
}