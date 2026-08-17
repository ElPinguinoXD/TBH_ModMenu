using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Windows.Forms;

using Microsoft.Win32;

namespace TBHModMenu;

internal static class ModInstaller
{
    private const string SteamAppId =
        "3719740";

    private const string GameExe =
        "TaskBarHero.exe";

    private const string ModDirectory =
        @"C:\TBH_ModMenu";

    private const string PayloadResourceName =
        "TBHModMenu.Payload.zip";


    // ============================================================
    // MAIN INSTALL
    // ============================================================

    public static bool EnsureInstalled()
    {
        try
        {
            Directory.CreateDirectory(
                ModDirectory
            );

            EnsureConfigFiles();


            string? gameDirectory =
                FindGameDirectory();


            // ====================================================
            // NO SE ENCONTRÓ AUTOMÁTICAMENTE
            // ====================================================

            if (gameDirectory == null)
            {
                using FolderBrowserDialog dialog =
                    new FolderBrowserDialog();

                dialog.Description =
                    "Selecciona la carpeta de Taskbar Hero";

                dialog.UseDescriptionForTitle =
                    true;


                if (
                    dialog.ShowDialog() !=
                    DialogResult.OK
                )
                {
                    return false;
                }


                gameDirectory =
                    dialog.SelectedPath;
            }


            string gameExePath =
                Path.Combine(
                    gameDirectory,
                    GameExe
                );


            if (!File.Exists(gameExePath))
            {
                MessageBox.Show(
                    "No se encontró TaskBarHero.exe en:\n\n" +
                    gameDirectory +
                    "\n\nSelecciona la carpeta correcta del juego.",
                    "Taskbar Hero Mod",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );

                return false;
            }


            // ====================================================
            // GUARDAR RUTA
            // ====================================================

            File.WriteAllText(
                Path.Combine(
                    ModDirectory,
                    "gamepath.txt"
                ),
                gameDirectory
            );


            bool gameRunning =
                Process
                    .GetProcessesByName(
                        "TaskBarHero"
                    )
                    .Length > 0;


            string pluginPath =
                Path.Combine(
                    gameDirectory,
                    "BepInEx",
                    "plugins",
                    "TBHPlugin.dll"
                );


            // ====================================================
            // SI EL JUEGO ESTÁ ABIERTO
            // ====================================================

            if (gameRunning)
            {
                // Si ya está instalado, dejamos abrir el menú.

                if (File.Exists(pluginPath))
                {
                    return true;
                }


                MessageBox.Show(
                    "Taskbar Hero está abierto y el plugin todavía " +
                    "no está instalado.\n\n" +
                    "Cierra el juego y vuelve a abrir este programa.",
                    "Taskbar Hero Mod",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );

                return false;
            }


            // ====================================================
            // INSTALAR / ACTUALIZAR PAYLOAD
            // ====================================================

            ExtractPayload(
                gameDirectory
            );


            // ====================================================
            // VERIFICAR
            // ====================================================

            if (!File.Exists(pluginPath))
            {
                throw new FileNotFoundException(
                    "La instalación terminó pero no apareció TBHPlugin.dll.",
                    pluginPath
                );
            }


            return true;
        }
        catch (UnauthorizedAccessException)
        {
            MessageBox.Show(
                "Windows bloqueó la escritura en la carpeta del juego.\n\n" +
                "Ejecuta TBHModMenu como administrador.",
                "Permisos insuficientes",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );

            return false;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "No se pudo instalar el mod:\n\n" +
                ex.Message,
                "Taskbar Hero Mod",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );

            return false;
        }
    }


    // ============================================================
    // CONFIG FILES
    // ============================================================

    private static void EnsureConfigFiles()
    {
        (string Name, string Value)[] files =
        {
            ("gamespeed.txt", "1.0"),

            ("attackdamage.txt", "50"),

            ("attackspeed.txt", "1.56"),

            ("movementspeed.txt", "8.5"),

            ("godmode.txt", "0"),

            ("moneymultiplier.txt", "1.0"),

            ("hero_command.txt", ""),

            ("damage_command.txt", ""),

            ("attackspeed_command.txt", ""),

            ("movementspeed_command.txt", "")
        };


        foreach (
            (string name, string value)
            in files
        )
        {
            string path =
                Path.Combine(
                    ModDirectory,
                    name
                );


            if (!File.Exists(path))
            {
                File.WriteAllText(
                    path,
                    value
                );
            }
        }
    }


    // ============================================================
    // EXTRACT EMBEDDED PAYLOAD
    // ============================================================

    private static void ExtractPayload(
        string gameDirectory
    )
    {
        Assembly assembly =
            Assembly.GetExecutingAssembly();


        string? resourceName =
            assembly
                .GetManifestResourceNames()
                .FirstOrDefault(
                    name =>
                        name.EndsWith(
                            "Payload.zip",
                            StringComparison.OrdinalIgnoreCase
                        )
                );


        if (resourceName == null)
        {
            throw new InvalidOperationException(
                "No se encontró Payload.zip dentro del ejecutable."
            );
        }


        using Stream? stream =
            assembly.GetManifestResourceStream(
                resourceName
            );


        if (stream == null)
        {
            throw new InvalidOperationException(
                "No se pudo abrir Payload.zip."
            );
        }


        using ZipArchive archive =
            new ZipArchive(
                stream,
                ZipArchiveMode.Read
            );


        string root =
            Path.GetFullPath(
                gameDirectory
            );


        string rootWithSeparator =
            root.EndsWith(
                Path.DirectorySeparatorChar
            )
                ? root
                : root +
                  Path.DirectorySeparatorChar;


        foreach (
            ZipArchiveEntry entry
            in archive.Entries
        )
        {
            string relativePath =
                entry.FullName.Replace(
                    '/',
                    Path.DirectorySeparatorChar
                );


            string destination =
                Path.GetFullPath(
                    Path.Combine(
                        root,
                        relativePath
                    )
                );


            // ====================================================
            // PROTECCIÓN ZIP SLIP
            // ====================================================

            if (
                !destination.StartsWith(
                    rootWithSeparator,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                throw new InvalidOperationException(
                    "Payload contiene una ruta inválida."
                );
            }


            // ====================================================
            // DIRECTORY
            // ====================================================

            if (
                string.IsNullOrEmpty(
                    entry.Name
                )
            )
            {
                Directory.CreateDirectory(
                    destination
                );

                continue;
            }


            string? directory =
                Path.GetDirectoryName(
                    destination
                );


            if (directory != null)
            {
                Directory.CreateDirectory(
                    directory
                );
            }


            entry.ExtractToFile(
                destination,
                true
            );
        }
    }


    // ============================================================
    // FIND TASKBAR HERO
    // ============================================================

    private static string? FindGameDirectory()
    {
        string? result;


        // ========================================================
        // STEAM UNINSTALL REGISTRY
        // ========================================================

        result =
            FindFromUninstallRegistry();

        if (IsGameDirectory(result))
            return result;


        // ========================================================
        // STEAM LIBRARIES
        // ========================================================

        result =
            FindFromSteamLibraries();

        if (IsGameDirectory(result))
            return result;


        // ========================================================
        // DEFAULT PATH
        // ========================================================

        string defaultPath =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder
                        .ProgramFilesX86
                ),
                "Steam",
                "steamapps",
                "common",
                "TaskbarHero"
            );


        if (IsGameDirectory(defaultPath))
        {
            return defaultPath;
        }


        return null;
    }


    private static bool IsGameDirectory(
        string? path
    )
    {
        if (
            string.IsNullOrWhiteSpace(
                path
            )
        )
        {
            return false;
        }


        return File.Exists(
            Path.Combine(
                path,
                GameExe
            )
        );
    }


    // ============================================================
    // REGISTRY
    // ============================================================

    private static string? FindFromUninstallRegistry()
    {
        RegistryView[] views =
        {
            RegistryView.Registry64,
            RegistryView.Registry32
        };


        foreach (
            RegistryView view
            in views
        )
        {
            try
            {
                using RegistryKey baseKey =
                    RegistryKey.OpenBaseKey(
                        RegistryHive.LocalMachine,
                        view
                    );


                using RegistryKey? key =
                    baseKey.OpenSubKey(
                        @"SOFTWARE\Microsoft\Windows\" +
                        @"CurrentVersion\Uninstall\" +
                        $"Steam App {SteamAppId}"
                    );


                string? installLocation =
                    key?.GetValue(
                        "InstallLocation"
                    ) as string;


                if (
                    IsGameDirectory(
                        installLocation
                    )
                )
                {
                    return installLocation;
                }
            }
            catch
            {
            }
        }


        return null;
    }


    // ============================================================
    // STEAM LIBRARYFOLDERS.VDF
    // ============================================================

    private static string? FindFromSteamLibraries()
    {
        try
        {
            using RegistryKey? steamKey =
                Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Valve\Steam"
                );


            string? steamPath =
                steamKey?.GetValue(
                    "SteamPath"
                ) as string;


            if (
                string.IsNullOrWhiteSpace(
                    steamPath
                )
            )
            {
                return null;
            }


            steamPath =
                steamPath.Replace(
                    '/',
                    '\\'
                );


            // ====================================================
            // MAIN LIBRARY
            // ====================================================

            string mainCandidate =
                Path.Combine(
                    steamPath,
                    "steamapps",
                    "common",
                    "TaskbarHero"
                );


            if (
                IsGameDirectory(
                    mainCandidate
                )
            )
            {
                return mainCandidate;
            }


            // ====================================================
            // EXTRA LIBRARIES
            // ====================================================

            string libraryFile =
                Path.Combine(
                    steamPath,
                    "steamapps",
                    "libraryfolders.vdf"
                );


            if (!File.Exists(libraryFile))
                return null;


            string text =
                File.ReadAllText(
                    libraryFile
                );


            MatchCollection matches =
                Regex.Matches(
                    text,
                    "\"path\"\\s+\"([^\"]+)\"",
                    RegexOptions.IgnoreCase
                );


            foreach (
                Match match
                in matches
            )
            {
                string libraryPath =
                    match.Groups[1]
                        .Value
                        .Replace(
                            @"\\",
                            @"\"
                        );


                string candidate =
                    Path.Combine(
                        libraryPath,
                        "steamapps",
                        "common",
                        "TaskbarHero"
                    );


                if (
                    IsGameDirectory(
                        candidate
                    )
                )
                {
                    return candidate;
                }
            }
        }
        catch
        {
        }


        return null;
    }

    // ============================================================
