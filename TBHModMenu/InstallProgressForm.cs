using System;
using System.Drawing;
using System.Windows.Forms;

namespace TBHModMenu;

internal sealed class InstallProgressForm : Form
{
    private readonly Label statusLabel;
    private readonly Label percentLabel;

    private readonly Panel progressTrack;
    private readonly Panel progressFill;

    private readonly TextBox logBox;

    private string lastStatus =
        string.Empty;


    public InstallProgressForm()
    {
        // ========================================================
        // FORM
        // ========================================================

        Text = "Taskbar Hero Mod Menu";

        Width =
            520;

        Height =
            340;

        StartPosition =
            FormStartPosition.CenterScreen;

        FormBorderStyle =
            FormBorderStyle.FixedDialog;

        MaximizeBox =
            false;

        MinimizeBox =
            false;

        TopMost =
            true;

        BackColor =
            Color.FromArgb(
                12,
                9,
                18
            );


        // ========================================================
        // HEADER
        // ========================================================

        Label title =
            new Label
            {
                Text = "TASKBAR HERO MOD MENU",

                ForeColor =
                    Color.White,

                Font =
                    new Font(
                        "Segoe UI",
                        16,
                        FontStyle.Bold
                    ),

                AutoSize =
                    true,

                Location =
                    new Point(
                        24,
                        22
                    )
            };


        Controls.Add(
            title
        );


        Label subtitle =
            new Label
            {
                Text = "Instalador • BepInEx • Launcher",

                ForeColor =
                    Color.FromArgb(
                        178,
                        118,
                        255
                    ),

                Font =
                    new Font(
                        "Segoe UI",
                        9,
                        FontStyle.Bold
                    ),

                AutoSize =
                    true,

                Location =
                    new Point(
                        26,
                        56
                    )
            };


        Controls.Add(
            subtitle
        );


        // ========================================================
        // STATUS
        // ========================================================

        statusLabel =
            new Label
            {
                Text = "Preparando instalación...",

                ForeColor =
                    Color.FromArgb(
                        244,
                        240,
                        250
                    ),

                Font =
                    new Font(
                        "Segoe UI",
                        10,
                        FontStyle.Bold
                    ),

                AutoSize =
                    false,

                Width =
                    390,

                Height =
                    24,

                Location =
                    new Point(
                        26,
                        96
                    )
            };


        Controls.Add(
            statusLabel
        );


        percentLabel =
            new Label
            {
                Text =
                    "0%",

                ForeColor =
                    Color.FromArgb(
                        178,
                        118,
                        255
                    ),

                Font =
                    new Font(
                        "Segoe UI",
                        10,
                        FontStyle.Bold
                    ),

                AutoSize =
                    false,

                TextAlign =
                    ContentAlignment.MiddleRight,

                Width =
                    60,

                Height =
                    24,

                Location =
                    new Point(
                        425,
                        96
                    )
            };


        Controls.Add(
            percentLabel
        );


        // ========================================================
        // PROGRESS BAR
        // ========================================================

        progressTrack =
            new Panel
            {
                BackColor =
                    Color.FromArgb(
                        37,
                        28,
                        51
                    ),

                Location =
                    new Point(
                        26,
                        130
                    ),

                Size =
                    new Size(
                        458,
                        12
                    )
            };


        Controls.Add(
            progressTrack
        );


        progressFill =
            new Panel
            {
                BackColor =
                    Color.FromArgb(
                        151,
                        82,
                        255
                    ),

                Location =
                    new Point(
                        0,
                        0
                    ),

                Size =
                    new Size(
                        0,
                        12
                    )
            };


        progressTrack.Controls.Add(
            progressFill
        );


        // ========================================================
        // LOG
        // ========================================================

        logBox =
            new TextBox
            {
                Multiline =
                    true,

                ReadOnly =
                    true,

                BorderStyle =
                    BorderStyle.None,

                ScrollBars =
                    ScrollBars.Vertical,

                BackColor =
                    Color.FromArgb(
                        20,
                        14,
                        30
                    ),

                ForeColor =
                    Color.FromArgb(
                        190,
                        175,
                        210
                    ),

                Font =
                    new Font(
                        "Consolas",
                        9
                    ),

                Location =
                    new Point(
                        26,
                        164
                    ),

                Size =
                    new Size(
                        458,
                        120
                    )
            };


        Controls.Add(
            logBox
        );


        SetProgress(
            "Preparando instalación...",
            0
        );
    }


    // ============================================================
    // UPDATE UI
    // ============================================================

    public void SetProgress(
        string status,
        int percent
    )
    {
        percent =
            Math.Clamp(
                percent,
                0,
                100
            );


        statusLabel.Text =
            status;


        percentLabel.Text =
            $"{percent}%";


        int width =
            (int)(
                progressTrack.ClientSize.Width *
                (
                    percent /
                    100f
                )
            );


        progressFill.Width =
            width;


        if (
            !string.Equals(
                lastStatus,
                status,
                StringComparison.Ordinal
            )
        )
        {
            logBox.AppendText(
                $"[{DateTime.Now:HH:mm:ss}] " +
                status +
                Environment.NewLine
            );


            lastStatus =
                status;


            logBox.SelectionStart =
                logBox.TextLength;


            logBox.ScrollToCaret();
        }


        Refresh();
    }
}