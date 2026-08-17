using System;
using System.Globalization;
using System.IO;
using System.Collections.Generic;

using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

using HarmonyLib;

using UnityEngine;

using TaskbarHero;
using TaskbarHero.EasySaveData;

using System.Reflection;

namespace TBHPlugin;


// ============================================================================
// PLUGIN
// ============================================================================

[BepInPlugin(
    "pengx.taskbarhero.plugin",
    "TBH Plugin",
    "0.3.2"
)]
public class Plugin : BasePlugin
{
    private Harmony harmony;

    public static ManualLogSource PluginLog { get; private set; }

    public override void Load()
{
    PluginLog = Log;

    Log.LogInfo("=================================");
    Log.LogInfo(" TBH Plugin v0.3.2");
    Log.LogInfo(" Plugin cargando...");
    Log.LogInfo("=================================");

    // ============================================================
    // CARGAR PRIMERO EL CONTROLLER
    //
    // Así Game Speed, F4, F7 y comandos del menú funcionan
    // incluso si algún parche Harmony da error.
    // ============================================================

    try
    {
        AddComponent<SpeedController>();

        Log.LogInfo(
            "[TBH] SpeedController cargado correctamente."
        );
    }
    catch (Exception ex)
    {
        Log.LogError(
            $"[TBH] ERROR cargando SpeedController:\n{ex}"
        );
    }


    // ============================================================
    // HARMONY
    // ============================================================

    try
    {
        harmony = new Harmony(
            "com.pengx.taskbarhero.mod"
        );

        harmony.PatchAll();

        Log.LogInfo(
            "[TBH] Harmony patches cargados correctamente."
        );
    }
    catch (Exception ex)
    {
        Log.LogError(
            $"[TBH] HARMONY ERROR:\n{ex}"
        );

        // IMPORTANTE:
        // No lanzamos nuevamente la excepción.
        // El resto del plugin debe seguir funcionando.
    }


    Log.LogInfo("=================================");
    Log.LogInfo(" TBH Plugin listo.");
    Log.LogInfo("=================================");
}


// ============================================================================
// ESTADO GLOBAL DEL MOD
// ============================================================================

internal static class ModState
{
    // ================================================================
    // GOD MODE
    // ================================================================

    public static bool GodModeEnabled = false;

    // ================================================================
    // ATTACK DAMAGE OVERRIDE
    // ================================================================

    public static bool AttackDamageEnabled = true;

    public static float AttackDamage = 50f;


    // ================================================================
    // ATTACK SPEED
    // ================================================================

    public static bool AttackSpeedEnabled = false;

    public static float AttackSpeed = 1.56f;

    public static int AttackSpeedHeroId = -1;

    public static int AttackSpeedHeroInstanceId = int.MinValue;


    // ================================================================
    // MOVEMENT SPEED
    // ================================================================

    public static bool MovementSpeedEnabled = false;

    public static float MovementSpeed = 8.5f;

    public static int MovementSpeedHeroId = -1;

    public static int MovementSpeedHeroInstanceId = int.MinValue;

    // ================================================================
    // MOVEMENT SPEED POR HERO
    // ================================================================

    public static readonly Dictionary<int, float>
        MovementSpeedByHero =
            new Dictionary<int, float>();

    public static readonly Dictionary<int, float>
        MovementSpeedByInstance =
            new Dictionary<int, float>();

    public static readonly Dictionary<int, int>
        MovementInstanceByHero =
            new Dictionary<int, int>();

    // ================================================================
    // ATTACK DAMAGE HELPER
    //
    // AttackSpeed y MovementSpeed se aplican directamente sobre
    // Hero.bsqu y Unit.bsrq para no afectar a otros héroes.
    // ================================================================

    public static void ApplyAttackDamage(
        StatType stat,
        ref float result
    )
    {
        if (!AttackDamageEnabled)
            return;

        if (stat != StatType.AttackDamage)
            return;

        result = AttackDamage;
    }

    // ================================================================
    // MONEY MULTIPLIER
    // ================================================================

    public static float MoneyMultiplier = 1.0f;

}


// ============================================================================
// MAIN CONTROLLER
// ============================================================================

public class SpeedController : MonoBehaviour
{
    // ========================================================================
    // ARCHIVOS
    // ========================================================================

    private const string ModDirectory =
        @"C:\TBH_ModMenu";

    private const string SpeedFile =
        @"C:\TBH_ModMenu\gamespeed.txt";

    private const string HeroCommandFile =
        @"C:\TBH_ModMenu\hero_command.txt";

    private const string AttackDamageFile =
        @"C:\TBH_ModMenu\attackdamage.txt";

    private const string DamageCommandFile =
        @"C:\TBH_ModMenu\damage_command.txt";

    private string lastDamageCommand = "";

    private float damageCommandTimer = 0f;

    private const float DamageCommandInterval = 0.10f;

    private const string AttackSpeedFile =
    @"C:\TBH_ModMenu\attackspeed.txt";

    private const string AttackSpeedCommandFile =
        @"C:\TBH_ModMenu\attackspeed_command.txt";

    private const string MovementSpeedFile =
        @"C:\TBH_ModMenu\movementspeed.txt";

    private const string MovementSpeedCommandFile =
        @"C:\TBH_ModMenu\movementspeed_command.txt";

    private string lastAttackSpeedCommand = "";
    private string lastMovementSpeedCommand = "";

    private float speedCommandTimer = 0f;

    private const float SpeedCommandInterval = 0.10f;

    private float heroSpeedTargetTimer = 0f;

    private const float HeroSpeedTargetInterval = 0.50f;

    private readonly Dictionary<int, Vector3>
        lastHeroPositions =
            new Dictionary<int, Vector3>();

    private readonly HashSet<int>
        initializedMovementHeroes =
            new HashSet<int>();

    // 8.5 fue el MovementSpeed real/base que encontre.
    private const float NativeMovementSpeed = 8.5f;

    private const string GodModeFile =
    @"C:\TBH_ModMenu\godmode.txt";

    private float godModeReadTimer = 0f;

    private const float GodModeReadInterval = 0.10f;

    private const string MoneyMultiplierFile =
    @"C:\TBH_ModMenu\moneymultiplier.txt";

    private float moneyMultiplierReadTimer = 0f;

    private const float MoneyMultiplierReadInterval = 0.10f;

    // ========================================================================
    // GAME SPEED
    // ========================================================================

    private float desiredSpeed = 1.0f;

    private float speedReadTimer = 0f;

    private const float SpeedReadInterval = 0.10f;


    // ========================================================================
    // ATTACK DAMAGE
    // ========================================================================

    private float damageReadTimer = 0f;

    private const float DamageReadInterval = 0.10f;


    // ========================================================================
    // HERO COMMAND
    // ========================================================================

    private string lastHeroCommand = "";

    private float heroCommandTimer = 0f;

    private const float HeroCommandInterval = 0.15f;


    // ========================================================================
    // HERO DEBUG
    // ========================================================================

    private bool heroesDumped = false;

    private float heroDumpTimer = 0f;

    private const float HeroDumpRetryInterval = 2.0f;


    // ========================================================================
    // IL2CPP CONSTRUCTOR
    // ========================================================================

    public SpeedController(IntPtr ptr)
        : base(ptr)
    {
    }


    // ========================================================================
    // START
    // ========================================================================

    private void Start()
    {
        try
        {
            Directory.CreateDirectory(
                ModDirectory
            );

             // ------------------------------------------------------------
            // GOD MODE
            // ------------------------------------------------------------


            if (!File.Exists(GodModeFile))
            {
                File.WriteAllText(
                    GodModeFile,
                    "0"
                );
            }

            ReadGodMode();

            // ------------------------------------------------------------
            // MONEY MULTIPLIER
            // ------------------------------------------------------------

            if (!File.Exists(MoneyMultiplierFile))
            {
                File.WriteAllText(
                    MoneyMultiplierFile,
                    "1.0"
                );
            }

            ReadMoneyMultiplier();
            // ------------------------------------------------------------
            // SPEED FILE
            // ------------------------------------------------------------

            if (!File.Exists(SpeedFile))
            {
                File.WriteAllText(
                    SpeedFile,
                    "1.0"
                );
            }

            // ------------------------------------------------------------
            // DAMAGE FILE
            // ------------------------------------------------------------

            if (!File.Exists(AttackDamageFile))
            {
                File.WriteAllText(
                    AttackDamageFile,
                    "50"
                );
            }

            // ------------------------------------------------------------
            // ATTACK SPEED / MOVEMENT SPEED FILES
            // ------------------------------------------------------------

            if (!File.Exists(AttackSpeedFile))
            {
                File.WriteAllText(
                    AttackSpeedFile,
                    "1.56"
                );
            }

            if (!File.Exists(MovementSpeedFile))
            {
                File.WriteAllText(
                    MovementSpeedFile,
                    "8.5"
                );
            }

            ReadDesiredSpeed();

            ReadDesiredAttackDamage();

            Plugin.PluginLog?.LogInfo(
                $"[TBH] Attack Damage inicial: " +
                $"{ModState.AttackDamage}"
            );
        }
        catch (Exception ex)
        {
            Plugin.PluginLog?.LogWarning(
                $"[TBH] Start ERROR: {ex.Message}"
            );
        }
    }