// GAME RUNNING
// ============================================================

public static bool IsGameRunning()
{
    try
    {
        Process[] processes =
            Process.GetProcessesByName(
                "TaskBarHero"
            );

        bool running =
            processes.Length > 0;

        foreach (
            Process process
            in processes
        )
        {
            process.Dispose();
        }

        return running;
    }
    catch
    {
        return false;
    }
}


// ============================================================
// ENABLE / DISABLE BEPINEX
// ============================================================

public static void SetDoorstopEnabled(
    bool enabled
)
{
    try
    {
        string? gameDirectory =
            null;


        string savedPath =
            Path.Combine(
                ModDirectory,
                "gamepath.txt"
            );


        // ====================================================
        // RUTA GUARDADA
        // ====================================================

        if (
            File.Exists(
                savedPath
            )
        )
        {
            string candidate =
                File
                    .ReadAllText(
                        savedPath
                    )
                    .Trim();


            if (
                IsGameDirectory(
                    candidate
                )
            )
            {
                gameDirectory =
                    candidate;
            }
        }


        // ====================================================
        // BUSCAR STEAM
        // ====================================================

        if (gameDirectory == null)
        {
            gameDirectory =
                FindGameDirectory();
        }


        if (gameDirectory == null)
        {
            return;
        }


        string configPath =
            Path.Combine(
                gameDirectory,
                "doorstop_config.ini"
            );


        if (!File.Exists(configPath))
        {
            return;
        }


        string text =
            File.ReadAllText(
                configPath
            );


        string value =
            enabled
                ? "true"
                : "false";


        Regex regex =
            new Regex(
                @"^(\s*enabled\s*=\s*).*$",
                RegexOptions.IgnoreCase |
                RegexOptions.Multiline
            );


        if (
            regex.IsMatch(
                text
            )
        )
        {
            text =
                regex.Replace(
                    text,
                    "${1}" + value,
                    1
                );
        }
        else
        {
            text =
                "[UnityDoorstop]" +
                Environment.NewLine +
                "enabled=" +
                value +
                Environment.NewLine +
                Environment.NewLine +
                text;
        }


        File.WriteAllText(
            configPath,
            text
        );
    }
    catch (Exception ex)
    {
        MessageBox.Show(
            "No se pudo cambiar el estado de BepInEx:\n\n" +
            ex.Message,
            "Taskbar Hero Mod",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning
        );
    }
}


