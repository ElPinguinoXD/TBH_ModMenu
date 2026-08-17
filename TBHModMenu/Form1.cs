using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TBHModMenu;

public class Form1 : Form
{
    // ============================================================
    // WIN32
    // ============================================================

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int X,
        int Y,
        int cx,
        int cy,
        uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(
        IntPtr hWnd,
        out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(
        int vKey);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(
        IntPtr hWnd);

    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

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
            FormStartPosition.Manual;

        ShowInTaskbar = false;

        TopMost = true;

        BackColor =
            backgroundColor;

        Opacity = 0.97;

        Width = 390;
        Height = 760;

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
        // PERSONAJE
        // ========================================================

        var heroLabel = new Label
        {
            Text =
                "Personaje",

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
                    16,
                    43
                )
        };

        heroCard.Controls.Add(heroLabel);

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
                        730
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
                        730
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

        IntPtr hwnd =
            gameProcess.MainWindowHandle;

        if (
            hwnd == IntPtr.Zero ||
            !IsWindow(hwnd)
        )
        {
            return;
        }

        if (
            !GetWindowRect(
                hwnd,
                out RECT rect
            )
        )
        {
            return;
        }

       int x =
            rect.Left + 20;

        int y =
            rect.Top + 20;


        // ============================================================
        // NO TOCAR LA VENTANA MIENTRAS SE USA EL SELECTOR DE HÉROE
        // ============================================================

        if (
            heroComboBox.Focused ||
            heroComboBox.DroppedDown
        )
        {
            return;
        }


        // ============================================================
        // SOLO REPOSICIONAR SI REALMENTE CAMBIÓ DE POSICIÓN
        // ============================================================

        if (
            Left == x &&
            Top == y
        )
        {
            return;
        }


        // SWP_NOSIZE      = 0x0001
        // SWP_NOZORDER    = 0x0004
        // SWP_NOACTIVATE  = 0x0010
        //
        // Total = 0x0015
        //
        // TopMost ya está configurado en el Form,
        // por eso NO necesitamos mandar HWND_TOPMOST
        // cada 50 ms.
        // ============================================================

        SetWindowPos(
            Handle,
            IntPtr.Zero,
            x,
            y,
            0,
            0,
            0x0015
        );
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

        gameProcess?.Dispose();

        base.OnFormClosing(e);
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