    // ========================================================================
    // UPDATE
    // ========================================================================

    private void Update()
    {
        // ------------------------------------------------------------
        // MOD SYSTEMS
        // ------------------------------------------------------------

        UpdateGameSpeed();

        UpdateAttackDamage();

        UpdateHeroCommand();

        UpdateDamageCommand();

        UpdateHeroSpeedCommands();

        UpdateHeroSpeedTargets();

        TryDumpHeroes();

        UpdateGodMode();

        MaintainGodModeHealth();

        UpdateMoneyMultiplier();

        // ------------------------------------------------------------
        // F5 = DAMAGE DEBUG
        // ------------------------------------------------------------

        if (Input.GetKeyDown(KeyCode.F5))
        {
            Plugin.PluginLog?.LogInfo(
                "[TBH] F5 detectado."
            );

            DumpAttackDamageStats();
        }


        // ------------------------------------------------------------
        // F7 = HERO RUNTIME DEBUG
        // ------------------------------------------------------------

        if (Input.GetKeyDown(KeyCode.F7))
        {
            Plugin.PluginLog?.LogInfo(
                "[TBH] F7 detectado."
            );

            TestHeroRuntime(201);
            TestHeroRuntime(101);
        }

        if (Input.GetKeyDown(KeyCode.F4))
            {
                Plugin.PluginLog?.LogInfo(
                    "[TBH] F4 detectado."
                );

                DumpHeroDamageCandidates(201);
            }

        if (Input.GetKeyDown(KeyCode.F3))
        {
            Plugin.PluginLog?.LogInfo(
                "[TBH] F3 detectado."
            );

            DumpSpeedStats(201);
        }

        if (Input.GetKeyDown(KeyCode.F2))
        {
            Plugin.PluginLog?.LogInfo(
                "[TBH] F2 detectado."
            );

            DumpMovementSpeed(201);
        }
    }

    


    // ========================================================================
    // GAME SPEED
    // ========================================================================

    private void UpdateGameSpeed()
    {
        speedReadTimer +=
            Time.unscaledDeltaTime;

        if (
            speedReadTimer >=
            SpeedReadInterval
        )
        {
            speedReadTimer = 0f;

            ReadDesiredSpeed();
        }


        // Taskbar Hero puede restaurar
        // Time.timeScale al cambiar de escena.

        if (
            Mathf.Abs(
                Time.timeScale -
                desiredSpeed
            ) > 0.001f
        )
        {
            Time.timeScale =
                desiredSpeed;
        }
    }


    private void ReadDesiredSpeed()
    {
        try
        {
            if (!File.Exists(SpeedFile))
                return;

            string text =
                File.ReadAllText(
                    SpeedFile
                ).Trim();

            if (
                float.TryParse(
                    text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float speed
                )
            )
            {
                desiredSpeed =
                    Mathf.Clamp(
                        speed,
                        0.1f,
                        5.0f
                    );
            }
        }
        catch (Exception ex)
        {
            Plugin.PluginLog?.LogWarning(
                $"[TBH] Error leyendo gamespeed.txt: " +
                $"{ex.Message}"
            );
        }
    }


    // ========================================================================
    // ATTACK DAMAGE OVERRIDE
    // ========================================================================

    private void UpdateAttackDamage()
    {
        damageReadTimer +=
            Time.unscaledDeltaTime;

        if (
            damageReadTimer <
            DamageReadInterval
        )
        {
            return;
        }

        damageReadTimer = 0f;

        ReadDesiredAttackDamage();
    }