// ============================================================
// RESET BEPINEX AFTER LAUNCH
// ============================================================

public static async Task
    DisableBepInExAfterGameStartsAsync()
{
    try
    {
        // ====================================================
        // ESPERAR HASTA QUE EL USUARIO ABRA EL JUEGO
        // ====================================================

        while (
            !IsGameRunning()
        )
        {
            await Task.Delay(
                250
            );
        }


        // ====================================================
        // DAR TIEMPO A DOORSTOP / BEPINEX PARA CARGAR
        // ====================================================

        await Task.Delay(
            10000
        );


        // ====================================================
        // EL SIGUIENTE INICIO SERÁ VANILLA
        // ====================================================

        SetDoorstopEnabled(
            false
        );
    }
    catch
    {
        SetDoorstopEnabled(
            false
        );
    }
}

    // ============================================================
    // LAUNCH GAME
    // ============================================================

    public static void LaunchGameIfNeeded()
    {
        try
        {
            if (
                Process
                    .GetProcessesByName(
                        "TaskBarHero"
                    )
                    .Length > 0
            )
            {
                return;
            }


            Process.Start(
                new ProcessStartInfo
                {
                    FileName =
                        $"steam://rungameid/{SteamAppId}",

                    UseShellExecute =
                        true
                }
            );
        }
        catch
        {
        }
    }
}