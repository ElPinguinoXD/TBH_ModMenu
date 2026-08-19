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

using System.Linq;
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
    // ATTACK SPEED - PER HERO
    // ================================================================

    // HeroId -> velocidad que el MOD está imponiendo.
    // Si un HeroId no existe aquí, el juego conserva su valor real.

    public static readonly Dictionary<int, float>
        AttackSpeedOverrides =
            new Dictionary<int, float>();


    // HeroId -> valor REAL calculado por el juego antes del Postfix.

    public static readonly Dictionary<int, float>
        RealAttackSpeedByHero =
            new Dictionary<int, float>();


    // ================================================================
    // MOVEMENT SPEED - PER HERO
    // ================================================================

    // HeroId -> velocidad que el MOD está imponiendo.
    // Este diccionario también es la fuente del LateUpdate físico.

    public static readonly Dictionary<int, float>
        MovementSpeedOverrides =
            new Dictionary<int, float>();


    // HeroId -> valor REAL calculado por el juego antes del Postfix.

    public static readonly Dictionary<int, float>
        RealMovementSpeedByHero =
            new Dictionary<int, float>();

    // ================================================================
    // ORIGINAL LEVEL - SESSION SNAPSHOT
    //
    // Nivel que tenía el héroe cuando esta sesión del mod consiguió
    // leer el save por primera vez.
    // ================================================================

    public static readonly Dictionary<int, int>
        OriginalLevelByHero =
            new Dictionary<int, int>();

    public static readonly Dictionary<int, int>
        OriginalAbilityPointsByHero =
            new Dictionary<int, int>();
    // ================================================================
    // RUNTIME HERO MAP
    //
    // InstanceID de Unity -> HeroId de Taskbar Hero
    // ================================================================

    public static readonly Dictionary<int, int>
        HeroIdByInstanceId =
            new Dictionary<int, int>();


    public static bool TryGetHeroId(
        UnityEngine.Object instance,
        out int heroId
    )
    {
        heroId =
            -1;


        if (instance == null)
        {
            return false;
        }


        // ============================================================
        // PRIMERA OPCIÓN: IDENTIDAD DIRECTA DEL HERO
        //
        // Hero posee su propio "cache" (vh). Ese cache hereda de vo,
        // donde btet representa el HeroKey runtime.
        //
        // Esto es mucho más estable que decidir el héroe únicamente
        // por GetInstanceID(), especialmente cuando Taskbar Hero
        // reconstruye/reutiliza objetos al cambiar personajes.
        // ============================================================

        try
        {
            Hero hero =
                instance as Hero;


            if (
                hero != null &&
                hero.cache != null
            )
            {
                int runtimeHeroId =
                    hero.cache.btet;


                if (runtimeHeroId > 0)
                {
                    heroId =
                        runtimeHeroId;

                    return true;
                }


                // Fallback adicional usando HeroInfoData.

                try
                {
                    if (
                        hero.cache.bflj != null &&
                        hero.cache.bflj.HeroKey > 0
                    )
                    {
                        heroId =
                            hero.cache.bflj.HeroKey;

                        return true;
                    }
                }
                catch
                {
                }
            }
        }
        catch
        {
        }


        // ============================================================
        // FALLBACK: MAPA DE INSTANCE ID
        // ============================================================

        return HeroIdByInstanceId.TryGetValue(
            instance.GetInstanceID(),
            out heroId
        );
    }

    // ================================================================
    // ATTACK DAMAGE - PER HERO
    // ================================================================

    // HeroId -> daño que el MOD está imponiendo.
    // Si el HeroId no está aquí, no alteramos su daño.

    public static readonly Dictionary<int, float>
        AttackDamageOverrides =
            new Dictionary<int, float>();


    // Valor REAL más reciente calculado por el juego.

    public static readonly Dictionary<int, float>
        RealDamageByHero =
            new Dictionary<int, float>();


    // Snapshot estable del daño cuando el mod
    // consiguió leer ese héroe por primera vez.

    public static readonly Dictionary<int, float>
        OriginalDamageByHero =
            new Dictionary<int, float>();


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

    private const string ResetCommandFile =
        @"C:\TBH_ModMenu\reset_command.txt";


    private string lastResetCommand =
        "";


    private float resetCommandTimer =
        0f;


    private const float ResetCommandInterval =
        0.10f;

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

    private SpriteAnimation_Image GetPreviewAnimation(
        SDModelPreview preview,
        string memberName
    )
    {
        if (preview == null)
        {
            return null;
        }


        try
        {
            Type type =
                preview.GetType();


            // ============================================================
            // PRIMERO PROPIEDAD
            //
            // Il2CppInterop suele convertir fields IL2CPP
            // en properties dentro del wrapper generado.
            // ============================================================

            var property =
                type.GetProperty(
                    memberName,
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic
                );


            if (property != null)
            {
                object value =
                    property.GetValue(
                        preview
                    );


                if (
                    value is SpriteAnimation_Image animation
                )
                {
                    return animation;
                }
            }


            // ============================================================
            // FALLBACK FIELD
            // ============================================================

            var field =
                type.GetField(
                    memberName,
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic
                );


            if (field != null)
            {
                object value =
                    field.GetValue(
                        preview
                    );


                if (
                    value is SpriteAnimation_Image animation
                )
                {
                    return animation;
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.PluginLog?.LogWarning(
                $"[TBH] GetPreviewAnimation " +
                $"{memberName} ERROR: {ex.Message}"
            );
        }


        return null;
    }

    private bool ExportSpriteToPng(
        Sprite sprite,
        string outputPath
    )
    {
        RenderTexture previousRenderTexture =
            null;


        RenderTexture renderTexture =
            null;


        Texture2D readableTexture =
            null;


        try
        {
            if (
                sprite == null ||
                sprite.texture == null
            )
            {
                return false;
            }


            Texture2D sourceTexture =
                sprite.texture;


            Rect rect =
                sprite.textureRect;


            int width =
                Mathf.RoundToInt(
                    rect.width
                );


            int height =
                Mathf.RoundToInt(
                    rect.height
                );


            if (
                width <= 0 ||
                height <= 0
            )
            {
                return false;
            }


            // ============================================================
            // COPIAR TEXTURA GPU -> RENDERTEXTURE
            //
            // Esto funciona incluso cuando el Texture2D original
            // no tiene Read/Write Enabled.
            // ============================================================

            renderTexture =
                RenderTexture.GetTemporary(
                    sourceTexture.width,
                    sourceTexture.height,
                    0,
                    RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.Default
                );


            Graphics.Blit(
                sourceTexture,
                renderTexture
            );


            previousRenderTexture =
                RenderTexture.active;


            RenderTexture.active =
                renderTexture;


            readableTexture =
                new Texture2D(
                    width,
                    height,
                    TextureFormat.RGBA32,
                    false
                );


            readableTexture.ReadPixels(
                new Rect(
                    rect.x,
                    rect.y,
                    rect.width,
                    rect.height
                ),
                0,
                0,
                false
            );


            readableTexture.Apply();


            byte[] png =
                ImageConversion.EncodeToPNG(
                    readableTexture
                );


            if (
                png == null ||
                png.Length == 0
            )
            {
                return false;
            }


            File.WriteAllBytes(
                outputPath,
                png
            );


            return true;
        }
        catch (Exception ex)
        {
            Plugin.PluginLog?.LogWarning(
                $"[TBH] ExportSpriteToPng ERROR: " +
                $"{ex.Message}"
            );

            return false;
        }
        finally
        {
            RenderTexture.active =
                previousRenderTexture;


            if (renderTexture != null)
            {
                RenderTexture.ReleaseTemporary(
                    renderTexture
                );
            }


            if (readableTexture != null)
            {
                UnityEngine.Object.Destroy(
                    readableTexture
                );
            }
        }
    }

    private bool ExportHeroAnimationFromPreview(
        SDModelPreview preview,
        int heroId,
        string memberName
    )
    {
        try
        {
            SpriteAnimation_Image animation =
                GetPreviewAnimation(
                    preview,
                    memberName
                );


            if (animation == null)
            {
                Plugin.PluginLog?.LogWarning(
                    $"[TBH] ANIMATION NULL | " +
                    $"Hero={heroId} Field={memberName}"
                );

                return false;
            }


            // ============================================================
            // SPRITES ORIGINALES
            //
            // AssetRipper:
            // private List<Sprite> sprites;
            //
            // No dependemos del nombre obfuscado bsel del dump.
            // ============================================================

            object spriteCollection =
                GetRuntimeMemberValue(
                    animation,
                    "sprites",
                    "bsel"
                );


            if (spriteCollection == null)
            {
                Plugin.PluginLog?.LogWarning(
                    $"[TBH] SPRITE COLLECTION NULL | " +
                    $"Hero={heroId}"
                );

                return false;
            }


            int spriteCount =
                GetRuntimeCollectionCount(
                    spriteCollection
                );


            if (spriteCount <= 0)
            {
                Plugin.PluginLog?.LogWarning(
                    $"[TBH] NO FRAMES | " +
                    $"Hero={heroId}"
                );

                return false;
            }


            string heroDirectory =
                Path.Combine(
                    HeroAnimationsDirectory,
                    heroId.ToString()
                );


            Directory.CreateDirectory(
                heroDirectory
            );


            int exportedFrames =
                0;


            for (
                int i = 0;
                i < spriteCount;
                i++
            )
            {
                object spriteObject =
                    GetRuntimeCollectionItem(
                        spriteCollection,
                        i
                    );


                Sprite sprite =
                    spriteObject as Sprite;


                if (sprite == null)
                {
                    continue;
                }


                string framePath =
                    Path.Combine(
                        heroDirectory,
                        $"{i:D3}.png"
                    );


                if (
                    ExportSpriteToPng(
                        sprite,
                        framePath
                    )
                )
                {
                    exportedFrames++;
                }
            }


            // ============================================================
            // METADATA ORIGINAL
            // ============================================================

            float duration =
                GetRuntimeFloat(
                    animation,
                    "duration",
                    "bsem"
                );


            float speed =
                GetRuntimeFloat(
                    animation,
                    "speed",
                    "bsen"
                );


            bool loop =
                GetRuntimeBool(
                    animation,
                    "loop",
                    "bsep"
                );


            // ============================================================
            // C# 10:
            // NO meter ToString multilínea dentro de $"..."
            // ============================================================

            string durationText =
                duration.ToString(
                    CultureInfo.InvariantCulture
                );


            string speedText =
                speed.ToString(
                    CultureInfo.InvariantCulture
                );


            string metadata =
                "hero=" +
                heroId +
                Environment.NewLine +

                "frames=" +
                exportedFrames +
                Environment.NewLine +

                "duration=" +
                durationText +
                Environment.NewLine +

                "speed=" +
                speedText +
                Environment.NewLine +

                "loop=" +
                (loop ? "1" : "0");


            File.WriteAllText(
                Path.Combine(
                    heroDirectory,
                    "animation.txt"
                ),
                metadata
            );


            Plugin.PluginLog?.LogInfo(
                $"[TBH] ANIMATION EXPORTED | " +
                $"Hero={heroId} " +
                $"Frames={exportedFrames} " +
                $"Duration={duration} " +
                $"Speed={speed} " +
                $"Loop={loop}"
            );


            return exportedFrames > 0;
        }
        catch (Exception ex)
        {
            Plugin.PluginLog?.LogWarning(
                $"[TBH] ExportHeroAnimation " +
                $"Hero={heroId} ERROR: " +
                $"{ex.Message}"
            );


            return false;
        }
    }

    // ========================================================================
    // HERO ANIMATION EXPORT
    // ========================================================================

    private void TryExportHeroAnimations()
    {
        if (heroAnimationsExported)
        {
            return;
        }


        heroAnimationExportTimer +=
            Time.unscaledDeltaTime;


        if (
            heroAnimationExportTimer <
            HeroAnimationExportRetryInterval
        )
        {
            return;
        }


        heroAnimationExportTimer =
            0f;


        try
        {
            SDModelPreview[] previews =
                UnityEngine.Object.FindObjectsOfType<
                    SDModelPreview
                >(
                    true
                );


            if (
                previews == null ||
                previews.Length == 0
            )
            {
                Plugin.PluginLog?.LogWarning(
                    "[TBH] SDModelPreview todavía no disponible."
                );

                return;
            }


            Plugin.PluginLog?.LogInfo(
                $"[TBH] SDModelPreview encontrados: " +
                $"{previews.Length}"
            );


            SDModelPreview preview =
                previews[0];


            Directory.CreateDirectory(
                HeroAnimationsDirectory
            );


            int exported =
                0;


            exported +=
                ExportHeroAnimationFromPreview(
                    preview,
                    101,
                    "m_spriteAnimationKnight"
                )
                ? 1
                : 0;


            exported +=
                ExportHeroAnimationFromPreview(
                    preview,
                    201,
                    "m_spriteAnimationRanger"
                )
                ? 1
                : 0;


            exported +=
                ExportHeroAnimationFromPreview(
                    preview,
                    301,
                    "m_spriteAnimationSorcerer"
                )
                ? 1
                : 0;


            exported +=
                ExportHeroAnimationFromPreview(
                    preview,
                    401,
                    "m_spriteAnimationPriest"
                )
                ? 1
                : 0;


            exported +=
                ExportHeroAnimationFromPreview(
                    preview,
                    501,
                    "m_spriteAnimationHunter"
                )
                ? 1
                : 0;


            exported +=
                ExportHeroAnimationFromPreview(
                    preview,
                    601,
                    "m_spriteAnimationSlayer"
                )
                ? 1
                : 0;


            Plugin.PluginLog?.LogInfo(
                $"[TBH] HERO ANIMATION EXPORT -> " +
                $"{exported}/6"
            );


            // No marcamos como terminado hasta tener los 6.
            // Algunos objetos de UI pueden inicializarse después.

            if (exported >= 6)
            {
                heroAnimationsExported =
                    true;


                Plugin.PluginLog?.LogInfo(
                    "[TBH] ✓ ANIMACIONES DE HEROES EXPORTADAS"
                );
            }
        }
        catch (Exception ex)
        {
            Plugin.PluginLog?.LogWarning(
                $"[TBH] TryExportHeroAnimations ERROR: " +
                $"{ex.Message}"
            );
        }
    }

    private object GetRuntimeMemberValue(
        object target,
        params string[] names
    )
    {
        if (target == null)
        {
            return null;
        }


        Type type =
            target.GetType();


        BindingFlags flags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;


        foreach (string name in names)
        {
            try
            {
                PropertyInfo property =
                    type.GetProperty(
                        name,
                        flags
                    );


                if (property != null)
                {
                    return property.GetValue(
                        target
                    );
                }
            }
            catch
            {
            }


            try
            {
                FieldInfo field =
                    type.GetField(
                        name,
                        flags
                    );


                if (field != null)
                {
                    return field.GetValue(
                        target
                    );
                }
            }
            catch
            {
            }
        }


        return null;
    }


    private int GetRuntimeCollectionCount(
        object collection
    )
    {
        if (collection == null)
        {
            return 0;
        }


        try
        {
            PropertyInfo property =
                collection.GetType().GetProperty(
                    "Count",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic
                );


            if (property == null)
            {
                return 0;
            }


            object value =
                property.GetValue(
                    collection
                );


            if (value == null)
            {
                return 0;
            }


            return Convert.ToInt32(
                value
            );
        }
        catch
        {
            return 0;
        }
    }


    private object GetRuntimeCollectionItem(
        object collection,
        int index
    )
    {
        if (collection == null)
        {
            return null;
        }


        try
        {
            Type type =
                collection.GetType();


            PropertyInfo property =
                type.GetProperty(
                    "Item",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic
                );


            if (property != null)
            {
                return property.GetValue(
                    collection,
                    new object[]
                    {
                        index
                    }
                );
            }


            MethodInfo method =
                type.GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic
                )
                .FirstOrDefault(
                    m =>
                        m.Name == "get_Item" &&
                        m.GetParameters().Length == 1
                );


            if (method != null)
            {
                return method.Invoke(
                    collection,
                    new object[]
                    {
                        index
                    }
                );
            }
        }
        catch
        {
        }


        return null;
    }


    private float GetRuntimeFloat(
        object target,
        params string[] names
    )
    {
        try
        {
            object value =
                GetRuntimeMemberValue(
                    target,
                    names
                );


            if (value == null)
            {
                return 0f;
            }


            return Convert.ToSingle(
                value,
                CultureInfo.InvariantCulture
            );
        }
        catch
        {
            return 0f;
        }
    }


    private bool GetRuntimeBool(
        object target,
        params string[] names
    )
    {
        try
        {
            object value =
                GetRuntimeMemberValue(
                    target,
                    names
                );


            if (value == null)
            {
                return false;
            }


            return Convert.ToBoolean(
                value
            );
        }
        catch
        {
            return false;
        }
    }

    // ========================================================================
    // HERO IDS
    // ========================================================================

    private static readonly int[] KnownHeroIds =
    {
        101,
        201,
        301,
        401,
        501,
        601
    };

    // ========================================================================
    // HERO RUNTIME STATE
    // ========================================================================

    private const string HeroRuntimeStateFile =
        @"C:\TBH_ModMenu\hero_runtime_state.txt";


    private float heroRuntimeStateTimer =
        0f;


    private const float HeroRuntimeStateInterval =
        0.50f;


    private bool heroRuntimeStateLogged =
        false;

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

    private string ReadExistingCommand(
        string path
    )
    {
        try
        {
            if (!File.Exists(path))
            {
                return "";
            }


            return File.ReadAllText(
                path
            ).Trim();
        }
        catch
        {
            return "";
        }
    }


    private void InitializeCommandBaselines()
    {
        lastHeroCommand =
            ReadExistingCommand(
                HeroCommandFile
            );


        lastDamageCommand =
            ReadExistingCommand(
                DamageCommandFile
            );


        lastAttackSpeedCommand =
            ReadExistingCommand(
                AttackSpeedCommandFile
            );


        lastMovementSpeedCommand =
            ReadExistingCommand(
                MovementSpeedCommandFile
            );
        
        lastResetCommand =
            ReadExistingCommand(
                ResetCommandFile
            );


        Plugin.PluginLog?.LogInfo(
            "[TBH] Command baselines initialized."
        );
    }

    // ========================================================================
    // GAME SPEED
    // ========================================================================

    private float desiredSpeed = 1.0f;

    private float speedReadTimer = 0f;

    private const float SpeedReadInterval = 0.10f;


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
    // HERO ANIMATION EXPORT
    // ========================================================================

    private const string HeroAnimationsDirectory =
        @"C:\TBH_ModMenu\HeroAnimations";


    private bool heroAnimationsExported =
        false;


    private float heroAnimationExportTimer =
        0f;


    private const float HeroAnimationExportRetryInterval =
        2.0f;


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


            // ============================================================
            // IGNORAR COMANDOS DE LA SESIÓN ANTERIOR
            // ============================================================

            InitializeCommandBaselines();


            // ============================================================
            // GOD MODE
            // ============================================================

            if (!File.Exists(GodModeFile))
            {
                File.WriteAllText(
                    GodModeFile,
                    "0"
                );
            }

            ReadGodMode();


            // ============================================================
            // MONEY MULTIPLIER
            // ============================================================

            if (!File.Exists(MoneyMultiplierFile))
            {
                File.WriteAllText(
                    MoneyMultiplierFile,
                    "1.0"
                );
            }

            ReadMoneyMultiplier();


            // ============================================================
            // GAME SPEED
            // ============================================================

            if (!File.Exists(SpeedFile))
            {
                File.WriteAllText(
                    SpeedFile,
                    "1.0"
                );
            }


            // ============================================================
            // ARCHIVOS LEGACY QUE FORM1 TODAVÍA USA
            //
            // El plugin ya NO aplica daño leyendo attackdamage.txt.
            // Damage se aplica únicamente mediante damage_command.txt
            // y AttackDamageOverrides por HeroId.
            // ============================================================

            if (!File.Exists(AttackDamageFile))
            {
                File.WriteAllText(
                    AttackDamageFile,
                    "50"
                );
            }


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


            Plugin.PluginLog?.LogInfo(
                "[TBH] SpeedController inicializado."
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

        UpdateHeroCommand();

        UpdateDamageCommand();

        UpdateResetCommand();

        UpdateHeroSpeedCommands();

        UpdateHeroSpeedTargets();

        TryDumpHeroes();

        TryExportHeroAnimations();

        UpdateHeroRuntimeState();

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
    // ATTACK DAMAGE
    //
    // El sistema global antiguo fue eliminado.
    // Damage ahora se procesa exclusivamente por HeroId mediante
    // UpdateDamageCommand() + ModState.AttackDamageOverrides.
    // ========================================================================


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

                // ============================================================
                // SNAPSHOT DEL NIVEL ORIGINAL DE ESTA SESIÓN
                // ============================================================

                if (
                    !ModState.OriginalLevelByHero.ContainsKey(
                        hero.heroKey
                    )
                )
                {
                    ModState.OriginalLevelByHero[
                        hero.heroKey
                    ] =
                        hero.HeroLevel;

                    Plugin.PluginLog?.LogInfo(
                        $"[TBH] ORIGINAL LEVEL SNAPSHOT | " +
                        $"Hero={hero.heroKey} " +
                        $"Level={hero.HeroLevel}"
                    );
                }

                if (
                    !ModState.OriginalAbilityPointsByHero.ContainsKey(
                        hero.heroKey
                    )
                )
                {
                    ModState.OriginalAbilityPointsByHero[
                        hero.heroKey
                    ] =
                        hero.AbilityPoint;


                    Plugin.PluginLog?.LogInfo(
                        $"[TBH] ORIGINAL ABILITY SNAPSHOT | " +
                        $"Hero={hero.heroKey} " +
                        $"AbilityPoints={hero.AbilityPoint}"
                    );
                }

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

    private void RestoreHeroOriginalLevel(
        int heroId
    )
    {
        try
        {
            if (
                !ModState.OriginalLevelByHero.TryGetValue(
                    heroId,
                    out int originalLevel
                )
            )
            {
                Plugin.PluginLog?.LogWarning(
                    $"[TBH] RESET LEVEL -> " +
                    $"No existe snapshot para Hero={heroId}"
                );

                return;
            }


            var manager =
                bbl.bspl;


            if (manager == null)
            {
                return;
            }


            PlayerSaveData save =
                manager.btou;


            if (
                save == null ||
                save.heroSaveDatas == null
            )
            {
                return;
            }


            HeroSaveData saveHero =
                null;


            for (
                int i = 0;
                i < save.heroSaveDatas.Count;
                i++
            )
            {
                HeroSaveData candidate =
                    save.heroSaveDatas[i];


                if (
                    candidate != null &&
                    candidate.heroKey == heroId
                )
                {
                    saveHero =
                        candidate;

                    break;
                }
            }


            if (saveHero == null)
            {
                return;
            }


            int originalAbilityPoints =
                saveHero.AbilityPoint;


            ModState.OriginalAbilityPointsByHero.TryGetValue(
                heroId,
                out originalAbilityPoints
            );


            // ============================================================
            // RESTAURAR SAVE
            // ============================================================

            saveHero.HeroLevel =
                originalLevel;


            saveHero.AbilityPoint =
                originalAbilityPoints;


            // ============================================================
            // RESTAURAR RUNTIME CACHE
            // ============================================================

            try
            {
                vo cache =
                    vm.uj.ivu(
                        heroId
                    );


                if (cache != null)
                {
                    cache.bfmc =
                        originalLevel;


                    cache.bfme =
                        originalAbilityPoints;
                }
            }
            catch
            {
            }


            Plugin.PluginLog?.LogInfo(
                $"[TBH] LEVEL RESET | " +
                $"Hero={heroId} " +
                $"Level={originalLevel} " +
                $"AbilityPoints={originalAbilityPoints}"
            );
        }
        catch (Exception ex)
        {
            Plugin.PluginLog?.LogWarning(
                $"[TBH] RestoreHeroOriginalLevel ERROR: " +
                $"{ex.Message}"
            );
        }
    }

    private void ResetHeroModifications(
        int heroId
    )
    {
        try
        {
            // ============================================================
            // DAMAGE
            // ============================================================

            ModState.AttackDamageOverrides.Remove(
                heroId
            );


            // ============================================================
            // ATTACK SPEED
            // ============================================================

            ModState.AttackSpeedOverrides.Remove(
                heroId
            );


            // ============================================================
            // MOVEMENT SPEED
            // ============================================================

            ModState.MovementSpeedOverrides.Remove(
                heroId
            );


            // Limpiar seguimiento físico del movimiento.

            lastHeroPositions.Remove(
                heroId
            );


            initializedMovementHeroes.Remove(
                heroId
            );


            // ============================================================
            // LEVEL
            // ============================================================

            RestoreHeroOriginalLevel(
                heroId
            );


            // ============================================================
            // FORZAR REFRESH
            // ============================================================

            UpdateHeroRuntimeMap(
                heroId
            );


            RefreshHeroSpeeds(
                heroId
            );


            Plugin.PluginLog?.LogInfo(
                $"[TBH] RESET HERO COMPLETE | " +
                $"Hero={heroId}"
            );
        }
        catch (Exception ex)
        {
            Plugin.PluginLog?.LogWarning(
                $"[TBH] ResetHeroModifications ERROR: " +
                $"{ex.Message}"
            );
        }
    }

    private void ResetAllModifications()
    {
        try
        {
            // ============================================================
            // HEROES
            // ============================================================

            foreach (
                int heroId
                in KnownHeroIds
            )
            {
                ModState.AttackDamageOverrides.Remove(
                    heroId
                );


                ModState.AttackSpeedOverrides.Remove(
                    heroId
                );


                ModState.MovementSpeedOverrides.Remove(
                    heroId
                );


                lastHeroPositions.Remove(
                    heroId
                );


                initializedMovementHeroes.Remove(
                    heroId
                );


                RestoreHeroOriginalLevel(
                    heroId
                );
            }


            // ============================================================
            // GAME SPEED
            // ============================================================

            desiredSpeed =
                1.0f;


            Time.timeScale =
                1.0f;


            File.WriteAllText(
                SpeedFile,
                "1.0"
            );


            // ============================================================
            // GOD MODE
            // ============================================================

            ModState.GodModeEnabled =
                false;


            File.WriteAllText(
                GodModeFile,
                "0"
            );


            // ============================================================
            // MONEY
            // ============================================================

            ModState.MoneyMultiplier =
                1.0f;


            File.WriteAllText(
                MoneyMultiplierFile,
                "1.0"
            );


            Plugin.PluginLog?.LogInfo(
                "[TBH] RESET ALL COMPLETE"
            );
        }
        catch (Exception ex)
        {
            Plugin.PluginLog?.LogWarning(
                $"[TBH] ResetAllModifications ERROR: " +
                $"{ex.Message}"
            );
        }
    }

    private void UpdateResetCommand()
    {
        resetCommandTimer +=
            Time.unscaledDeltaTime;


        if (
            resetCommandTimer <
            ResetCommandInterval
        )
        {
            return;
        }


        resetCommandTimer =
            0f;


        try
        {
            if (!File.Exists(ResetCommandFile))
            {
                return;
            }


            string command =
                File.ReadAllText(
                    ResetCommandFile
                ).Trim();


            if (
                string.IsNullOrWhiteSpace(
                    command
                ) ||
                command ==
                lastResetCommand
            )
            {
                return;
            }


            lastResetCommand =
                command;


            string[] parts =
                command.Split('|');


            if (parts.Length < 1)
            {
                return;
            }


            // ============================================================
            // RESET ALL
            // ============================================================

            if (
                string.Equals(
                    parts[0],
                    "all",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                ResetAllModifications();

                return;
            }


            // ============================================================
            // RESET HERO
            //
            // Formato:
            //
            // hero|201|timestamp
            // ============================================================

            if (
                parts.Length >= 2 &&
                string.Equals(
                    parts[0],
                    "hero",
                    StringComparison.OrdinalIgnoreCase
                ) &&
                int.TryParse(
                    parts[1],
                    out int heroId
                )
            )
            {
                ResetHeroModifications(
                    heroId
                );
            }
        }
        catch (Exception ex)
        {
            Plugin.PluginLog?.LogWarning(
                $"[TBH] UpdateResetCommand ERROR: " +
                $"{ex.Message}"
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
            $"[TBH] DAMAGE OVERRIDES COUNT = " +
            $"{ModState.AttackDamageOverrides.Count}"
        );


        foreach (
            KeyValuePair<int, float> pair
            in ModState.AttackDamageOverrides
        )
        {
            Plugin.PluginLog?.LogInfo(
                $"[TBH] DAMAGE OVERRIDE | " +
                $"Hero={pair.Key} Value={pair.Value}"
            );
        }


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

           ModState.AttackDamageOverrides[
                heroId
            ] =
                damage;

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

            ModState.AttackSpeedOverrides[
                heroId
            ] =
                value;

                lastAttackSpeedCommand =
                    command;

            File.WriteAllText(
                AttackSpeedFile,
                value.ToString(
                    CultureInfo.InvariantCulture
                )
            );

           UpdateHeroRuntimeMap(
                heroId
            );

            Plugin.PluginLog?.LogInfo(
                $"[TBH] ATTACK SPEED -> " +
                $"Hero={heroId} Value={value}"
            );

            RefreshHeroSpeeds(
                heroId
            );


            LogHeroSpeedOverrides();
        }
        catch (Exception ex)
        {
            Plugin.PluginLog?.LogWarning(
                $"[TBH] AttackSpeed command ERROR: {ex.Message}"
            );
        }
    }


    private void LogHeroSpeedOverrides()
    {
        try
        {
            Plugin.PluginLog?.LogInfo(
                $"[TBH] ATTACK OVERRIDES COUNT = " +
                $"{ModState.AttackSpeedOverrides.Count}"
            );


            foreach (
                KeyValuePair<int, float> pair
                in ModState.AttackSpeedOverrides
            )
            {
                Plugin.PluginLog?.LogInfo(
                    $"[TBH] ATTACK OVERRIDE | " +
                    $"Hero={pair.Key} Value={pair.Value}"
                );
            }


            Plugin.PluginLog?.LogInfo(
                $"[TBH] MOVEMENT OVERRIDES COUNT = " +
                $"{ModState.MovementSpeedOverrides.Count}"
            );


            foreach (
                KeyValuePair<int, float> pair
                in ModState.MovementSpeedOverrides
            )
            {
                Plugin.PluginLog?.LogInfo(
                    $"[TBH] MOVEMENT OVERRIDE | " +
                    $"Hero={pair.Key} Value={pair.Value}"
                );
            }
        }
        catch
        {
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

            ModState.MovementSpeedOverrides[
                heroId
            ] =
                value;


            // Reiniciar solamente el seguimiento físico de ESTE héroe.
            // El siguiente LateUpdate tomará como origen su posición actual.

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

            UpdateHeroRuntimeMap(
                heroId
            );

            Plugin.PluginLog?.LogInfo(
                $"[TBH] MOVEMENT SPEED -> " +
                $"Hero={heroId} Value={value}"
            );

            RefreshHeroSpeeds(
                heroId
            );


            LogHeroSpeedOverrides();
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


        heroSpeedTargetTimer =
            0f;


        RefreshAllHeroRuntimeMaps();
    }

    // ========================================================================
    // REFRESH ALL HERO RUNTIME MAPS
    // ========================================================================

    private void RefreshAllHeroRuntimeMaps()
    {
        try
        {
            // Los Hero pueden ser recreados cuando cambia una escena.
            // Reconstruimos el mapa InstanceID -> HeroId.

            ModState.HeroIdByInstanceId.Clear();


            foreach (
                int heroId
                in KnownHeroIds
            )
            {
                UpdateHeroRuntimeMap(
                    heroId
                );
            }
        }
        catch (Exception ex)
        {
            Plugin.PluginLog?.LogWarning(
                $"[TBH] RefreshAllHeroRuntimeMaps ERROR: {ex.Message}"
            );
        }
    }


    // ========================================================================
    // UPDATE ONE HERO RUNTIME MAP
    // ========================================================================

    private void UpdateHeroRuntimeMap(
        int heroId
    )
    {
        try
        {
            Hero hero =
                GetRuntimeHero(
                    heroId
                );


            if (hero == null)
            {
                return;
            }


            int actualHeroId =
                heroId;


            // Confirmar la identidad desde el propio objeto Hero.
            // Si por alguna razón vm.uj.ivu(heroId).bflu apunta a un
            // objeto cuyo cache pertenece a otro héroe, NO dejamos
            // que el mapa lo etiquete incorrectamente.

            try
            {
                if (
                    hero.cache != null &&
                    hero.cache.btet > 0
                )
                {
                    actualHeroId =
                        hero.cache.btet;
                }
            }
            catch
            {
            }


            int instanceId =
                hero.GetInstanceID();


            ModState.HeroIdByInstanceId[
                instanceId
            ] =
                actualHeroId;
        }
        catch
        {
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

    // ========================================================================
    // HERO RUNTIME STATE
    // ========================================================================

    private void UpdateHeroRuntimeState()
    {
        heroRuntimeStateTimer +=
            Time.unscaledDeltaTime;


        if (
            heroRuntimeStateTimer <
            HeroRuntimeStateInterval
        )
        {
            return;
        }


        heroRuntimeStateTimer =
            0f;


        try
        {
            var manager =
                bbl.bspl;


            if (manager == null)
            {
                return;
            }


            PlayerSaveData save =
                manager.btou;


            if (
                save == null ||
                save.heroSaveDatas == null
            )
            {
                return;
            }


            List<string> lines =
                new List<string>();


            // ============================================================
            // HEADER
            // ============================================================

            lines.Add(
                "#HeroId|Level|OriginalLevel|Unlocked|" +
                "OriginalDamage|DamageOverride|" +
                "RealAttackSpeed|AttackOverride|" +
                "RealMovementSpeed|MovementOverride"
            );


            // ============================================================
            // HEROES
            // ============================================================

            foreach (
                int heroId
                in KnownHeroIds
            )
            {
                HeroSaveData saveHero =
                    null;


                // --------------------------------------------------------
                // LOCALIZAR SAVE DEL HÉROE
                // --------------------------------------------------------

                for (
                    int i = 0;
                    i < save.heroSaveDatas.Count;
                    i++
                )
                {
                    HeroSaveData candidate =
                        save.heroSaveDatas[i];


                    if (
                        candidate != null &&
                        candidate.heroKey == heroId
                    )
                    {
                        saveHero =
                            candidate;

                        break;
                    }
                }


                if (saveHero == null)
                {
                    continue;
                }


                // --------------------------------------------------------
                // SNAPSHOT ORIGINAL
                // --------------------------------------------------------

                if (
                    !ModState.OriginalLevelByHero.ContainsKey(
                        heroId
                    )
                )
                {
                    ModState.OriginalLevelByHero[
                        heroId
                    ] =
                        saveHero.HeroLevel;
                }


                // --------------------------------------------------------
                // FORZAR LECTURA DE VALORES RUNTIME
                //
                // Los Harmony Postfix guardan el valor REAL antes de
                // sustituirlo por un override.
                // --------------------------------------------------------

                Hero runtimeHero =
                    GetRuntimeHero(
                        heroId
                    );


                if (runtimeHero != null)
                {
                    try
                    {
                        // Estos getters son seguros para refrescar
                        // Attack Speed y Movement Speed.

                        float attackProbe =
                            runtimeHero.bsqu;

                        float movementProbe =
                            runtimeHero.bsrq;


                        // Damage solamente se sondea mientras todavía
                        // no tengamos el snapshot inicial.
                        //
                        // Una vez conseguido, NO volvemos a llamar gut()
                        // desde el monitor de estado.

                        if (
                            !ModState.OriginalDamageByHero.ContainsKey(
                                heroId
                            )
                        )
                        {
                            DamageInfo damageProbe =
                                runtimeHero.gut();
                        }
                    }
                    catch
                    {
                    }
                }


                // --------------------------------------------------------
                // NIVEL
                // --------------------------------------------------------

                int originalLevel =
                    saveHero.HeroLevel;


                if (
                    ModState.OriginalLevelByHero.TryGetValue(
                        heroId,
                        out int storedOriginalLevel
                    )
                )
                {
                    originalLevel =
                        storedOriginalLevel;
                }


                // --------------------------------------------------------
                // Original Damage
                // --------------------------------------------------------

                string realDamage =
                    "NA";


                if (
                    ModState.OriginalDamageByHero.TryGetValue(
                        heroId,
                        out float realDamageValue
                    )
                )
                {
                    realDamage =
                        realDamageValue.ToString(
                            "0.####",
                            CultureInfo.InvariantCulture
                        );
                }


                // --------------------------------------------------------
                // DAMAGE MOD
                // --------------------------------------------------------

                string damageOverride =
                    "OFF";


                if (
                    ModState.AttackDamageOverrides.TryGetValue(
                        heroId,
                        out float damageOverrideValue
                    )
                )
                {
                    damageOverride =
                        damageOverrideValue.ToString(
                            "0.####",
                            CultureInfo.InvariantCulture
                        );
                }


                // --------------------------------------------------------
                // ATTACK SPEED REAL
                // --------------------------------------------------------

                string realAttackSpeed =
                    "NA";


                if (
                    ModState.RealAttackSpeedByHero.TryGetValue(
                        heroId,
                        out float realAttackValue
                    )
                )
                {
                    realAttackSpeed =
                        realAttackValue.ToString(
                            "0.####",
                            CultureInfo.InvariantCulture
                        );
                }


                // --------------------------------------------------------
                // ATTACK SPEED MOD
                // --------------------------------------------------------

                string attackOverride =
                    "OFF";


                if (
                    ModState.AttackSpeedOverrides.TryGetValue(
                        heroId,
                        out float attackOverrideValue
                    )
                )
                {
                    attackOverride =
                        attackOverrideValue.ToString(
                            "0.####",
                            CultureInfo.InvariantCulture
                        );
                }


                // --------------------------------------------------------
                // MOVEMENT REAL
                // --------------------------------------------------------

                string realMovementSpeed =
                    "NA";


                if (
                    ModState.RealMovementSpeedByHero.TryGetValue(
                        heroId,
                        out float realMovementValue
                    )
                )
                {
                    realMovementSpeed =
                        realMovementValue.ToString(
                            "0.####",
                            CultureInfo.InvariantCulture
                        );
                }


                // --------------------------------------------------------
                // MOVEMENT MOD
                // --------------------------------------------------------

                string movementOverride =
                    "OFF";


                if (
                    ModState.MovementSpeedOverrides.TryGetValue(
                        heroId,
                        out float movementOverrideValue
                    )
                )
                {
                    movementOverride =
                        movementOverrideValue.ToString(
                            "0.####",
                            CultureInfo.InvariantCulture
                        );
                }


                // --------------------------------------------------------
                // LINE
                // --------------------------------------------------------

                string line =
                    $"{heroId}|" +
                    $"{saveHero.HeroLevel}|" +
                    $"{originalLevel}|" +
                    $"{(saveHero.IsUnLock ? 1 : 0)}|" +
                    $"{realDamage}|" +
                    $"{damageOverride}|" +
                    $"{realAttackSpeed}|" +
                    $"{attackOverride}|" +
                    $"{realMovementSpeed}|" +
                    $"{movementOverride}";


                lines.Add(
                    line
                );
            }


            // ============================================================
            // ESCRITURA SEGURA
            // ============================================================

            string tempFile =
                HeroRuntimeStateFile +
                ".tmp";


            File.WriteAllLines(
                tempFile,
                lines
            );


            File.Copy(
                tempFile,
                HeroRuntimeStateFile,
                true
            );


            File.Delete(
                tempFile
            );


            if (!heroRuntimeStateLogged)
            {
                heroRuntimeStateLogged =
                    true;


                Plugin.PluginLog?.LogInfo(
                    "[TBH] hero_runtime_state.txt listo."
                );
            }
        }
        catch (Exception ex)
        {
            Plugin.PluginLog?.LogWarning(
                $"[TBH] UpdateHeroRuntimeState ERROR: " +
                $"{ex.Message}"
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
            ModState.MovementSpeedOverrides.Count == 0
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
                ModState.MovementSpeedOverrides
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

            float nativeMovementSpeed =
                NativeMovementSpeed;


            // Si el Harmony Postfix ya capturó el valor real de este héroe,
            // lo usamos como base en lugar del 8.5 fijo.
            //
            // Esto permite respetar diferencias reales entre héroes/buffs
            // sin perder el sistema físico de movimiento que ya funcionaba.

            if (
                ModState.RealMovementSpeedByHero.TryGetValue(
                    heroId,
                    out float capturedRealMovementSpeed
                )
                &&
                capturedRealMovementSpeed > 0.001f
            )
            {
                nativeMovementSpeed =
                    capturedRealMovementSpeed;
            }


            float multiplier =
                desiredMovementSpeed /
                nativeMovementSpeed;


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
// ============================================================================

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
            if (!(__instance is Hero))
            {
                return;
            }


            if (
                !ModState.TryGetHeroId(
                    __instance,
                    out int heroId
                )
            )
            {
                return;
            }


            // ====================================================
            // DAÑO REAL
            //
            // Aquí todavía tenemos el resultado calculado
            // originalmente por Taskbar Hero.
            // ====================================================

            float realDamage =
                __result.OriginDamage;


            ModState.RealDamageByHero[
                heroId
            ] =
                realDamage;


            // ====================================================
            // SNAPSHOT ORIGINAL
            //
            // Solo se guarda UNA VEZ.
            // No cambia aunque después existan buffs,
            // ataques especiales, etc.
            // ====================================================

            if (
                realDamage > 0f &&
                !ModState.OriginalDamageByHero.ContainsKey(
                    heroId
                )
            )
            {
                ModState.OriginalDamageByHero[
                    heroId
                ] =
                    realDamage;


                Plugin.PluginLog?.LogInfo(
                    $"[TBH] ORIGINAL DAMAGE SNAPSHOT | " +
                    $"Hero={heroId} Damage={realDamage}"
                );
            }


            // ====================================================
            // SIN OVERRIDE
            //
            // Si este héroe no está en el Dictionary,
            // dejamos el resultado original intacto.
            // ====================================================

            if (
                !ModState.AttackDamageOverrides.TryGetValue(
                    heroId,
                    out float overrideDamage
                )
            )
            {
                return;
            }


            // ====================================================
            // APLICAR MOD
            // ====================================================

            __result.OriginDamage =
                overrideDamage;
        }
        catch (Exception ex)
        {
            Plugin.PluginLog?.LogWarning(
                $"[TBH] HeroDamageInfoPatch ERROR: " +
                $"{ex.Message}"
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
            if (__instance == null)
            {
                return;
            }


            if (
                !ModState.TryGetHeroId(
                    __instance,
                    out int heroId
                )
            )
            {
                return;
            }


            // ====================================================
            // GUARDAR VALOR REAL
            //
            // En este punto __result todavía contiene lo que
            // Taskbar Hero calculó originalmente.
            // ====================================================

            float realValue =
                __result;


            ModState.RealAttackSpeedByHero[
                heroId
            ] =
                realValue;


            // ====================================================
            // ¿EXISTE OVERRIDE PARA ESTE HÉROE?
            // ====================================================

            if (
                !ModState.AttackSpeedOverrides.TryGetValue(
                    heroId,
                    out float overrideValue
                )
            )
            {
                // Ningún cambio activo.
                // Dejamos el resultado original intacto.

                return;
            }


            // ====================================================
            // APLICAR MOD
            // ====================================================

            __result =
                overrideValue;
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
// El getter pertenece a Unit, pero aplicamos el override correspondiente a cada Hero.
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
            if (!(__instance is Hero))
            {
                return;
            }


            if (
                !ModState.TryGetHeroId(
                    __instance,
                    out int heroId
                )
            )
            {
                return;
            }


            // ====================================================
            // VALOR REAL DEL JUEGO
            // ====================================================

            float realValue =
                __result;


            ModState.RealMovementSpeedByHero[
                heroId
            ] =
                realValue;


            // ====================================================
            // SIN OVERRIDE = COMPORTAMIENTO ORIGINAL
            // ====================================================

            if (
                !ModState.MovementSpeedOverrides.TryGetValue(
                    heroId,
                    out float overrideValue
                )
            )
            {
                return;
            }


            // ====================================================
            // APLICAR MOD
            // ====================================================

            __result =
                overrideValue;
        }
        catch (Exception ex)
        {
            Plugin.PluginLog?.LogWarning(
                $"[TBH] HeroMovementSpeedPatch ERROR: {ex.Message}"
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