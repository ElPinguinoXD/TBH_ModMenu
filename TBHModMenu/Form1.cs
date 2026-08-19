using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Collections.Generic;

namespace TBHModMenu;

public class Form1 : Form
{
    // ============================================================
    // WIN32
    // ============================================================

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(
        int vKey);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(
        IntPtr hWnd,
        int Msg,
        IntPtr wParam,
        IntPtr lParam
    );

private const int WM_NCLBUTTONDOWN = 0x00A1;
private const int HTCAPTION = 0x0002;


    // ============================================================
    // CONSTANTES
    // ============================================================

    private const int VK_F1 = 0x70;

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

    private const string AttackSpeedFile =
        @"C:\TBH_ModMenu\attackspeed.txt";

    private const string AttackSpeedCommandFile =
        @"C:\TBH_ModMenu\attackspeed_command.txt";

    private const string MovementSpeedFile =
        @"C:\TBH_ModMenu\movementspeed.txt";

    private const string MovementSpeedCommandFile =
        @"C:\TBH_ModMenu\movementspeed_command.txt";

    private const string GodModeFile =
        @"C:\TBH_ModMenu\godmode.txt";

    private const string MoneyMultiplierFile =
        @"C:\TBH_ModMenu\moneymultiplier.txt";

    private const string HeroRuntimeStateFile =
    @"C:\TBH_ModMenu\hero_runtime_state.txt";

    private const string ResetCommandFile =
        @"C:\TBH_ModMenu\reset_command.txt";

    private const string HeroAnimationsDirectory =
    @"C:\TBH_ModMenu\HeroAnimations";

    // ============================================================
    // PALETA MORADA
    // ============================================================

    private readonly Color backgroundColor =
        Color.FromArgb(12, 9, 18);

    private readonly Color headerColor =
        Color.FromArgb(20, 14, 30);

    private readonly Color cardColor =
        Color.FromArgb(27, 20, 39);

    private readonly Color cardBorderColor =
        Color.FromArgb(72, 49, 102);

    private readonly Color inputColor =
        Color.FromArgb(37, 28, 51);

    private readonly Color inputHoverColor =
        Color.FromArgb(48, 36, 66);

    private readonly Color accentColor =
        Color.FromArgb(151, 82, 255);

    private readonly Color accentHoverColor =
        Color.FromArgb(178, 118, 255);

    private readonly Color accentDarkColor =
        Color.FromArgb(105, 53, 190);

    private readonly Color textColor =
        Color.FromArgb(244, 240, 250);

    private readonly Color secondaryTextColor =
        Color.FromArgb(166, 151, 183);

    private readonly Color successColor =
        Color.FromArgb(112, 235, 174);

    private readonly Color dangerColor =
        Color.FromArgb(255, 105, 135);

    // ============================================================
    // RUNTIME
    // ============================================================

    private readonly System.Windows.Forms.Timer updateTimer;

    private Process? gameProcess;

    private bool menuVisible = true;

    private bool f1WasPressed;

    private readonly Dictionary<int, HeroRuntimeState>
    heroRuntimeStates =
        new Dictionary<int, HeroRuntimeState>();

    private DateTime lastHeroRuntimeStateRead =
        DateTime.MinValue;

    private const int HeroRuntimeStateRefreshMs =
        250;

    private bool heroRuntimeStateLoaded =
        false;
    // ============================================================
    // UI
    // ============================================================

    private readonly Label gameStatusLabel;

    private readonly Label speedValueLabel;

    private readonly TrackBar speedSlider;

    private readonly ComboBox heroComboBox;

    private readonly NumericUpDown heroLevelInput;

    private readonly NumericUpDown damageInput;

    private readonly NumericUpDown attackSpeedInput;

    private readonly NumericUpDown movementSpeedInput;

    private readonly CheckBox godModeCheckBox;

    private readonly NumericUpDown moneyMultiplierInput;

    private readonly Label resultLabel;

    private FlowLayoutPanel heroVisualPanel = null!;
    private readonly Dictionary<int, Panel> heroVisualCards = new();
    private readonly Dictionary<int, PictureBox> heroVisualPictures = new();
    private readonly Dictionary<int, Label> heroVisualLabels = new();

    private int selectedHeroKeyVisual = 201;

    // ============================================================
    // HERO VISUAL ANIMATIONS
    // ============================================================

    private readonly Dictionary<int, List<Image>>
        heroAnimationFrames =
            new Dictionary<int, List<Image>>();


    private System.Windows.Forms.Timer? heroAnimationTimer;


    private int heroAnimationFrameIndex =
        0;


    private bool heroAnimationsLoaded =
        false;


    private DateTime lastHeroAnimationLoadAttempt =
        DateTime.MinValue;


    private const int HeroAnimationLoadRetryMs =
        500;
    
    // ============================================================
    // CONSTRUCTOR
    // ============================================================

    public Form1()
    {
        Directory.CreateDirectory(ModDirectory);

        // ========================================================
        // FORM
        // ========================================================

        Text = "TBH Mod Menu";

        FormBorderStyle =
            FormBorderStyle.None;

        StartPosition =
            FormStartPosition.CenterScreen;

        ShowInTaskbar = false;

        TopMost = true;

        BackColor =
            backgroundColor;

        Opacity = 0.97;

        Width = 390;
        Height = 835;

        // ========================================================
        // HEADER
        // ========================================================

        var header = new Panel
        {
            BackColor =
                headerColor,

            Location =
                new Point(0, 0),

            Size =
                new Size(390, 72)
        };

        Controls.Add(header);

        // ========================================================
        // MOVER VENTANA DESDE EL HEADER
        // ========================================================

        header.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left)
                return;

            ReleaseCapture();