    private void ReadDesiredAttackDamage()
    {
        try
        {
            if (!File.Exists(AttackDamageFile))
                return;

            string text =
                File.ReadAllText(
                    AttackDamageFile
                ).Trim();


            // ------------------------------------------------------------
            // "off" desactiva el override
            // ------------------------------------------------------------

            if (
                string.Equals(
                    text,
                    "off",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                ModState.AttackDamageEnabled =
                    false;

                return;
            }


            // ------------------------------------------------------------
            // LEER DAÑO
            // ------------------------------------------------------------

            if (
                float.TryParse(
                    text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float damage
                )
            )
            {
                if (damage <= 0f)
                {
                    ModState.AttackDamageEnabled =
                        false;

                    return;
                }

                damage =
                    Mathf.Clamp(
                        damage,
                        1f,
                        1000000f
                    );

                ModState.AttackDamage =
                    damage;

                ModState.AttackDamageEnabled =
                    true;
            }
        }
        catch (Exception ex)
        {
            Plugin.PluginLog?.LogWarning(
                $"[TBH] Error leyendo attackdamage.txt: " +
                $"{ex.Message}"
            );
        }
    }


    // ========================================================================
    // HERO COMMAND FROM MOD MENU
    // ========================================================================

    private void UpdateHeroCommand()
    {
        heroCommandTimer +=
            Time.unscaledDeltaTime;

        if (
            heroCommandTimer <
            HeroCommandInterval
        )
        {
            return;
        }

        heroCommandTimer = 0f;


        try
        {
            if (!File.Exists(HeroCommandFile))
                return;


            string command =
                File.ReadAllText(
                    HeroCommandFile
                ).Trim();


            if (
                string.IsNullOrWhiteSpace(
                    command
                )
            )
            {
                return;
            }


            // ------------------------------------------------------------
            // NO REPETIR MISMO COMANDO
            // ------------------------------------------------------------

            if (
                command ==
                lastHeroCommand
            )
            {
                return;
            }


            string[] parts =
                command.Split('|');


            if (parts.Length < 2)
            {
                Plugin.PluginLog?.LogWarning(
                    $"[TBH] Comando inválido: " +
                    $"{command}"
                );

                lastHeroCommand =
                    command;

                return;
            }


            // ------------------------------------------------------------
            // HERO ID
            // ------------------------------------------------------------

            if (
                !int.TryParse(
                    parts[0],
                    out int heroId
                )
            )
            {
                Plugin.PluginLog?.LogWarning(
                    $"[TBH] HeroKey inválido: " +
                    $"{parts[0]}"
                );

                lastHeroCommand =
                    command;

                return;
            }


            // ------------------------------------------------------------
            // LEVEL
            // ------------------------------------------------------------

            if (
                !int.TryParse(
                    parts[1],
                    out int level
                )
            )
            {
                Plugin.PluginLog?.LogWarning(
                    $"[TBH] Nivel inválido: " +
                    $"{parts[1]}"
                );

                lastHeroCommand =
                    command;

                return;
            }


            lastHeroCommand =
                command;


            Plugin.PluginLog?.LogInfo(
                $"[TBH] MOD MENU COMMAND -> " +
                $"Hero={heroId} Level={level}"
            );


            SetHeroLevelRuntime(
                heroId,
                level
            );
        }
        catch (Exception ex)
        {
            Plugin.PluginLog?.LogError(
                $"[TBH] UpdateHeroCommand ERROR:\n" +
                $"{ex}"
            );
        }
    }


    // ========================================================================
    // HERO DUMP
    // ========================================================================

    private void TryDumpHeroes()
    {
        if (heroesDumped)
            return;


        heroDumpTimer +=
            Time.unscaledDeltaTime;


        if (
            heroDumpTimer <
            HeroDumpRetryInterval
        )
        {
            return;
        }


        heroDumpTimer = 0f;


        if (DumpHeroes())
        {
            heroesDumped = true;
        }
    }


    private bool DumpHeroes()
    {
        try
        {
            var manager =
                bbl.bspl;


            if (manager == null)
            {
                Plugin.PluginLog?.LogWarning(
                    "[TBH] bbl.bspl todavía es null."
                );

                return false;
            }


            PlayerSaveData save =
                manager.btou;


            if (save == null)
            {
                Plugin.PluginLog?.LogWarning(
                    "[TBH] PlayerSaveData todavía es null."
                );

                return false;
            }


            var heroes =
                save.heroSaveDatas;


            if (heroes == null)
            {
                Plugin.PluginLog?.LogWarning(
                    "[TBH] heroSaveDatas es null."
                );

                return false;
            }


            Plugin.PluginLog?.LogInfo(
                "================================="
            );

            Plugin.PluginLog?.LogInfo(
                $"[TBH] HERO COUNT = " +
                $"{heroes.Count}"
            );

            Plugin.PluginLog?.LogInfo(
                "================================="
            );


            for (
                int i = 0;
                i < heroes.Count;
                i++
            )
            {
                HeroSaveData hero =
                    heroes[i];


                if (hero == null)
                    continue;


                Plugin.PluginLog?.LogInfo(
                    $"[TBH] HERO[{i}] | " +
                    $"Key={hero.heroKey} | " +
                    $"Level={hero.HeroLevel} | " +
                    $"Exp={hero.HeroExp} | " +
                    $"Unlocked={hero.IsUnLock} | " +
                    $"AbilityPoints={hero.AbilityPoint} | " +
                    $"AllocatedPoints=" +
                    $"{hero.AllocatedHeroAbilityPoint}"
                );
            }


            // ------------------------------------------------------------
            // ARRANGED HEROES
            // ------------------------------------------------------------

            try
            {
                var commonSave =
                    save.commonSaveData;


                if (
                    commonSave != null &&
                    commonSave.arrangedHeroKey != null
                )
                {
                    Plugin.PluginLog?.LogInfo(
                        "---------------------------------"
                    );

                    Plugin.PluginLog?.LogInfo(
                        $"[TBH] ARRANGED HERO COUNT = " +
                        $"{commonSave.arrangedHeroKey.Length}"
                    );


                    for (
                        int i = 0;
                        i <
                        commonSave.arrangedHeroKey.Length;
                        i++
                    )
                    {
                        int heroKey =
                            commonSave.arrangedHeroKey[i];

                        Plugin.PluginLog?.LogInfo(
                            $"[TBH] ARRANGED[{i}] " +
                            $"HeroKey={heroKey}"
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.PluginLog?.LogWarning(
                    $"[TBH] arrangedHeroKey ERROR: " +
                    $"{ex.Message}"
                );
            }


            Plugin.PluginLog?.LogInfo(
                "================================="
            );

            Plugin.PluginLog?.LogInfo(
                "[TBH] Hero dump completado."
            );

            Plugin.PluginLog?.LogInfo(
                "================================="
            );


            return true;
        }
        catch (Exception ex)
        {
            Plugin.PluginLog?.LogError(
                $"[TBH] DumpHeroes ERROR:\n" +
                $"{ex}"
            );

            return false;
        }
    }


    // ========================================================================
    // LEVEL EDITOR
    // ========================================================================

    private void SetHeroLevelRuntime(
        int heroId,
        int newLevel
    )
    {
        try
        {
            Plugin.PluginLog?.LogInfo(
                $"[TBH] Cambiando Hero " +
                $"{heroId} a nivel {newLevel}..."
            );


            // ------------------------------------------------------------
            // SAVE MANAGER
            // ------------------------------------------------------------

            var manager =
                bbl.bspl;


            if (manager == null)
            {
                Plugin.PluginLog?.LogWarning(
                    "[TBH] Save manager = NULL"
                );

                return;
            }


            PlayerSaveData save =
                manager.btou;


            if (save == null)
            {
                Plugin.PluginLog?.LogWarning(
                    "[TBH] PlayerSaveData = NULL"
                );

                return;
            }


            // ------------------------------------------------------------
            // BUSCAR HERO SAVE
            // ------------------------------------------------------------

            HeroSaveData saveHero = null;

            var heroes =
                save.heroSaveDatas;

            if (heroes != null)
            {
                for (
                    int i = 0;
                    i < heroes.Count;
                    i++
                )
                {
                    HeroSaveData hero =
                        heroes[i];


                    if (
                        hero != null &&
                        hero.heroKey == heroId
                    )
                    {
                        saveHero = hero;

                        break;
                    }
                }
            }


            if (saveHero == null)
            {
                Plugin.PluginLog?.LogWarning(
                    $"[TBH] HeroSaveData " +
                    $"{heroId} no encontrado."
                );

                return;
            }


            // ------------------------------------------------------------
            // RUNTIME CACHE
            // ------------------------------------------------------------

            vo cache =
                vm.uj.ivu(heroId);


            if (cache == null)
            {
                Plugin.PluginLog?.LogWarning(
                    $"[TBH] Runtime vo para " +
                    $"{heroId} = NULL"
                );

                return;
            }


            // ------------------------------------------------------------
            // CLAMP LEVEL
            // ------------------------------------------------------------

            int maxLevel =
                cache.btep;


            if (maxLevel > 0)
            {
                newLevel =
                    Math.Clamp(
                        newLevel,
                        1,
                        maxLevel
                    );
            }
            else
            {
                newLevel =
                    Math.Max(
                        1,
                        newLevel
                    );
            }


            int oldLevel =
                cache.bteo;


            int allocatedPoints =
                cache.bter;


            // ------------------------------------------------------------
            // LEVEL
            // ------------------------------------------------------------

            saveHero.HeroLevel =
                newLevel;

            cache.bfmc =
                newLevel;


            // ------------------------------------------------------------
            // ABILITY POINTS
            // ------------------------------------------------------------

            int availablePoints =
                Math.Max(
                    0,
                    newLevel -
                    allocatedPoints
                );


            saveHero.AbilityPoint =
                availablePoints;

            cache.bfme =
                availablePoints;


            // ------------------------------------------------------------
            // LOG
            // ------------------------------------------------------------

            Plugin.PluginLog?.LogInfo(
                $"[TBH] LEVEL: " +
                $"{oldLevel} -> {cache.bteo}"
            );

            Plugin.PluginLog?.LogInfo(
                $"[TBH] ABILITY POINTS: " +
                $"{cache.bteq}"
            );

            Plugin.PluginLog?.LogInfo(
                $"[TBH] ALLOCATED POINTS: " +
                $"{cache.bter}"
            );

            Plugin.PluginLog?.LogInfo(
                $"[TBH] SAVE: " +
                $"Level={saveHero.HeroLevel} | " +
                $"Ability={saveHero.AbilityPoint} | " +
                $"Allocated=" +
                $"{saveHero.AllocatedHeroAbilityPoint}"
            );

            Plugin.PluginLog?.LogInfo(
                $"[TBH] RUNTIME: " +
                $"Level={cache.bteo} | " +
                $"Ability={cache.bteq} | " +
                $"Allocated={cache.bter} | " +
                $"HeroKey={cache.btet}"
            );

            Plugin.PluginLog?.LogInfo(
                "[TBH] Cambio completado."
            );
        }
        catch (Exception ex)
        {
            Plugin.PluginLog?.LogError(
                $"[TBH] SetHeroLevelRuntime ERROR:\n" +
                $"{ex}"
            );
        }
    }


    // ========================================================================
    // HERO RUNTIME DEBUG
    // ========================================================================

    private void TestHeroRuntime(
        int heroId
    )
    {
        try
        {
            Plugin.PluginLog?.LogInfo(
                $"[TBH] ===== HERO {heroId} ====="
            );


            vo cache =
                vm.uj.ivu(heroId);


            if (cache == null)
            {
                Plugin.PluginLog?.LogWarning(
                    $"[TBH] Hero runtime " +
                    $"{heroId} = NULL"
                );

                return;
            }


            Plugin.PluginLog?.LogInfo(
                $"[TBH] Level={cache.bteo}"
            );

            Plugin.PluginLog?.LogInfo(
                $"[TBH] MaxLevel={cache.btep}"
            );

            Plugin.PluginLog?.LogInfo(
                $"[TBH] Ability={cache.bteq}"
            );

            Plugin.PluginLog?.LogInfo(
                $"[TBH] Allocated={cache.bter}"
            );

            Plugin.PluginLog?.LogInfo(
                $"[TBH] Exp={cache.btes}"
            );

            Plugin.PluginLog?.LogInfo(
                $"[TBH] HeroKey={cache.btet}"
            );


            try
            {
                var info =
                    cache.bflj;

                if (info != null)
                {
                    Plugin.PluginLog?.LogInfo(
                        $"[TBH] Base AttackDamage=" +
                        $"{info.AttackDamage}"
                    );
                }
            }
            catch
            {
            }


            Plugin.PluginLog?.LogInfo(
                "[TBH] ==========================="
            );
        }
        catch (Exception ex)
        {
            Plugin.PluginLog?.LogError(
                $"[TBH] TestHeroRuntime ERROR:\n" +
                $"{ex}"
            );
        }
    }


    // ========================================================================
    // DAMAGE DEBUG
    // ========================================================================

    private void DumpAttackDamageStats()
    {
        StatType stat =
            StatType.AttackDamage;


        Plugin.PluginLog?.LogInfo(
            "=========================================="
        );

        Plugin.PluginLog?.LogInfo(
            "[TBH] ATTACK DAMAGE RUNTIME TEST"
        );

        Plugin.PluginLog?.LogInfo(
            $"[TBH] Override Enabled = " +
            $"{ModState.AttackDamageEnabled}"
        );

        Plugin.PluginLog?.LogInfo(
            $"[TBH] Override Value = " +
            $"{ModState.AttackDamage}"
        );


        try
        {
            Plugin.PluginLog?.LogInfo(
                $"[TBH] iwv = " +
                $"{vm.uj.iwv(stat)}"
            );
        }
        catch (Exception ex)
        {
            Plugin.PluginLog?.LogWarning(
                $"[TBH] iwv ERROR: " +
                $"{ex.Message}"
            );
        }


        try
        {
            Plugin.PluginLog?.LogInfo(
                $"[TBH] iwy = " +
                $"{vm.uj.iwy(stat)}"
            );
        }
        catch (Exception ex)
        {
            Plugin.PluginLog?.LogWarning(
                $"[TBH] iwy ERROR: " +
                $"{ex.Message}"
            );
        }


        try
        {
            Plugin.PluginLog?.LogInfo(
                $"[TBH] npp = " +
                $"{vm.uj.npp(stat)}"
            );
        }
        catch (Exception ex)
        {
            Plugin.PluginLog?.LogWarning(
                $"[TBH] npp ERROR: " +
                $"{ex.Message}"
            );
        }


        try
        {
            Plugin.PluginLog?.LogInfo(
                $"[TBH] iht = " +
                $"{vm.uj.iht(stat)}"
            );
        }
        catch (Exception ex)
        {
            Plugin.PluginLog?.LogWarning(
                $"[TBH] iht ERROR: " +
                $"{ex.Message}"
            );
        }


        try
        {
            Plugin.PluginLog?.LogInfo(
                $"[TBH] jjj = " +
                $"{vm.uj.jjj(stat)}"
            );
        }
        catch (Exception ex)
        {
            Plugin.PluginLog?.LogWarning(
                $"[TBH] jjj ERROR: " +
                $"{ex.Message}"
            );
        }


        // ------------------------------------------------------------
        // LOS QUE DIERON VALORES EXTRAÑOS
        // ------------------------------------------------------------

        try
        {
            Plugin.PluginLog?.LogInfo(
                $"[TBH] jym = " +
                $"{vm.uj.jym(stat)}"
            );
        }
        catch
        {
        }


        try
        {
            Plugin.PluginLog?.LogInfo(
                $"[TBH] inp = " +
                $"{vm.uj.inp(stat)}"
            );
        }
        catch
        {
        }


        try
        {
            Plugin.PluginLog?.LogInfo(
                $"[TBH] bmx = " +
                $"{vm.uj.bmx(stat)}"
            );
        }
        catch
        {
        }


        Plugin.PluginLog?.LogInfo(
            "=========================================="
        );
    }

    private void DumpHeroDamageCandidates(int heroId)
{
    try
    {
        Plugin.PluginLog?.LogInfo(
            "========================================"
        );

        Plugin.PluginLog?.LogInfo(
            $"[TBH] DAMAGE CANDIDATES HERO {heroId}"
        );

        vo cache = vm.uj.ivu(heroId);

        if (cache == null)
        {
            Plugin.PluginLog?.LogWarning(
                $"[TBH] vo {heroId} = NULL"
            );

            return;
        }

        Hero hero = cache.bflu;

        if (hero == null)
        {
            Plugin.PluginLog?.LogWarning(
                $"[TBH] Hero {heroId} = NULL"
            );

            return;
        }


        // ========================================================
        // 1. DamageInfo directo del Unit/Hero
        // ========================================================

        try
        {
            DamageInfo info = hero.gut();

            Plugin.PluginLog?.LogInfo(
                "[TBH] ----- gut() DAMAGEINFO -----"
            );

            Plugin.PluginLog?.LogInfo(
                $"[TBH] OriginDamage = {info.OriginDamage}"
            );

            Plugin.PluginLog?.LogInfo(
                $"[TBH] DamageAttribute = {info.DamageAttribute}"
            );

            Plugin.PluginLog?.LogInfo(
                $"[TBH] DamageType = {info.DamageType}"
            );
        }
        catch (Exception ex)
        {
            Plugin.PluginLog?.LogWarning(
                $"[TBH] gut ERROR: {ex.Message}"
            );
        }


        // ========================================================
        // StatType.AttackDamage = 1
        // ========================================================

        int attackDamageStat =
            (int)StatType.AttackDamage;


        // ========================================================
        // 2. gqm(int)
        // ========================================================

        try
        {
            float result =
                hero.gqm(attackDamageStat);

            Plugin.PluginLog?.LogInfo(
                $"[TBH] gqm(AttackDamage) = {result}"
            );
        }
        catch (Exception ex)
        {
            Plugin.PluginLog?.LogWarning(
                $"[TBH] gqm ERROR: {ex.Message}"
            );
        }


        // ========================================================
        // 3. gui(int)
        // ========================================================

        try
        {
            float result =
                hero.gui(attackDamageStat);

            Plugin.PluginLog?.LogInfo(
                $"[TBH] gui(AttackDamage) = {result}"
            );
        }
        catch (Exception ex)
        {
            Plugin.PluginLog?.LogWarning(
                $"[TBH] gui ERROR: {ex.Message}"
            );
        }


        Plugin.PluginLog?.LogInfo(
            "========================================"
        );
    }
    catch (Exception ex)
    {
        Plugin.PluginLog?.LogError(
            $"[TBH] DumpHeroDamageCandidates ERROR:\n{ex}"
        );
    }
}

    private void UpdateDamageCommand()
    {
        damageCommandTimer +=
            Time.unscaledDeltaTime;

        if (
            damageCommandTimer <
            DamageCommandInterval
        )
        {
            return;
        }

        damageCommandTimer = 0f;

        try
        {
            if (!File.Exists(DamageCommandFile))
                return;

            string command =
                File.ReadAllText(
                    DamageCommandFile
                ).Trim();

            if (
                string.IsNullOrWhiteSpace(
                    command
                )
            )
            {
                return;
            }

            if (
                command ==
                lastDamageCommand
            )
            {
                return;
            }

            string[] parts =
                command.Split('|');

            if (parts.Length < 2)
            {
                lastDamageCommand =
                    command;

                return;
            }

            if (
                !int.TryParse(
                    parts[0],
                    out int heroId
                )
            )
            {
                lastDamageCommand =
                    command;

                return;
            }

            if (
                !float.TryParse(
                    parts[1],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float damage
                )
            )
            {
                lastDamageCommand =
                    command;

                return;
            }

            lastDamageCommand =
                command;

            damage =
                Mathf.Clamp(
                    damage,
                    1f,
                    1000000f
                );

            // ========================================================
            // CAMBIAR VALOR DEL PATCH
            // ========================================================

            ModState.AttackDamage =
                damage;

            ModState.AttackDamageEnabled =
                true;

            Plugin.PluginLog?.LogInfo(
                $"[TBH] MOD MENU DAMAGE -> " +
                $"Hero={heroId} Damage={damage}"
            );

            // ========================================================
            // REFRESCAR DAÑO DEL HERO
            // ========================================================

            RefreshHeroDamage(
                heroId
            );
        }
        catch (Exception ex)
        {
            Plugin.PluginLog?.LogError(
                $"[TBH] UpdateDamageCommand ERROR:\n{ex}"
            );
        }
    }

    private void RefreshHeroDamage(
    int heroId
)
{
    try
    {
        vo cache =
            vm.uj.ivu(heroId);

        if (cache == null)
        {
            Plugin.PluginLog?.LogWarning(
                $"[TBH] Damage refresh: " +
                $"vo {heroId} = NULL"
            );

            return;
        }

        Hero hero =
            cache.bflu;

        if (hero == null)
        {
            Plugin.PluginLog?.LogWarning(
                $"[TBH] Damage refresh: " +
                $"Hero {heroId} = NULL"
            );

            return;
        }

        // ========================================================
        // ESTA ES LA PARTE QUE ANTES ACTIVABAS CON F4
        // ========================================================

        DamageInfo info =
            hero.gut();

        Plugin.PluginLog?.LogInfo(
            $"[TBH] DAMAGE REFRESH -> " +
            $"Hero={heroId} | " +
            $"OriginDamage={info.OriginDamage}"
        );
    }
    catch (Exception ex)
    {
        Plugin.PluginLog?.LogError(
            $"[TBH] RefreshHeroDamage ERROR:\n{ex}"
        );
    }
}

    // ========================================================================
    // HERO SPEED COMMANDS
    // ========================================================================

    private void UpdateHeroSpeedCommands()
    {
        speedCommandTimer +=
            Time.unscaledDeltaTime;

        if (
            speedCommandTimer <
            SpeedCommandInterval
        )
        {
            return;
        }

        speedCommandTimer = 0f;

        ProcessAttackSpeedCommand();

        ProcessMovementSpeedCommand();
    }


    private void ProcessAttackSpeedCommand()
    {
        try
        {
            if (!File.Exists(AttackSpeedCommandFile))
                return;

            string command =
                File.ReadAllText(
                    AttackSpeedCommandFile
                ).Trim();

            if (
                string.IsNullOrWhiteSpace(command) ||
                command == lastAttackSpeedCommand
            )
            {
                return;
            }

            string[] parts =
                command.Split('|');

            if (parts.Length < 2)
            {
                lastAttackSpeedCommand = command;

                Plugin.PluginLog?.LogWarning(
                    $"[TBH] AttackSpeed command inválido: {command}"
                );

                return;
            }

            if (
                !int.TryParse(
                    parts[0],
                    out int heroId
                )
            )
            {
                lastAttackSpeedCommand = command;
                return;
            }

            if (
                !float.TryParse(
                    parts[1],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float value
                )
            )
            {
                lastAttackSpeedCommand = command;
                return;
            }

            value =
                Mathf.Clamp(
                    value,
                    0.10f,
                    100f
                );

            ModState.AttackSpeed =
                value;

            ModState.AttackSpeedHeroId =
                heroId;

            ModState.AttackSpeedEnabled =
                true;

            lastAttackSpeedCommand =
                command;

            File.WriteAllText(
                AttackSpeedFile,
                value.ToString(
                    CultureInfo.InvariantCulture
                )
            );

            UpdateAttackSpeedTarget(
                heroId
            );

            Plugin.PluginLog?.LogInfo(
                $"[TBH] ATTACK SPEED -> " +
                $"Hero={heroId} Value={value}"
            );

            RefreshHeroSpeeds(
                heroId
            );
        }
        catch (Exception ex)
        {
            Plugin.PluginLog?.LogWarning(
                $"[TBH] AttackSpeed command ERROR: {ex.Message}"
            );
        }
    }


    private void ProcessMovementSpeedCommand()
    {
        try
        {
            if (!File.Exists(MovementSpeedCommandFile))
                return;

            string command =
                File.ReadAllText(
                    MovementSpeedCommandFile
                ).Trim();

            if (
                string.IsNullOrWhiteSpace(command) ||
                command == lastMovementSpeedCommand
            )
            {
                return;
            }

            string[] parts =
                command.Split('|');

            if (parts.Length < 2)
            {
                lastMovementSpeedCommand = command;

                Plugin.PluginLog?.LogWarning(
                    $"[TBH] MovementSpeed command inválido: {command}"
                );

                return;
            }

            if (
                !int.TryParse(
                    parts[0],
                    out int heroId
                )
            )
            {
                lastMovementSpeedCommand = command;
                return;
            }

            if (
                !float.TryParse(
                    parts[1],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float value
                )
            )
            {
                lastMovementSpeedCommand = command;
                return;
            }

            value =
                Mathf.Clamp(
                    value,
                    0.10f,
                    125f
                );

           // Último valor aplicado.
            // Se conserva por compatibilidad con el resto del plugin.
            ModState.MovementSpeed =
                value;

            ModState.MovementSpeedHeroId =
                heroId;

            ModState.MovementSpeedEnabled =
                true;


            // ========================================================
            // GUARDAR VELOCIDAD INDEPENDIENTE PARA ESTE HERO
            // ========================================================

            ModState.MovementSpeedByHero[heroId] =
                value;


            // Reiniciar solamente el seguimiento de ESTE héroe.

            lastHeroPositions.Remove(
                heroId
            );

            initializedMovementHeroes.Remove(
                heroId
            );

            lastMovementSpeedCommand =
                command;

            File.WriteAllText(
                MovementSpeedFile,
                value.ToString(
                    CultureInfo.InvariantCulture
                )
            );

            UpdateMovementSpeedTarget(
                heroId
            );

            Plugin.PluginLog?.LogInfo(
                $"[TBH] MOVEMENT SPEED -> " +
                $"Hero={heroId} Value={value}"
            );

            RefreshHeroSpeeds(
                heroId
            );
        }
        catch (Exception ex)
        {
            Plugin.PluginLog?.LogWarning(
                $"[TBH] MovementSpeed command ERROR: {ex.Message}"
            );
        }
    }


    // ========================================================================
    // HERO SPEED TARGETS
    //
    // El Hero puede reconstruirse al cambiar de stage/escena.
    // Actualizamos periódicamente el InstanceID para mantener el override
    // asociado al personaje seleccionado.
    // ========================================================================

    private void UpdateHeroSpeedTargets()
    {
        heroSpeedTargetTimer +=
            Time.unscaledDeltaTime;

        if (
            heroSpeedTargetTimer <
            HeroSpeedTargetInterval
        )
        {
            return;
        }

        heroSpeedTargetTimer = 0f;

        if (
            ModState.AttackSpeedEnabled &&
            ModState.AttackSpeedHeroId > 0
        )
        {
            UpdateAttackSpeedTarget(
                ModState.AttackSpeedHeroId
            );
        }

        if (
            ModState.MovementSpeedEnabled &&
            ModState.MovementSpeedByHero.Count > 0
        )
        {
            List<int> movementHeroIds =
                new List<int>(
                    ModState.MovementSpeedByHero.Keys
                );

            foreach (
                int heroId in movementHeroIds
            )
            {
                UpdateMovementSpeedTarget(
                    heroId
                );
            }
        }
    }


    private Hero GetRuntimeHero(
        int heroId
    )
    {
        try
        {
            vo cache =
                vm.uj.ivu(heroId);

            if (cache == null)
                return null;

            return cache.bflu;
        }
        catch
        {
            return null;
        }
    }


    private void UpdateAttackSpeedTarget(
        int heroId
    )
    {
        Hero hero =
            GetRuntimeHero(heroId);

        if (hero == null)
        {
            ModState.AttackSpeedHeroInstanceId =
                int.MinValue;

            return;
        }

        ModState.AttackSpeedHeroInstanceId =
            hero.GetInstanceID();
    }


    private void UpdateMovementSpeedTarget(
    int heroId
    )
    {
        try
        {
            // Debe existir una configuración para este héroe.

            if (
                !ModState.MovementSpeedByHero.TryGetValue(
                    heroId,
                    out float speed
                )
            )
            {
                return;
            }


            Hero hero =
                GetRuntimeHero(heroId);


            // ========================================================
            // HERO NO INSTANCIADO
            // ========================================================

            if (hero == null)
            {
                if (
                    ModState.MovementInstanceByHero.TryGetValue(
                        heroId,
                        out int oldInstance
                    )
                )
                {
                    ModState.MovementSpeedByInstance.Remove(
                        oldInstance
                    );

                    ModState.MovementInstanceByHero.Remove(
                        heroId
                    );
                }

                return;
            }


            int newInstance =
                hero.GetInstanceID();


            // ========================================================
            // ELIMINAR INSTANCE ANTERIOR SI EL JUEGO RECREÓ EL HERO
            // ========================================================

            if (
                ModState.MovementInstanceByHero.TryGetValue(
                    heroId,
                    out int previousInstance
                )
                &&
                previousInstance != newInstance
            )
            {
                ModState.MovementSpeedByInstance.Remove(
                    previousInstance
                );
            }


            // ========================================================
            // ASOCIAR INSTANCE ACTUAL CON SU VELOCIDAD
            // ========================================================

            ModState.MovementInstanceByHero[heroId] =
                newInstance;

            ModState.MovementSpeedByInstance[newInstance] =
                speed;


            // Los mantenemos por compatibilidad.

            ModState.MovementSpeedHeroId =
                heroId;

            ModState.MovementSpeedHeroInstanceId =
                newInstance;
        }
        catch (Exception ex)
        {
            Plugin.PluginLog?.LogWarning(
                $"[TBH] UpdateMovementSpeedTarget ERROR: " +
                $"{ex.Message}"
            );
        }
    }


    private void RefreshHeroSpeeds(
        int heroId
    )
    {
        try
        {
            Hero hero =
                GetRuntimeHero(heroId);

            if (hero == null)
            {
                Plugin.PluginLog?.LogWarning(
                    $"[TBH] SPEED REFRESH: Hero {heroId} = NULL"
                );

                return;
            }

            // Fuerza una lectura inmediata de los getters parcheados.
            float attackSpeed =
                hero.bsqu;

            float movementSpeed =
                hero.bsrq;

            Plugin.PluginLog?.LogInfo(
                $"[TBH] SPEED REFRESH -> " +
                $"Hero={heroId} | " +
                $"AttackSpeed={attackSpeed} | " +
                $"MovementSpeed={movementSpeed}"
            );
        }
        catch (Exception ex)
        {
            Plugin.PluginLog?.LogWarning(
                $"[TBH] RefreshHeroSpeeds ERROR: {ex.Message}"
            );
        }
    }


private void DumpSpeedStats(int heroId)
{
    try
    {
        Plugin.PluginLog?.LogInfo(
            "=========================================="
        );

        Plugin.PluginLog?.LogInfo(
            "[TBH] SPEED RUNTIME TEST"
        );

        // ========================================================
        // MOSTRAR TODOS LOS STATTYPE RELACIONADOS CON SPEED/MOVE
        // ========================================================

        Plugin.PluginLog?.LogInfo(
            "[TBH] ---- STAT TYPES SPEED / MOVE ----"
        );

        Array values =
            Enum.GetValues(typeof(StatType));

        foreach (object value in values)
        {
            StatType stat =
                (StatType)value;

            string name =
                stat.ToString();

            if (
                name.IndexOf(
                    "speed",
                    StringComparison.OrdinalIgnoreCase
                ) >= 0
                ||
                name.IndexOf(
                    "move",
                    StringComparison.OrdinalIgnoreCase
                ) >= 0
                ||
                name.IndexOf(
                    "movement",
                    StringComparison.OrdinalIgnoreCase
                ) >= 0
            )
            {
                Plugin.PluginLog?.LogInfo(
                    $"[TBH] STAT {name} = {(int)stat}"
                );
            }
        }


        // ========================================================
        // ATTACK SPEED
        // ========================================================

        StatType attackSpeed =
            StatType.AttackSpeed;

        Plugin.PluginLog?.LogInfo(
            "[TBH] ---- ATTACK SPEED ----"
        );

        try
        {
            Plugin.PluginLog?.LogInfo(
                $"[TBH] iwv = {vm.uj.iwv(attackSpeed)}"
            );
        }
        catch { }

        try
        {
            Plugin.PluginLog?.LogInfo(
                $"[TBH] iwy = {vm.uj.iwy(attackSpeed)}"
            );
        }
        catch { }

        try
        {
            Plugin.PluginLog?.LogInfo(
                $"[TBH] npp = {vm.uj.npp(attackSpeed)}"
            );
        }
        catch { }

        try
        {
            Plugin.PluginLog?.LogInfo(
                $"[TBH] iht = {vm.uj.iht(attackSpeed)}"
            );
        }
        catch { }

        try
        {
            Plugin.PluginLog?.LogInfo(
                $"[TBH] jjj = {vm.uj.jjj(attackSpeed)}"
            );
        }
        catch { }


        // ========================================================
        // HERO RUNTIME
        // ========================================================

        vo cache =
            vm.uj.ivu(heroId);

        if (cache == null)
        {
            Plugin.PluginLog?.LogWarning(
                $"[TBH] vo {heroId} = NULL"
            );

            return;
        }

        Hero hero =
            cache.bflu;

        if (hero == null)
        {
            Plugin.PluginLog?.LogWarning(
                $"[TBH] Hero {heroId} = NULL"
            );

            return;
        }

        // ========================================================
        // PROPIEDADES FLOAT DEL HERO
        //
        // Esto nos ayudará a identificar movimiento comparando
        // valores reales.
        // ========================================================

        Plugin.PluginLog?.LogInfo(
            "[TBH] ---- HERO FLOAT PROPERTIES ----"
        );

        Type heroType =
            hero.GetType();

        PropertyInfo[] properties =
            heroType.GetProperties(
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Instance
            );

        foreach (
            PropertyInfo property
            in properties
        )
        {
            if (
                property.PropertyType
                != typeof(float)
            )
            {
                continue;
            }

            if (
                property.GetIndexParameters().Length
                != 0
            )
            {
                continue;
            }

            try
            {
                object value =
                    property.GetValue(hero);

                Plugin.PluginLog?.LogInfo(
                    $"[TBH] {property.Name} = {value}"
                );
            }
            catch
            {
            }
        }

        Plugin.PluginLog?.LogInfo(
            "=========================================="
        );
    }
    catch (Exception ex)
    {
        Plugin.PluginLog?.LogError(
            $"[TBH] DumpSpeedStats ERROR:\n{ex}"
        );
    }
}

private void DumpMovementSpeed(int heroId)
{
    try
    {
        Plugin.PluginLog?.LogInfo(
            "=========================================="
        );

        Plugin.PluginLog?.LogInfo(
            "[TBH] MOVEMENT SPEED TEST"
        );

        StatType movement =
            StatType.MovementSpeed;

        float iwv = 0f;
        float iwy = 0f;
        float npp = 0f;
        float iht = 0f;
        float jjj = 0f;

        try
        {
            iwv = vm.uj.iwv(movement);

            Plugin.PluginLog?.LogInfo(
                $"[TBH] iwv(MovementSpeed) = {iwv}"
            );
        }
        catch { }

        try
        {
            iwy = vm.uj.iwy(movement);

            Plugin.PluginLog?.LogInfo(
                $"[TBH] iwy(MovementSpeed) = {iwy}"
            );
        }
        catch { }

        try
        {
            npp = vm.uj.npp(movement);

            Plugin.PluginLog?.LogInfo(
                $"[TBH] npp(MovementSpeed) = {npp}"
            );
        }
        catch { }

        try
        {
            iht = vm.uj.iht(movement);

            Plugin.PluginLog?.LogInfo(
                $"[TBH] iht(MovementSpeed) = {iht}"
            );
        }
        catch { }

        try
        {
            jjj = vm.uj.jjj(movement);

            Plugin.PluginLog?.LogInfo(
                $"[TBH] jjj(MovementSpeed) = {jjj}"
            );
        }
        catch { }


        // ========================================================
        // HERO
        // ========================================================

        vo cache =
            vm.uj.ivu(heroId);

        if (cache == null)
            return;

        Hero hero =
            cache.bflu;

        if (hero == null)
            return;


        Type heroType =
            hero.GetType();

        PropertyInfo[] properties =
            heroType.GetProperties(
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Instance
            );


        Plugin.PluginLog?.LogInfo(
            "[TBH] ---- PROPERTIES MATCHING MOVEMENT ----"
        );


        foreach (PropertyInfo property in properties)
        {
            if (
                property.PropertyType !=
                typeof(float)
            )
            {
                continue;
            }

            if (
                property.GetIndexParameters().Length !=
                0
            )
            {
                continue;
            }

            try
            {
                object obj =
                    property.GetValue(hero);

                if (obj == null)
                    continue;

                float value =
                    (float)obj;

                bool match =
                    Mathf.Abs(value - iwv) < 0.001f ||
                    Mathf.Abs(value - iwy) < 0.001f ||
                    Mathf.Abs(value - npp) < 0.001f ||
                    Mathf.Abs(value - iht) < 0.001f ||
                    Mathf.Abs(value - jjj) < 0.001f;

                if (!match)
                    continue;

                Plugin.PluginLog?.LogInfo(
                    $"[TBH] MATCH -> " +
                    $"{property.Name} = {value} | " +
                    $"DeclaredBy={property.DeclaringType?.FullName}"
                );
            }
            catch
            {
            }
        }


        // ========================================================
        // ATTACK SPEED PROPERTY
        // ========================================================

        try
        {
            PropertyInfo attackProperty =
                heroType.GetProperty(
                    "bsqu",
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.Instance
                );

            if (attackProperty != null)
            {
                Plugin.PluginLog?.LogInfo(
                    $"[TBH] ATTACK PROPERTY bsqu | " +
                    $"Value={attackProperty.GetValue(hero)} | " +
                    $"DeclaredBy={attackProperty.DeclaringType?.FullName}"
                );
            }
        }
        catch
        {
        }


        Plugin.PluginLog?.LogInfo(
            "=========================================="
        );
    }
    catch (Exception ex)
    {
        Plugin.PluginLog?.LogError(
            $"[TBH] DumpMovementSpeed ERROR:\n{ex}"
        );
    }
}

private void LateUpdate()
{
    try
    {
        // ========================================================
        // MOVEMENT SPEED DESACTIVADO
        // ========================================================

        if (
            !ModState.MovementSpeedEnabled ||
            ModState.MovementSpeedByHero.Count == 0
        )
        {
            lastHeroPositions.Clear();

            initializedMovementHeroes.Clear();

            return;
        }


        // Copia para evitar problemas si entra un comando
        // mientras recorremos los valores.

        List<KeyValuePair<int, float>> configuredHeroes =
            new List<KeyValuePair<int, float>>(
                ModState.MovementSpeedByHero
            );


        foreach (
            KeyValuePair<int, float> pair
            in configuredHeroes
        )
        {
            int heroId =
                pair.Key;

            float desiredMovementSpeed =
                pair.Value;


            // ====================================================
            // OBTENER HERO
            // ====================================================

            vo cache =
                vm.uj.ivu(
                    heroId
                );

            if (cache == null)
            {
                lastHeroPositions.Remove(
                    heroId
                );

                initializedMovementHeroes.Remove(
                    heroId
                );

                continue;
            }


            Hero hero =
                cache.bflu;

            if (hero == null)
            {
                lastHeroPositions.Remove(
                    heroId
                );

                initializedMovementHeroes.Remove(
                    heroId
                );

                continue;
            }


            // ====================================================
            // IGNORAR HERO INACTIVO
            // ====================================================

            if (
                hero.gameObject == null ||
                !hero.gameObject.activeInHierarchy ||
                !hero.enabled
            )
            {
                lastHeroPositions.Remove(
                    heroId
                );

                initializedMovementHeroes.Remove(
                    heroId
                );

                continue;
            }


            Transform heroTransform =
                hero.transform;

            if (heroTransform == null)
                continue;


            Vector3 currentPosition =
                heroTransform.position;


            // ====================================================
            // PRIMER FRAME DE ESTE HERO
            // ====================================================

            if (
                !initializedMovementHeroes.Contains(
                    heroId
                )
            )
            {
                lastHeroPositions[heroId] =
                    currentPosition;

                initializedMovementHeroes.Add(
                    heroId
                );

                continue;
            }


            if (
                !lastHeroPositions.TryGetValue(
                    heroId,
                    out Vector3 lastPosition
                )
            )
            {
                lastHeroPositions[heroId] =
                    currentPosition;

                continue;
            }


            // ====================================================
            // MOVIMIENTO NATIVO DEL JUEGO ESTE FRAME
            // ====================================================

            Vector3 delta =
                currentPosition -
                lastPosition;


            // ====================================================
            // EVITAR MULTIPLICAR TELEPORTS
            // ====================================================

            if (
                Mathf.Abs(delta.x) >
                3f
            )
            {
                lastHeroPositions[heroId] =
                    currentPosition;

                continue;
            }


            // ====================================================
            // MULTIPLICADOR INDIVIDUAL
            // ====================================================

            float multiplier =
                desiredMovementSpeed /
                NativeMovementSpeed;

            multiplier =
                Mathf.Max(
                    0.01f,
                    multiplier
                );


            // Solo horizontal.

            float newX =
                lastPosition.x +
                (
                    delta.x *
                    multiplier
                );


            Vector3 modifiedPosition =
                new Vector3(
                    newX,
                    currentPosition.y,
                    currentPosition.z
                );


            heroTransform.position =
                modifiedPosition;


            lastHeroPositions[heroId] =
                modifiedPosition;
        }
    }
    catch (Exception ex)
    {
        Plugin.PluginLog?.LogWarning(
            $"[TBH] Movement LateUpdate ERROR: " +
            $"{ex.Message}"
        );
    }
}

// ========================================================================
// MONEY MULTIPLIER
// ========================================================================

private void UpdateMoneyMultiplier()
{
    moneyMultiplierReadTimer +=
        Time.unscaledDeltaTime;

    if (
        moneyMultiplierReadTimer <
        MoneyMultiplierReadInterval
    )
    {
        return;
    }

    moneyMultiplierReadTimer = 0f;

    ReadMoneyMultiplier();
}


private void ReadMoneyMultiplier()
{
    try
    {
        if (!File.Exists(MoneyMultiplierFile))
            return;

        string text =
            File.ReadAllText(
                MoneyMultiplierFile
            ).Trim();

        if (
            float.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float multiplier
            )
        )
        {
            multiplier =
                Mathf.Clamp(
                    multiplier,
                    1.0f,
                    100000.0f
                );

            if (
                Mathf.Abs(
                    multiplier -
                    ModState.MoneyMultiplier
                ) > 0.001f
            )
            {
                ModState.MoneyMultiplier =
                    multiplier;

                Plugin.PluginLog?.LogInfo(
                    $"[TBH] MONEY MULTIPLIER -> " +
                    $"{multiplier:0.##}x"
                );
            }
        }
    }
    catch (Exception ex)
    {
        Plugin.PluginLog?.LogWarning(
            $"[TBH] MoneyMultiplier ERROR: " +
            $"{ex.Message}"
        );
    }
}

// ========================================================================
// GOD MODE
// ========================================================================

private void UpdateGodMode()
{
    godModeReadTimer +=
        Time.unscaledDeltaTime;

    if (
        godModeReadTimer <
        GodModeReadInterval
    )
    {
        return;
    }

    godModeReadTimer = 0f;

    ReadGodMode();
}


private void ReadGodMode()
{
    try
    {
        if (!File.Exists(GodModeFile))
            return;

        string text =
            File.ReadAllText(
                GodModeFile
            ).Trim();

        bool enabled =
            text == "1" ||
            string.Equals(
                text,
                "true",
                StringComparison.OrdinalIgnoreCase
            ) ||
            string.Equals(
                text,
                "on",
                StringComparison.OrdinalIgnoreCase
            );

        if (
            enabled !=
            ModState.GodModeEnabled
        )
        {
            ModState.GodModeEnabled =
                enabled;

            Plugin.PluginLog?.LogInfo(
                $"[TBH] GOD MODE -> " +
                $"{(enabled ? "ON" : "OFF")}"
            );
        }
    }
    catch (Exception ex)
    {
        Plugin.PluginLog?.LogWarning(
            $"[TBH] GodMode ERROR: {ex.Message}"
        );
    }
}

private void MaintainGodModeHealth()
{
    try
    {
        if (!ModState.GodModeEnabled)
            return;

        int[] heroIds =
        {
            101,
            201,
            301,
            401,
            501,
            601
        };

        foreach (int heroId in heroIds)
        {
            try
            {
                vo cache =
                    vm.uj.ivu(
                        heroId
                    );

                if (cache == null)
                    continue;

                Hero hero =
                    cache.bflu;

                if (hero == null)
                    continue;

                var health =
                    hero.UnitHealthController;

                if (health == null)
                    continue;

                float currentHealth =
                    health.bdhg;

                float maxHealth =
                    health.bdhj;

                if (maxHealth <= 0f)
                    continue;

                if (
                    currentHealth <
                    maxHealth
                )
                {
                    health.bdhg =
                        maxHealth;
                }
            }
            catch
            {
                // Otro hero puede no estar instanciado.
            }
        }
    }
    catch (Exception ex)
    {
        Plugin.PluginLog?.LogWarning(
            $"[TBH] MaintainGodModeHealth ERROR: {ex.Message}"
        );
    }
}


}


// ============================================================================
// HARMONY PATCHES
//
// Solo se modifica StatType.AttackDamage.
//
// Nuestra prueba mostró que estos cinco métodos devolvían el mismo
// Attack Damage mostrado por STATUS.
//
// ============================================================================


// ----------------------------------------------------------------------------
// iwv
// ----------------------------------------------------------------------------

[HarmonyPatch(
    typeof(vm.uj),
    nameof(vm.uj.iwv)
)]
internal static class AttackDamagePatch_IWV
{
    [HarmonyPostfix]
    private static void Postfix(
        StatType a,
        ref float __result
    )
    {
        ModState.ApplyAttackDamage(
            a,
            ref __result
        );
    }

    
}


// ----------------------------------------------------------------------------
// iwy
// ----------------------------------------------------------------------------

[HarmonyPatch(
    typeof(vm.uj),
    nameof(vm.uj.iwy)
)]
internal static class AttackDamagePatch_IWY
{
    [HarmonyPostfix]
    private static void Postfix(
        StatType a,
        ref float __result
    )
    {
        ModState.ApplyAttackDamage(
            a,
            ref __result
        );
    }
}


// ----------------------------------------------------------------------------
// npp
// ----------------------------------------------------------------------------

[HarmonyPatch(
    typeof(vm.uj),
    nameof(vm.uj.npp)
)]
internal static class AttackDamagePatch_NPP
{
    [HarmonyPostfix]
    private static void Postfix(
        StatType a,
        ref float __result
    )
    {
        ModState.ApplyAttackDamage(
            a,
            ref __result
        );
    }
}


// ----------------------------------------------------------------------------
// iht
// ----------------------------------------------------------------------------

[HarmonyPatch(
    typeof(vm.uj),
    nameof(vm.uj.iht)
)]
internal static class AttackDamagePatch_IHT
{
    [HarmonyPostfix]
    private static void Postfix(
        StatType a,
        ref float __result
    )
    {
        ModState.ApplyAttackDamage(
            a,
            ref __result
        );
    }
}


// ----------------------------------------------------------------------------
// jjj
// ----------------------------------------------------------------------------

[HarmonyPatch(
    typeof(vm.uj),
    nameof(vm.uj.jjj)
)]
internal static class AttackDamagePatch_JJJ
{
    [HarmonyPostfix]
    private static void Postfix(
        StatType a,
        ref float __result
    )
    {
        ModState.ApplyAttackDamage(
            a,
            ref __result
        );
    }
}

// ============================================================================
// REAL HERO DAMAGE PATCH
//
// gut() devuelve el DamageInfo del ataque.
// En nuestra prueba:
// OriginDamage = 6
//
// Solo modificamos Hero.
// Monstruos y otros Unit quedan intactos.
// ============================================================================

// ============================================================================
// HERO BASIC DAMAGE TEST
// ============================================================================

[HarmonyPatch(
    typeof(Unit),
    "gut"
)]
internal static class HeroDamageInfoPatch
{
    [HarmonyPostfix]
    private static void Postfix(
        Unit __instance,
        ref DamageInfo __result
    )
    {
        try
        {
            // gut pertenece al Unit.
            // Solo alteramos el resultado cuando
            // la instancia realmente es un Hero.
            if (!(__instance is Hero))
                return;

            if (!ModState.AttackDamageEnabled)
                return;

            __result.OriginDamage =
                ModState.AttackDamage;
        }
        catch (Exception ex)
        {
            Plugin.PluginLog?.LogWarning(
                $"[TBH] HeroDamageInfoPatch ERROR: {ex.Message}"
            );
        }
    }
}

// ============================================================================
// HERO ATTACK SPEED PATCH
//
// Hero.bsqu = velocidad de ataque runtime.
// En la prueba del Explorador: 1.56
// ============================================================================

[HarmonyPatch]
internal static class HeroAttackSpeedPatch
{
    private static MethodBase TargetMethod()
    {
        PropertyInfo property =
            typeof(Hero).GetProperty(
                "bsqu",
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Instance
            );

        if (property == null)
        {
            throw new MissingMemberException(
                "No se encontró Hero.bsqu"
            );
        }

        MethodInfo getter =
            property.GetGetMethod(true);

        if (getter == null)
        {
            throw new MissingMethodException(
                "No se encontró el getter de Hero.bsqu"
            );
        }

        return getter;
    }


    [HarmonyPostfix]
    private static void Postfix(
        Hero __instance,
        ref float __result
    )
    {
        try
        {
            if (!ModState.AttackSpeedEnabled)
                return;

            if (__instance == null)
                return;

            if (
                ModState.AttackSpeedHeroInstanceId ==
                int.MinValue
            )
            {
                return;
            }

            if (
                __instance.GetInstanceID() !=
                ModState.AttackSpeedHeroInstanceId
            )
            {
                return;
            }

            __result =
                ModState.AttackSpeed;
        }
        catch (Exception ex)
        {
            Plugin.PluginLog?.LogWarning(
                $"[TBH] HeroAttackSpeedPatch ERROR: {ex.Message}"
            );
        }
    }
}


// ============================================================================
// HERO MOVEMENT SPEED PATCH
//
// Unit.bsrq = velocidad de movimiento runtime.
// En la prueba del Explorador: 8.5
//
// El getter pertenece a Unit, pero solamente modificamos el Hero seleccionado.
// ============================================================================

[HarmonyPatch]
internal static class HeroMovementSpeedPatch
{
    private static MethodBase TargetMethod()
    {
        PropertyInfo property =
            typeof(Unit).GetProperty(
                "bsrq",
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Instance
            );

        if (property == null)
        {
            throw new MissingMemberException(
                "No se encontró Unit.bsrq"
            );
        }

        MethodInfo getter =
            property.GetGetMethod(true);

        if (getter == null)
        {
            throw new MissingMethodException(
                "No se encontró el getter de Unit.bsrq"
            );
        }

        return getter;
    }


    [HarmonyPostfix]
    private static void Postfix(
        Unit __instance,
        ref float __result
    )
    {
        try
        {
            if (!ModState.MovementSpeedEnabled)
                return;

            if (!(__instance is Hero))
                return;


            int instanceId =
                __instance.GetInstanceID();


            if (
                !ModState.MovementSpeedByInstance.TryGetValue(
                    instanceId,
                    out float movementSpeed
                )
            )
            {
                return;
            }


            __result =
                movementSpeed;
        }
        catch (Exception ex)
        {
            Plugin.PluginLog?.LogWarning(
                $"[TBH] HeroMovementSpeedPatch ERROR: " +
                $"{ex.Message}"
            );
        }
    }
}
// ============================================================================
// GOD MODE REAL
//
// pj = HealthController runtime del Hero.
//
// bdhg = HP actual
// bdhj = HP máximo
//
// Cuando el juego intenta cambiar HP actual,
// si God Mode está ON sustituimos el nuevo valor
// por HP máximo.
//
// No toca DamageInfo, ataques, proyectiles ni dirección.
// ============================================================================

[HarmonyPatch]
internal static class HeroGodModeHealthPatch
{
    private static MethodBase TargetMethod()
    {
        Type healthType =
            AccessTools.TypeByName(
                "pj"
            );

        if (healthType == null)
        {
            throw new MissingMemberException(
                "No se encontró el tipo runtime pj."
            );
        }

        MethodInfo setter =
            AccessTools.PropertySetter(
                healthType,
                "bdhg"
            );

        if (setter == null)
        {
            throw new MissingMethodException(
                "No se encontró pj.set_bdhg."
            );
        }

        return setter;
    }


    [HarmonyPrefix]
    private static void Prefix(
        object __instance,
        ref float __0
    )
    {
        try
        {
            if (!ModState.GodModeEnabled)
                return;

            if (__instance == null)
                return;

            PropertyInfo maxHealthProperty =
                __instance
                    .GetType()
                    .GetProperty(
                        "bdhj",
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic
                    );

            if (maxHealthProperty == null)
                return;

            object maxValue =
                maxHealthProperty.GetValue(
                    __instance
                );

            if (maxValue == null)
                return;

            float maxHealth =
                Convert.ToSingle(
                    maxValue,
                    CultureInfo.InvariantCulture
                );

            if (
                float.IsNaN(maxHealth) ||
                float.IsInfinity(maxHealth) ||
                maxHealth <= 0f
            )
            {
                return;
            }

            // ========================================================
            // GOD MODE
            // El juego quería establecer otra vida,
            // nosotros obligamos HP máximo.
            // ========================================================

            __0 =
                maxHealth;
        }
        catch (Exception ex)
        {
            Plugin.PluginLog?.LogWarning(
                $"[TBH] GOD MODE HEALTH ERROR: {ex.Message}"
            );
        }
    }

    // ============================================================================
    // MONEY MULTIPLIER TEST
    //
    // vm.tz = runtime de una moneda.
    // iuq(long, EGoldCurrencySource) = candidato fuerte a entrega de oro.
    //
    // Solo multiplicamos valores POSITIVOS.
    // Valores 0 o negativos se dejan intactos.
    // ============================================================================

    [HarmonyPatch(
        typeof(vm.tz),
        "iuq"
    )]
    internal static class MoneyMultiplierPatch
    {
        [HarmonyPrefix]
        private static void Prefix(
            ref long __0,
            EGoldCurrencySource __1
        )
        {
            try
            {
                if (
                    ModState.MoneyMultiplier <=
                    1.0f
                )
                {
                    return;
                }

                // No tocar gastos/reducciones.
                if (__0 <= 0)
                    return;

                long original =
                    __0;

                double calculated =
                    original *
                    (double)ModState.MoneyMultiplier;

                long multiplied;

                if (
                    calculated >=
                    long.MaxValue
                )
                {
                    multiplied =
                        long.MaxValue;
                }
                else
                {
                    multiplied =
                        Math.Max(
                            1L,
                            (long)Math.Round(
                                calculated,
                                MidpointRounding
                                    .AwayFromZero
                            )
                        );
                }

                __0 =
                    multiplied;

                Plugin.PluginLog?.LogInfo(
                    $"[TBH] MONEY TEST -> " +
                    $"{original} x " +
                    $"{ModState.MoneyMultiplier:0.##} " +
                    $"= {multiplied} | " +
                    $"Source={__1}"
                );
            }
            catch (Exception ex)
            {
                Plugin.PluginLog?.LogWarning(
                    $"[TBH] MoneyMultiplierPatch ERROR: " +
                    $"{ex.Message}"
                );
            }
        }
    }
}

}