            SendMessage(
                Handle,
                WM_NCLBUTTONDOWN,
                new IntPtr(HTCAPTION),
                IntPtr.Zero
            );
        };

        var topAccent = new Panel
        {
            BackColor =
                accentColor,

            Location =
                new Point(0, 0),

            Size =
                new Size(390, 3)
        };

        header.Controls.Add(topAccent);

        var title = new Label
        {
            Text =
                "TASKBAR HERO MOD",

            ForeColor =
                Color.White,

            Font =
                new Font(
                    "Segoe UI",
                    15,
                    FontStyle.Bold
                ),

            AutoSize = true,

            Location =
                new Point(22, 15)
        };

        header.Controls.Add(title);

        var subtitle = new Label
        {
            Text =
                "PengX Runtime Tools",

            ForeColor =
                accentHoverColor,

            Font =
                new Font(
                    "Segoe UI",
                    8,
                    FontStyle.Bold
                ),

            AutoSize = true,

            Location =
                new Point(24, 45)
        };

        header.Controls.Add(subtitle);

        // ========================================================
        // CLOSE BUTTON
        // ========================================================

        var closeButton = new Button
        {
            Text = "×",

            Font =
                new Font(
                    "Segoe UI",
                    15,
                    FontStyle.Regular
                ),

            ForeColor =
                secondaryTextColor,

            BackColor =
                Color.Transparent,

            FlatStyle =
                FlatStyle.Flat,

            Width = 42,
            Height = 42,

            Location =
                new Point(340, 10),

            Cursor =
                Cursors.Hand
        };

        closeButton.FlatAppearance.BorderSize = 0;

        closeButton.FlatAppearance.MouseOverBackColor =
            Color.FromArgb(55, 24, 67);

        closeButton.FlatAppearance.MouseDownBackColor =
            Color.FromArgb(76, 31, 92);

        closeButton.Click += (_, _) =>
        {
            Close();
        };

        header.Controls.Add(closeButton);

        // ========================================================
        // GAME STATUS
        // ========================================================

        gameStatusLabel = new Label
        {
            Text =
                "● Esperando Taskbar Hero...",

            ForeColor =
                accentHoverColor,

            Font =
                new Font(
                    "Segoe UI",
                    9,
                    FontStyle.Bold
                ),

            AutoSize = true,

            Location =
                new Point(22, 86)
        };

        Controls.Add(gameStatusLabel);

        // ========================================================
        // GAME SPEED CARD
        // ========================================================

        Panel speedCard =
            CreateCard(
                20,
                115,
                350,
                145
            );

        Controls.Add(speedCard);

        Label speedTitle =
            CreateSectionTitle(
                "GAME SPEED",
                16,
                13
            );

        speedCard.Controls.Add(speedTitle);

        speedValueLabel = new Label
        {
            Text =
                "1.0x",

            ForeColor =
                accentHoverColor,

            Font =
                new Font(
                    "Segoe UI",
                    12,
                    FontStyle.Bold
                ),

            AutoSize = true,

            Location =
                new Point(283, 10)
        };

        speedCard.Controls.Add(speedValueLabel);

        // ========================================================
        // SPEED SLIDER
        // ========================================================

        speedSlider = new TrackBar
        {
            Minimum = 10,
            Maximum = 50,

            TickFrequency = 5,

            SmallChange = 1,
            LargeChange = 5,

            Width = 315,

            Location =
                new Point(12, 42)
        };

        speedSlider.Value =
            ReadCurrentSpeedValue();

        speedValueLabel.Text =
            $"{speedSlider.Value / 10f:0.0}x";

        speedSlider.ValueChanged += (_, _) =>
        {
            float speed =
                speedSlider.Value / 10f;

            speedValueLabel.Text =
                $"{speed:0.0}x";

            WriteGameSpeed(speed);
        };

        speedCard.Controls.Add(speedSlider);

        // ========================================================
        // SPEED BUTTONS
        // ========================================================

        Button button1x =
            CreateSmallButton(
                "1X",
                15,
                98
            );

        button1x.Click += (_, _) =>
        {
            speedSlider.Value = 10;
        };

        speedCard.Controls.Add(button1x);

        Button button2x =
            CreateSmallButton(
                "2X",
                125,
                98
            );

        button2x.Click += (_, _) =>
        {
            speedSlider.Value = 20;
        };

        speedCard.Controls.Add(button2x);

        Button button5x =
            CreateSmallButton(
                "5X",
                235,
                98
            );

        button5x.Click += (_, _) =>
        {
            speedSlider.Value = 50;
        };

        speedCard.Controls.Add(button5x);

        // ========================================================
        // HERO CARD
        // ========================================================

        Panel heroCard =
            CreateCard(
                20,
                275,
                350,
                315
            );

        Controls.Add(heroCard);

        Label heroTitle =
            CreateSectionTitle(
                "HERO EDITOR",
                16,
                13
            );

        heroCard.Controls.Add(heroTitle);

        // ========================================================
        // HERO VISUAL SELECTOR
        //
        // El ComboBox continúa existiendo internamente porque
        // toda la lógica Apply/Reset ya funciona con él.
        //
        // Pero visualmente queda oculto.
        // ========================================================

        heroComboBox =
            new ComboBox
            {
                DropDownStyle =
                    ComboBoxStyle.DropDownList,

                Visible =
                    false,

                Width =
                    1,

                Height =
                    1,

                Location =
                    new Point(
                        -500,
                        -500
                    )
            };


        heroComboBox.Items.Add(
            new HeroOption(
                "Caballero",
                101
            )
        );


        heroComboBox.Items.Add(
            new HeroOption(
                "Explorador",
                201
            )
        );


        heroComboBox.Items.Add(
            new HeroOption(
                "Hechicero",
                301
            )
        );


        heroComboBox.Items.Add(
            new HeroOption(
                "Sacerdote",
                401
            )
        );


        heroComboBox.Items.Add(
            new HeroOption(
                "Cazador",
                501
            )
        );


        heroComboBox.Items.Add(
            new HeroOption(
                "Asesino",
                601
            )
        );


        heroComboBox.SelectedIndexChanged +=
            (_, _) =>
            {
                if (
                    heroComboBox.SelectedItem
                    is HeroOption hero
                )
                {
                    selectedHeroKeyVisual =
                        hero.HeroKey;


                    UpdateHeroVisualSelection();
                }


                LoadSelectedHeroRuntimeValues();
            };


        // Explorador por defecto.

        heroComboBox.SelectedIndex =
            1;


        heroCard.Controls.Add(
            heroComboBox
        );


        // ========================================================
        // SELECTOR VISUAL ANIMADO
        // ========================================================

        BuildHeroVisualSelector(
            heroCard,
            16,
            42,
            318
        );


        UpdateHeroVisualSelection();


        // Puede que Taskbar Hero ya haya exportado los frames.
        // Si todavía no, UpdateOverlay reintentará después.

        TryLoadHeroAnimations();

        heroComboBox =
            new ComboBox
            {
                DropDownStyle =
                    ComboBoxStyle.DropDownList,

                BackColor =
                    inputColor,

                ForeColor =
                    textColor,

                FlatStyle =
                    FlatStyle.Flat,

                Font =
                    new Font(
                        "Segoe UI",
                        9
                    ),

                Width = 318,

                Location =
                    new Point(
                        16,
                        62
                    )
            };

        heroComboBox.Items.Add(
            new HeroOption(
                "Caballero",
                101
            )
        );

        heroComboBox.Items.Add(
            new HeroOption(
                "Explorador",
                201
            )
        );

        heroComboBox.Items.Add(
            new HeroOption(
                "Hechicero",
                301
            )
        );

        heroComboBox.Items.Add(
            new HeroOption(
                "Sacerdote",
                401
            )
        );

        heroComboBox.Items.Add(
            new HeroOption(
                "Cazador",
                501
            )
        );

        heroComboBox.Items.Add(
            new HeroOption(
                "Asesino",
                601
            )
        );

        heroComboBox.SelectedIndexChanged +=
        (_, _) =>
        {
            LoadSelectedHeroRuntimeValues();
        };

        heroComboBox.SelectedIndex = 1;

        heroCard.Controls.Add(
            heroComboBox
        );

        // ========================================================
        // NIVEL
        // ========================================================

        var levelLabel =
            CreateInputLabel(
                "Nivel",
                16,
                101
            );

        heroCard.Controls.Add(
            levelLabel
        );

        heroLevelInput =
            CreateIntegerInput(
                16,
                121,
                145,
                1,
                500,
                20
            );

        heroCard.Controls.Add(
            heroLevelInput
        );

        Button applyLevelButton =
            CreatePurpleButton(
                "APLICAR NIVEL",
                16,
                151,
                145
            );

        applyLevelButton.Click +=
            ApplyLevel;

        heroCard.Controls.Add(
            applyLevelButton
        );

        // ========================================================
        // DAÑO REAL
        // ========================================================

        var damageLabel =
            CreateInputLabel(
                "Daño real",
                188,
                101
            );

        heroCard.Controls.Add(
            damageLabel
        );

        damageInput =
            CreateIntegerInput(
                188,
                121,
                146,
                1,
                1000000,
                ReadCurrentDamageValue()
            );

        heroCard.Controls.Add(
            damageInput
        );

        Button applyDamageButton =
            CreatePurpleButton(
                "APLICAR DAÑO",
                188,
                151,
                146
            );

        applyDamageButton.Click +=
            ApplyDamage;

        heroCard.Controls.Add(
            applyDamageButton
        );

        // ========================================================
        // ATTACK SPEED
        // ========================================================

        var attackSpeedLabel =
            CreateInputLabel(
                "Vel. de ataque",
                16,
                196
            );

        heroCard.Controls.Add(
            attackSpeedLabel
        );

        attackSpeedInput =
            CreateDecimalInput(
                16,
                216,
                145,
                0.10m,
                100.00m,
                ReadCurrentAttackSpeedValue(),
                2,
                0.10m
            );

        heroCard.Controls.Add(
            attackSpeedInput
        );

        Button applyAttackSpeedButton =
            CreatePurpleButton(
                "APLICAR ATAQUE",
                16,
                246,
                145
            );

        applyAttackSpeedButton.Click +=
            ApplyAttackSpeed;

        heroCard.Controls.Add(
            applyAttackSpeedButton
        );

        // ========================================================
        // MOVEMENT SPEED
        // ========================================================

        var movementSpeedLabel =
            CreateInputLabel(
                "Vel. movimiento",
                188,
                196
            );

        heroCard.Controls.Add(
            movementSpeedLabel
        );

        movementSpeedInput =
            CreateDecimalInput(
                188,
                216,
                146,
                0.10m,
                125.00m,
                ReadCurrentMovementSpeedValue(),
                2,
                0.50m
            );

        heroCard.Controls.Add(
            movementSpeedInput
        );

        Button applyMovementButton =
            CreatePurpleButton(
                "APLICAR MOV.",
                188,
                246,
                146
            );

        applyMovementButton.Click +=
            ApplyMovementSpeed;

        heroCard.Controls.Add(
            applyMovementButton
        );

        // ========================================================
        // GOD MODE
        // ========================================================

        godModeCheckBox =
            new CheckBox
            {
                Text =
                    "GOD MODE  •  Vida infinita",

                Checked =
                    ReadCurrentGodModeValue(),

                ForeColor =
                    textColor,

                BackColor =
                    cardColor,

                Font =
                    new Font(
                        "Segoe UI",
                        9,
                        FontStyle.Bold
                    ),

                FlatStyle =
                    FlatStyle.Flat,

                AutoSize = true,

                Location =
                    new Point(
                        16,
                        287
                    ),

                Cursor =
                    Cursors.Hand
            };

        godModeCheckBox.FlatAppearance.BorderColor =
            accentColor;

        godModeCheckBox.CheckedChanged += (_, _) =>
        {
            WriteGodMode(
                godModeCheckBox.Checked
            );

            godModeCheckBox.ForeColor =
                godModeCheckBox.Checked
                    ? successColor
                    : textColor;

            SetResult(
                godModeCheckBox.Checked
                    ? "GOD MODE ACTIVADO"
                    : "GOD MODE DESACTIVADO",
                godModeCheckBox.Checked
            );
        };

        godModeCheckBox.ForeColor =
            godModeCheckBox.Checked
                ? successColor
                : textColor;

        heroCard.Controls.Add(
            godModeCheckBox
        );

        // ========================================================
        // RESET PERSONAJE
        // ========================================================

        Button resetHeroButton =
            CreatePurpleButton(
                "RESTABLECER PJ",
                188,
                284,
                146
            );


        resetHeroButton.BackColor =
            Color.FromArgb(
                76,
                57,
                92
            );


        resetHeroButton.FlatAppearance.MouseOverBackColor =
            Color.FromArgb(
                98,
                72,
                120
            );


        resetHeroButton.Click +=
            ResetSelectedHero;


        heroCard.Controls.Add(
            resetHeroButton
        );

        // ========================================================
        // ECONOMÍA
        // ========================================================

        Panel economyCard =
            CreateCard(
                20,
                605,
                350,
                105
            );

        Controls.Add(
            economyCard
        );

        Label economyTitle =
            CreateSectionTitle(
                "ECONOMÍA",
                16,
                13
            );

        economyCard.Controls.Add(
            economyTitle
        );


        var moneyLabel =
            CreateInputLabel(
                "Multiplicador de dinero",
                16,
                43
            );

        economyCard.Controls.Add(
            moneyLabel
        );


        moneyMultiplierInput =
            CreateDecimalInput(
                16,
                62,
                145,
                1.00m,
                100000.00m,
                ReadCurrentMoneyMultiplier(),
                2,
                1.00m
            );

        economyCard.Controls.Add(
            moneyMultiplierInput
        );


        Button applyMoneyButton =
            CreatePurpleButton(
                "APLICAR DINERO",
                188,
                60,
                146
            );

        applyMoneyButton.Click +=
            ApplyMoneyMultiplier;

        economyCard.Controls.Add(
            applyMoneyButton
        );

        // ========================================================
        // UNINSTALL MOD
        // ========================================================

        var uninstallButton =
            new Button
            {
                Text =
                    "LIMPIAR / DESINSTALAR MOD",

                ForeColor =
                    Color.White,

                BackColor =
                    dangerColor,

                FlatStyle =
                    FlatStyle.Flat,

                Font =
                    new Font(
                        "Segoe UI",
                        9,
                        FontStyle.Bold
                    ),

                Cursor =
                    Cursors.Hand,

                Location =
                    new Point(
                        20,
                        735
                    ),

                Size =
                    new Size(
                        350,
                        40
                    )
            };

        uninstallButton.FlatAppearance.BorderSize =
            0;

        uninstallButton.FlatAppearance.MouseOverBackColor =
            Color.FromArgb(
                220,
                75,
                105
            );

        uninstallButton.Click += (_, _) =>
        {
            DialogResult confirmation =
                MessageBox.Show(
                    "¿Quieres eliminar el mod de la instalación de Taskbar Hero?",
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

            if (
                ModInstaller.UninstallMod()
            )
            {
                Close();
            }
        };

        Controls.Add(
            uninstallButton
        );

        // ========================================================
        // RESULT
        // ========================================================

        resultLabel =
            new Label
            {
                Text =
                    "Listo para modificar.",

                ForeColor =
                    secondaryTextColor,

                Font =
                    new Font(
                        "Segoe UI",
                        8
                    ),

                AutoEllipsis = true,

                Width = 210,

                Location =
                    new Point(
                        22,
                        785
                    )
            };

        Controls.Add(
            resultLabel
        );

        // ========================================================
        // F1
        // ========================================================

        var hintLabel =
            new Label
            {
                Text =
                    "F1  •  Mostrar / Ocultar",

                ForeColor =
                    secondaryTextColor,

                Font =
                    new Font(
                        "Segoe UI",
                        8
                    ),

                AutoSize = true,

                Location =
                    new Point(
                        225,
                        785
                    )
            };

        Controls.Add(
            hintLabel
        );

        // ========================================================
        // TIMER
        // ========================================================

        updateTimer =
            new System.Windows.Forms.Timer
            {
                Interval = 50
            };

        updateTimer.Tick +=
            UpdateOverlay;

        updateTimer.Start();
    }

    // ============================================================
    // CREATE CARD
    // ============================================================

    private Panel CreateCard(
        int x,
        int y,
        int width,
        int height
    )
    {
        Panel card =
            new Panel
            {
                BackColor =
                    cardColor,

                Location =
                    new Point(
                        x,
                        y
                    ),

                Size =
                    new Size(
                        width,
                        height
                    )
            };

        Panel accentLine =
            new Panel
            {
                BackColor =
                    accentDarkColor,

                Location =
                    new Point(
                        0,
                        0
                    ),

                Size =
                    new Size(
                        3,
                        height
                    )
            };

        card.Controls.Add(
            accentLine
        );

        return card;
    }

    // ============================================================
    // SECTION TITLE
    // ============================================================

    private Label CreateSectionTitle(
        string text,
        int x,
        int y
    )
    {
        return new Label
        {
            Text =
                text,

            ForeColor =
                textColor,

            Font =
                new Font(
                    "Segoe UI",
                    9,
                    FontStyle.Bold
                ),

            AutoSize = true,

            Location =
                new Point(
                    x,
                    y
                )
        };
    }

    // ============================================================
    // INPUT LABEL
    // ============================================================

    private Label CreateInputLabel(
        string text,
        int x,
        int y
    )
    {
        return new Label
        {
            Text =
                text,

            ForeColor =
                secondaryTextColor,

            Font =
                new Font(
                    "Segoe UI",
                    8
                ),

            AutoSize = true,

            Location =
                new Point(
                    x,
                    y
                )
        };
    }

    // ============================================================
    // INTEGER INPUT
    // ============================================================

    private NumericUpDown CreateIntegerInput(
        int x,
        int y,
        int width,
        int minimum,
        int maximum,
        int value
    )
    {
        int safeValue =
            Math.Clamp(
                value,
                minimum,
                maximum
            );

        return new NumericUpDown
        {
            Minimum =
                minimum,

            Maximum =
                maximum,

            Value =
                safeValue,

            BackColor =
                inputColor,

            ForeColor =
                textColor,

            BorderStyle =
                BorderStyle.FixedSingle,

            Font =
                new Font(
                    "Segoe UI",
                    10,
                    FontStyle.Bold
                ),

            Width =
                width,

            Height =
                28,

            Location =
                new Point(
                    x,
                    y
                )
        };
    }

    // ============================================================
    // DECIMAL INPUT
    // ============================================================

    private NumericUpDown CreateDecimalInput(
        int x,
        int y,
        int width,
        decimal minimum,
        decimal maximum,
        decimal value,
        int decimalPlaces,
        decimal increment
    )
    {
        decimal safeValue =
            Math.Max(
                minimum,
                Math.Min(
                    maximum,
                    value
                )
            );

        return new NumericUpDown
        {
            Minimum =
                minimum,

            Maximum =
                maximum,

            Value =
                safeValue,

            DecimalPlaces =
                decimalPlaces,

            Increment =
                increment,

            BackColor =
                inputColor,

            ForeColor =
                textColor,

            BorderStyle =
                BorderStyle.FixedSingle,

            Font =
                new Font(
                    "Segoe UI",
                    10,
                    FontStyle.Bold
                ),

            Width =
                width,

            Height =
                28,

            Location =
                new Point(
                    x,
                    y
                )
        };
    }

    // ============================================================
    // SMALL BUTTON
    // ============================================================

    private Button CreateSmallButton(
        string text,
        int x,
        int y
    )
    {
        Button button =
            new Button
            {
                Text =
                    text,

                ForeColor =
                    textColor,

                BackColor =
                    inputColor,

                FlatStyle =
                    FlatStyle.Flat,

                Font =
                    new Font(
                        "Segoe UI",
                        8,
                        FontStyle.Bold
                    ),

                Width = 95,
                Height = 28,

                Location =
                    new Point(
                        x,
                        y
                    ),

                Cursor =
                    Cursors.Hand
            };

        button.FlatAppearance.BorderSize =
            1;

        button.FlatAppearance.BorderColor =
            cardBorderColor;

        button.FlatAppearance.MouseOverBackColor =
            inputHoverColor;

        button.FlatAppearance.MouseDownBackColor =
            accentDarkColor;

        return button;
    }

    // ============================================================
    // PURPLE ACTION BUTTON
    // ============================================================

    private Button CreatePurpleButton(
        string text,
        int x,
        int y,
        int width
    )
    {
        Button button =
            new Button
            {
                Text =
                    text,

                ForeColor =
                    Color.White,

                BackColor =
                    accentColor,

                FlatStyle =
                    FlatStyle.Flat,

                Font =
                    new Font(
                        "Segoe UI",
                        8,
                        FontStyle.Bold
                    ),

                Width =
                    width,

                Height =
                    31,

                Location =
                    new Point(
                        x,
                        y
                    ),

                Cursor =
                    Cursors.Hand
            };

        button.FlatAppearance.BorderSize =
            0;

        button.FlatAppearance.MouseOverBackColor =
            accentHoverColor;

        button.FlatAppearance.MouseDownBackColor =
            accentDarkColor;

        return button;
    }

    // ============================================================
    // BUILD HERO VISUAL SELECTOR
    // ============================================================

    private void BuildHeroVisualSelector(
        Control parent,
        int x,
        int y,
        int width
    )
    {
        heroVisualPanel =
            new FlowLayoutPanel
            {
                Location =
                    new Point(
                        x,
                        y
                    ),

                Size =
                    new Size(
                        width,
                        54
                    ),

                FlowDirection =
                    FlowDirection.LeftToRight,

                WrapContents =
                    false,

                AutoScroll =
                    false,

                Padding =
                    new Padding(0),

                Margin =
                    new Padding(0),

                BackColor =
                    Color.Transparent
            };


        parent.Controls.Add(
            heroVisualPanel
        );


        AddHeroVisualCard(
            101,
            "Caballero"
        );


        AddHeroVisualCard(
            201,
            "Explorador"
        );


        AddHeroVisualCard(
            301,
            "Hechicero"
        );


        AddHeroVisualCard(
            401,
            "Sacerdote"
        );


        AddHeroVisualCard(
            501,
            "Cazador"
        );


        AddHeroVisualCard(
            601,
            "Asesino"
        );
    }

    // ============================================================
    // ADD HERO VISUAL CARD
    // ============================================================

    private void AddHeroVisualCard(
        int heroKey,
        string heroName
    )
    {
        Panel card =
            new Panel
            {
                Width =
                    49,

                Height =
                    52,

                Margin =
                    new Padding(
                        2,
                        0,
                        2,
                        0
                    ),

                BackColor =
                    inputColor,

                Cursor =
                    Cursors.Hand,

                Tag =
                    heroKey
            };


        PictureBox picture =
            new PictureBox
            {
                Location =
                    new Point(
                        3,
                        3
                    ),

                Size =
                    new Size(
                        43,
                        43
                    ),

                SizeMode =
                    PictureBoxSizeMode.Zoom,

                BackColor =
                    Color.Transparent,

                Cursor =
                    Cursors.Hand,

                Tag =
                    heroKey
            };


        // ========================================================
        // BORDER
        // ========================================================

        card.Paint +=
            (_, e) =>
            {
                bool selected =
                    heroKey ==
                    selectedHeroKeyVisual;


                Color border =
                    selected
                        ? accentHoverColor
                        : cardBorderColor;


                int thickness =
                    selected
                        ? 2
                        : 1;


                using Pen pen =
                    new Pen(
                        border,
                        thickness
                    );


                e.Graphics.DrawRectangle(
                    pen,
                    1,
                    1,
                    card.Width - 3,
                    card.Height - 3
                );


                // Línea morada inferior del seleccionado.

                if (selected)
                {
                    using Brush brush =
                        new SolidBrush(
                            accentColor
                        );


                    e.Graphics.FillRectangle(
                        brush,
                        4,
                        card.Height - 5,
                        card.Width - 8,
                        3
                    );
                }
            };


        void SelectThisHero(
            object? sender,
            EventArgs e
        )
        {
            SelectHeroByKey(
                heroKey
            );
        }


        card.Click +=
            SelectThisHero;


        picture.Click +=
            SelectThisHero;


        card.Controls.Add(
            picture
        );


        heroVisualPanel.Controls.Add(
            card
        );


        heroVisualCards[
            heroKey
        ] =
            card;


        heroVisualPictures[
            heroKey
        ] =
            picture;
    }

    // ============================================================
    // SELECT HERO BY VISUAL CARD
    // ============================================================

    private void SelectHeroByKey(
        int heroKey
    )
    {
        try
        {
            for (
                int i = 0;
                i < heroComboBox.Items.Count;
                i++
            )
            {
                if (
                    heroComboBox.Items[i]
                    is HeroOption hero &&
                    hero.HeroKey == heroKey
                )
                {
                    heroComboBox.SelectedIndex =
                        i;


                    selectedHeroKeyVisual =
                        heroKey;


                    UpdateHeroVisualSelection();


                    SetResult(
                        $"{hero.Name} seleccionado",
                        true
                    );


                    return;
                }
            }
        }
        catch
        {
        }
    }

    // ============================================================
    // UPDATE HERO VISUAL SELECTION
    // ============================================================

    private void UpdateHeroVisualSelection()
    {
        foreach (
            KeyValuePair<int, Panel> pair
            in heroVisualCards
        )
        {
            bool selected =
                pair.Key ==
                selectedHeroKeyVisual;


            pair.Value.BackColor =
                selected
                    ? Color.FromArgb(
                        48,
                        31,
                        68
                    )
                    : inputColor;


            pair.Value.Invalidate();
        }
    }

    // ============================================================
    // LOAD HERO ANIMATIONS
    // ============================================================

    private void TryLoadHeroAnimations()
    {
        if (heroAnimationsLoaded)
        {
            return;
        }


        if (
            (
                DateTime.UtcNow -
                lastHeroAnimationLoadAttempt
            ).TotalMilliseconds
            <
            HeroAnimationLoadRetryMs
        )
        {
            return;
        }


        lastHeroAnimationLoadAttempt =
            DateTime.UtcNow;


        try
        {
            int[] heroIds =
            {
                101,
                201,
                301,
                401,
                501,
                601
            };


            foreach (
                int heroId
                in heroIds
            )
            {
                // Ya lo cargamos.

                if (
                    heroAnimationFrames.ContainsKey(
                        heroId
                    )
                )
                {
                    continue;
                }


                string heroDirectory =
                    Path.Combine(
                        HeroAnimationsDirectory,
                        heroId.ToString()
                    );


                if (
                    !Directory.Exists(
                        heroDirectory
                    )
                )
                {
                    continue;
                }


                string[] files =
                    Directory.GetFiles(
                        heroDirectory,
                        "*.png"
                    );


                if (files.Length == 0)
                {
                    continue;
                }


                Array.Sort(
                    files,
                    StringComparer.OrdinalIgnoreCase
                );


                List<Image> frames =
                    new List<Image>();


                foreach (
                    string path
                    in files
                )
                {
                    Image? frame =
                        LoadImageWithoutLock(
                            path
                        );


                    if (frame != null)
                    {
                        frames.Add(
                            frame
                        );
                    }
                }


                if (frames.Count > 0)
                {
                    heroAnimationFrames[
                        heroId
                    ] =
                        frames;
                }
            }


            // Esperamos hasta tener los seis personajes.

            if (
                heroAnimationFrames.Count < 6
            )
            {
                return;
            }


            heroAnimationsLoaded =
                true;


            StartHeroAnimationTimer();


            AdvanceHeroAnimations();
        }
        catch
        {
        }
    }

    // ============================================================
    // LOAD IMAGE WITHOUT LOCKING PNG FILE
    // ============================================================

    private Image? LoadImageWithoutLock(
        string path
    )
    {
        try
        {
            byte[] data =
                File.ReadAllBytes(
                    path
                );


            using MemoryStream stream =
                new MemoryStream(
                    data
                );


            using Image source =
                Image.FromStream(
                    stream
                );


            return new Bitmap(
                source
            );
        }
        catch
        {
            return null;
        }
    }

    // ============================================================
    // READ HERO ANIMATION INTERVAL
    // ============================================================

    private int ReadHeroAnimationInterval()
    {
        const int fallback =
            167;


        try
        {
            string metadataPath =
                Path.Combine(
                    HeroAnimationsDirectory,
                    "201",
                    "animation.txt"
                );


            if (
                !File.Exists(
                    metadataPath
                )
            )
            {
                return fallback;
            }


            float duration =
                1.0f;


            float speed =
                1.0f;


            int frames =
                6;


            foreach (
                string rawLine
                in File.ReadAllLines(
                    metadataPath
                )
            )
            {
                string[] parts =
                    rawLine.Split(
                        '=',
                        2
                    );


                if (parts.Length != 2)
                {
                    continue;
                }


                string key =
                    parts[0].Trim();


                string value =
                    parts[1].Trim();


                if (
                    key.Equals(
                        "duration",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    float.TryParse(
                        value,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out duration
                    );
                }


                if (
                    key.Equals(
                        "speed",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    float.TryParse(
                        value,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out speed
                    );
                }


                if (
                    key.Equals(
                        "frames",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    int.TryParse(
                        value,
                        out frames
                    );
                }
            }


            if (
                duration <= 0f ||
                speed <= 0f ||
                frames <= 0
            )
            {
                return fallback;
            }


            double milliseconds =
                (
                    duration /
                    frames /
                    speed
                )
                *
                1000.0;


            return Math.Clamp(
                (int)Math.Round(
                    milliseconds
                ),
                50,
                500
            );
        }
        catch
        {
            return fallback;
        }
    }

    // ============================================================
    // START HERO ANIMATION TIMER
    // ============================================================

    private void StartHeroAnimationTimer()
    {
        if (heroAnimationTimer != null)
        {
            return;
        }


        heroAnimationTimer =
            new System.Windows.Forms.Timer
            {
                Interval =
                    ReadHeroAnimationInterval()
            };


        heroAnimationTimer.Tick +=
            (_, _) =>
            {
                AdvanceHeroAnimations();
            };


        heroAnimationTimer.Start();
    }

    // ============================================================
    // ADVANCE HERO ANIMATIONS
    // ============================================================

    private void AdvanceHeroAnimations()
    {
        if (!heroAnimationsLoaded)
        {
            return;
        }


        foreach (
            KeyValuePair<int, PictureBox> pair
            in heroVisualPictures
        )
        {
            if (
                !heroAnimationFrames.TryGetValue(
                    pair.Key,
                    out List<Image>? frames
                ) ||
                frames == null ||
                frames.Count == 0
            )
            {
                continue;
            }


            int frameIndex =
                heroAnimationFrameIndex
                %
                frames.Count;


            pair.Value.Image =
                frames[
                    frameIndex
                ];
        }


        heroAnimationFrameIndex++;
    }

    // ============================================================
    // APPLY LEVEL
    // ============================================================

    private void ApplyLevel(
        object? sender,
        EventArgs e
    )
    {
        try
        {
            if (!TryGetSelectedHero(out HeroOption? hero))
                return;

            int level =
                (int)heroLevelInput.Value;

            string command =
                $"{hero!.HeroKey}|" +
                $"{level}|" +
                $"{DateTime.UtcNow.Ticks}";

            File.WriteAllText(
                HeroCommandFile,
                command
            );

            SetResult(
                $"{hero.Name} • Nivel {level}",
                true
            );
        }
        catch (Exception ex)
        {
            SetResult(
                $"Error nivel: {ex.Message}",
                false
            );
        }
    }

    // ============================================================
    // APPLY DAMAGE
    // ============================================================

    private void ApplyDamage(
        object? sender,
        EventArgs e
    )
    {
        try
        {
            if (!TryGetSelectedHero(out HeroOption? hero))
                return;

            int damage =
                (int)damageInput.Value;

            File.WriteAllText(
                AttackDamageFile,
                damage.ToString(
                    CultureInfo.InvariantCulture
                )
            );

            string command =
                $"{hero!.HeroKey}|" +
                $"{damage}|" +
                $"{DateTime.UtcNow.Ticks}";

            File.WriteAllText(
                DamageCommandFile,
                command
            );

            SetResult(
                $"{hero.Name} • Daño {damage}",
                true
            );
        }
        catch (Exception ex)
        {
            SetResult(
                $"Error daño: {ex.Message}",
                false
            );
        }
    }

    // ============================================================
    // APPLY ATTACK SPEED
    // ============================================================

    private void ApplyAttackSpeed(
        object? sender,
        EventArgs e
    )
    {
        try
        {
            if (!TryGetSelectedHero(out HeroOption? hero))
                return;

            decimal value =
                attackSpeedInput.Value;

            string text =
                value.ToString(
                    CultureInfo.InvariantCulture
                );

            File.WriteAllText(
                AttackSpeedFile,
                text
            );

            string command =
                $"{hero!.HeroKey}|" +
                $"{text}|" +
                $"{DateTime.UtcNow.Ticks}";

            File.WriteAllText(
                AttackSpeedCommandFile,
                command
            );

            SetResult(
                $"{hero.Name} • Vel. ataque {value:0.00}",
                true
            );
        }
        catch (Exception ex)
        {
            SetResult(
                $"Error vel. ataque: {ex.Message}",
                false
            );
        }
    }

    // ============================================================
    // APPLY MOVEMENT SPEED
    // ============================================================

    private void ApplyMovementSpeed(
        object? sender,
        EventArgs e
    )
    {
        try
        {
            if (!TryGetSelectedHero(out HeroOption? hero))
                return;

            decimal value =
                movementSpeedInput.Value;

            string text =
                value.ToString(
                    CultureInfo.InvariantCulture
                );

            File.WriteAllText(
                MovementSpeedFile,
                text
            );

            string command =
                $"{hero!.HeroKey}|" +
                $"{text}|" +
                $"{DateTime.UtcNow.Ticks}";

            File.WriteAllText(
                MovementSpeedCommandFile,
                command
            );

            SetResult(
                $"{hero.Name} • Movimiento {value:0.00}",
                true
            );
        }
        catch (Exception ex)
        {
            SetResult(
                $"Error movimiento: {ex.Message}",
                false
            );
        }
    }

    // ============================================================
    // RESET SELECTED HERO
    // ============================================================

    private void ResetSelectedHero(
        object? sender,
        EventArgs e
    )
    {
        try
        {
            if (
                !TryGetSelectedHero(
                    out HeroOption? hero
                ) ||
                hero == null
            )
            {
                return;
            }


            // ========================================================
            // ENVIAR COMANDO AL PLUGIN
            //
            // hero|201|timestamp
            // ========================================================

            string command =
                $"hero|" +
                $"{hero.HeroKey}|" +
                $"{DateTime.UtcNow.Ticks}";


            File.WriteAllText(
                ResetCommandFile,
                command
            );


            SetResult(
                $"{hero.Name} • Restaurando valores reales...",
                true
            );


            // ========================================================
            // ESPERAR A QUE TBHPLUGIN PROCESE EL RESET
            //
            // Plugin revisa reset_command y después actualiza
            // hero_runtime_state.txt.
            // ========================================================

            System.Windows.Forms.Timer refreshTimer =
                new System.Windows.Forms.Timer
                {
                    Interval =
                        750
                };


            refreshTimer.Tick +=
                (_, _) =>
                {
                    refreshTimer.Stop();

                    refreshTimer.Dispose();


                    // Forzar que el lector permita una nueva lectura.

                    lastHeroRuntimeStateRead =
                        DateTime.MinValue;


                    UpdateHeroRuntimeState();


                    LoadSelectedHeroRuntimeValues();


                    SetResult(
                        $"{hero.Name} • Valores restaurados",
                        true
                    );
                };


            refreshTimer.Start();
        }
        catch (Exception ex)
        {
            SetResult(
                $"Error reset: {ex.Message}",
                false
            );
        }
    }
    // ============================================================
    // APPLY MONEY MULTIPLIER
    // ============================================================

    private void ApplyMoneyMultiplier(
        object? sender,
        EventArgs e
    )
    {
        try
        {
            decimal value =
                moneyMultiplierInput.Value;

            string text =
                value.ToString(
                    CultureInfo.InvariantCulture
                );

            File.WriteAllText(
                MoneyMultiplierFile,
                text
            );

            SetResult(
                $"Dinero • {value:0.00}x",
                true
            );
        }
        catch (Exception ex)
        {
            SetResult(
                $"Error dinero: {ex.Message}",
                false
            );
        }
    }

    // ============================================================
    // GOD MODE
    // ============================================================

    private bool ReadCurrentGodModeValue()
    {
        try
        {
            if (!File.Exists(GodModeFile))
            {
                File.WriteAllText(
                    GodModeFile,
                    "0"
                );

                return false;
            }

            string text =
                File.ReadAllText(
                    GodModeFile
                ).Trim();

            return
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
        }
        catch
        {
            return false;
        }
    }


    private void WriteGodMode(
        bool enabled
    )
    {
        try
        {
            File.WriteAllText(
                GodModeFile,
                enabled
                    ? "1"
                    : "0"
            );
        }
        catch (Exception ex)
        {
            SetResult(
                $"Error God Mode: {ex.Message}",
                false
            );
        }
    }


    // ============================================================
    // SELECTED HERO
    // ============================================================

    private bool TryGetSelectedHero(
        out HeroOption? hero
    )
    {
        hero =
            heroComboBox.SelectedItem
            as HeroOption;

        if (hero != null)
            return true;

        SetResult(
            "Selecciona un personaje.",
            false
        );

        return false;
    }

    // ============================================================
    // RESULT
    // ============================================================

    private void SetResult(
        string message,
        bool success
    )
    {
        resultLabel.Text =
            message;

        resultLabel.ForeColor =
            success
                ? successColor
                : dangerColor;
    }

    // ============================================================
    // READ CURRENT SPEED
    // ============================================================

    private int ReadCurrentSpeedValue()
    {
        try
        {
            if (!File.Exists(SpeedFile))
            {
                File.WriteAllText(
                    SpeedFile,
                    "1.0"
                );

                return 10;
            }

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
                speed =
                    Math.Clamp(
                        speed,
                        1.0f,
                        5.0f
                    );

                return (int)Math.Round(
                    speed * 10f
                );
            }
        }
        catch
        {
        }

        return 10;
    }

    // ============================================================
    // READ DAMAGE
    // ============================================================

    private int ReadCurrentDamageValue()
    {
        try
        {
            if (!File.Exists(
                    AttackDamageFile
                ))
            {
                File.WriteAllText(
                    AttackDamageFile,
                    "50"
                );

                return 50;
            }

            string text =
                File.ReadAllText(
                    AttackDamageFile
                ).Trim();

            if (
                int.TryParse(
                    text,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int damage
                )
            )
            {
                return Math.Clamp(
                    damage,
                    1,
                    1000000
                );
            }
        }
        catch
        {
        }

        return 50;
    }

    // ============================================================
    // READ ATTACK SPEED
    // ============================================================

    private decimal ReadCurrentAttackSpeedValue()
    {
        return ReadDecimalFile(
            AttackSpeedFile,
            1.56m,
            0.10m,
            100.00m
        );
    }

    // ============================================================
    // READ MOVEMENT SPEED
    // ============================================================

    private decimal ReadCurrentMovementSpeedValue()
    {
        return ReadDecimalFile(
            MovementSpeedFile,
            8.50m,
            0.10m,
            125.00m
        );
    }

    // ============================================================
    // READ DECIMAL FILE
    // ============================================================

    private decimal ReadDecimalFile(
        string path,
        decimal defaultValue,
        decimal minimum,
        decimal maximum
    )
    {
        try
        {
            if (!File.Exists(path))
            {
                File.WriteAllText(
                    path,
                    defaultValue.ToString(
                        CultureInfo.InvariantCulture
                    )
                );

                return defaultValue;
            }

            string text =
                File.ReadAllText(
                    path
                ).Trim();

            if (
                decimal.TryParse(
                    text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out decimal value
                )
            )
            {
                return Math.Max(
                    minimum,
                    Math.Min(
                        maximum,
                        value
                    )
                );
            }
        }
        catch
        {
        }

        return defaultValue;
    }

    // ============================================================
    // READ MONEY MULTIPLIER
    // ============================================================

    private decimal ReadCurrentMoneyMultiplier()
    {
        return ReadDecimalFile(
            MoneyMultiplierFile,
            1.00m,
            1.00m,
            100000.00m
        );
    }
       

    // ============================================================
    // WRITE SPEED
    // ============================================================

    private void WriteGameSpeed(
        float speed
    )
    {
        try
        {
            File.WriteAllText(
                SpeedFile,
                speed.ToString(
                    CultureInfo.InvariantCulture
                )
            );
        }
        catch
        {
        }
    }

    // ============================================================
    // UPDATE OVERLAY
    // ============================================================

    private void UpdateOverlay(
        object? sender,
        EventArgs e
    )
    {
        DetectGame();
        UpdateHeroRuntimeState();
        TryLoadHeroAnimations();
        bool f1Pressed =
            (
                GetAsyncKeyState(
                    VK_F1
                )
                &
                0x8000
            )
            != 0;

        if (
            f1Pressed &&
            !f1WasPressed
        )
        {
            menuVisible =
                !menuVisible;

            Visible =
                menuVisible;
        }

        f1WasPressed =
            f1Pressed;

        if (
            gameProcess == null ||
            gameProcess.HasExited
        )
        {
            gameStatusLabel.Text =
                "● Esperando Taskbar Hero...";

            gameStatusLabel.ForeColor =
                accentHoverColor;

            if (menuVisible)
            {
                Visible = true;
            }

            return;
        }

        gameStatusLabel.Text =
            "● Taskbar Hero conectado";

        gameStatusLabel.ForeColor =
            successColor;

        if (!menuVisible)
            return;
    }

    // ============================================================
    // DETECT GAME
    // ============================================================

    private void DetectGame()
    {
        if (gameProcess != null)
        {
            try
            {
                if (!gameProcess.HasExited)
                {
                    return;
                }
            }
            catch
            {
            }

            gameProcess.Dispose();

            gameProcess = null;
        }

        try
        {
            Process[] processes =
                Process.GetProcessesByName(
                    "TaskBarHero"
                );

            if (processes.Length > 0)
            {
                gameProcess =
                    processes[0];
            }
        }
        catch
        {
            gameProcess = null;
        }
    }

    // ============================================================
    // CLOSE
    // ============================================================

   protected override void OnFormClosing(
        FormClosingEventArgs e
    )
    {
        updateTimer.Stop();

        updateTimer.Dispose();


        if (heroAnimationTimer != null)
        {
            heroAnimationTimer.Stop();

            heroAnimationTimer.Dispose();

            heroAnimationTimer =
                null;
        }


        foreach (
            KeyValuePair<int, List<Image>> pair
            in heroAnimationFrames
        )
        {
            foreach (
                Image image
                in pair.Value
            )
            {
                image.Dispose();
            }
        }


        heroAnimationFrames.Clear();


        gameProcess?.Dispose();


        base.OnFormClosing(
            e
        );
    }

    // ============================================================
    // READ HERO RUNTIME STATE
    // ============================================================

    private void UpdateHeroRuntimeState()
    {
        try
        {
            if (
                (
                    DateTime.UtcNow -
                    lastHeroRuntimeStateRead
                ).TotalMilliseconds
                <
                HeroRuntimeStateRefreshMs
            )
            {
                return;
            }


            lastHeroRuntimeStateRead =
                DateTime.UtcNow;


            if (!File.Exists(HeroRuntimeStateFile))
            {
                return;
            }


            string[] lines =
                File.ReadAllLines(
                    HeroRuntimeStateFile
                );


            if (lines.Length <= 1)
            {
                return;
            }


            Dictionary<int, HeroRuntimeState>
                newStates =
                    new Dictionary<int, HeroRuntimeState>();


            foreach (string rawLine in lines)
            {
                string line =
                    rawLine.Trim();


                if (
                    string.IsNullOrWhiteSpace(line) ||
                    line.StartsWith("#")
                )
                {
                    continue;
                }


                string[] parts =
                    line.Split('|');


                // Formato actual:
                //
                // 0 HeroId
                // 1 Level
                // 2 OriginalLevel
                // 3 Unlocked
                // 4 OriginalDamage
                // 5 DamageOverride
                // 6 RealAttackSpeed
                // 7 AttackOverride
                // 8 RealMovementSpeed
                // 9 MovementOverride

                if (parts.Length < 10)
                {
                    continue;
                }


                if (
                    !int.TryParse(
                        parts[0],
                        out int heroId
                    )
                )
                {
                    continue;
                }


                int.TryParse(
                    parts[1],
                    out int level
                );


                int.TryParse(
                    parts[2],
                    out int originalLevel
                );


                bool unlocked =
                    parts[3] == "1";


                HeroRuntimeState state =
                    new HeroRuntimeState
                    {
                        HeroId =
                            heroId,

                        Level =
                            level,

                        OriginalLevel =
                            originalLevel,

                        Unlocked =
                            unlocked,

                        OriginalDamage =
                            ParseRuntimeDecimal(
                                parts[4]
                            ),

                        DamageOverride =
                            ParseRuntimeDecimal(
                                parts[5]
                            ),

                        RealAttackSpeed =
                            ParseRuntimeDecimal(
                                parts[6]
                            ),

                        AttackOverride =
                            ParseRuntimeDecimal(
                                parts[7]
                            ),

                        RealMovementSpeed =
                            ParseRuntimeDecimal(
                                parts[8]
                            ),

                        MovementOverride =
                            ParseRuntimeDecimal(
                                parts[9]
                            )
                    };


                newStates[
                    heroId
                ] =
                    state;
            }


            if (newStates.Count == 0)
            {
                return;
            }


            bool firstLoad =
                !heroRuntimeStateLoaded;


            heroRuntimeStates.Clear();


            foreach (
                KeyValuePair<int, HeroRuntimeState> pair
                in newStates
            )
            {
                heroRuntimeStates[
                    pair.Key
                ] =
                    pair.Value;
            }


            heroRuntimeStateLoaded =
                true;


            // En el primer estado válido cargamos también
            // el personaje que está seleccionado actualmente.

            if (firstLoad)
            {
                LoadSelectedHeroRuntimeValues();
            }
        }
        catch
        {
            // El plugin puede estar reemplazando el archivo
            // justo mientras Form1 intenta leerlo.
            //
            // Simplemente esperamos el próximo tick.
        }
    }

    private decimal? ParseRuntimeDecimal(
        string text
    )
    {
        if (
            string.IsNullOrWhiteSpace(text) ||
            string.Equals(
                text,
                "NA",
                StringComparison.OrdinalIgnoreCase
            ) ||
            string.Equals(
                text,
                "OFF",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return null;
        }


        if (
            decimal.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out decimal value
            )
        )
        {
            return value;
        }


        return null;
    }

    private void LoadSelectedHeroRuntimeValues()
    {
        try
        {
            if (
                !TryGetSelectedHero(
                    out HeroOption? hero
                ) ||
                hero == null
            )
            {
                return;
            }


            if (
                !heroRuntimeStates.TryGetValue(
                    hero.HeroKey,
                    out HeroRuntimeState? state
                ) ||
                state == null
            )
            {
                return;
            }


            // ========================================================
            // LEVEL
            // ========================================================

            SetNumericValueSafe(
                heroLevelInput,
                state.Level
            );


            // ========================================================
            // DAMAGE
            //
            // Si existe override mostramos ese.
            // Si no, mostramos el valor real/original.
            // ========================================================

            decimal? damageValue =
                state.DamageOverride ??
                state.OriginalDamage;


            if (damageValue.HasValue)
            {
                SetNumericValueSafe(
                    damageInput,
                    damageValue.Value
                );
            }


            // ========================================================
            // ATTACK SPEED
            // ========================================================

            decimal? attackValue =
                state.AttackOverride ??
                state.RealAttackSpeed;


            if (attackValue.HasValue)
            {
                SetNumericValueSafe(
                    attackSpeedInput,
                    attackValue.Value
                );
            }


            // ========================================================
            // MOVEMENT
            // ========================================================

            decimal? movementValue =
                state.MovementOverride ??
                state.RealMovementSpeed;


            if (movementValue.HasValue)
            {
                SetNumericValueSafe(
                    movementSpeedInput,
                    movementValue.Value
                );
            }
        }
        catch
        {
        }
    }

    private void SetNumericValueSafe(
        NumericUpDown input,
        decimal value
    )
    {
        if (value < input.Minimum)
        {
            value =
                input.Minimum;
        }


        if (value > input.Maximum)
        {
            value =
                input.Maximum;
        }


        input.Value =
            value;
    }

    // ============================================================
    // HERO RUNTIME STATE
    // ============================================================

    private sealed class HeroRuntimeState
    {
        public int HeroId { get; set; }

        public int Level { get; set; }

        public int OriginalLevel { get; set; }

        public bool Unlocked { get; set; }


        public decimal? OriginalDamage { get; set; }

        public decimal? DamageOverride { get; set; }


        public decimal? RealAttackSpeed { get; set; }

        public decimal? AttackOverride { get; set; }


        public decimal? RealMovementSpeed { get; set; }

        public decimal? MovementOverride { get; set; }
    }

    // ============================================================
    // HERO OPTION
    // ============================================================

    private sealed class HeroOption
    {
        public string Name { get; }

        public int HeroKey { get; }

        public HeroOption(
            string name,
            int heroKey
        )
        {
            Name =
                name;

            HeroKey =
                heroKey;
        }

        public override string ToString()
        {
            return
                $"{Name}  [{HeroKey}]";
        }
    }
}