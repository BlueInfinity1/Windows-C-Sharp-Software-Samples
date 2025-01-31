using System;
using System.Drawing;
using System.Windows.Forms;
using System.ComponentModel;

namespace NativeService
{
    class TaskBarNotifier : Form
    {
        private readonly NotifyIcon trayIcon;
        private readonly ContextMenu trayMenu;

        public TaskBarNotifier()
        {
            // Create a simple tray menu with an exit option.
            trayMenu = new ContextMenu();
            trayMenu.MenuItems.Add("Exit", OnExit);

            // Create the tray icon.
            trayIcon = new NotifyIcon
            {
                Text = "Medical Device Service",
                Icon = new Icon(SystemIcons.Application, 40, 40),
                ContextMenu = trayMenu,
                Visible = true
            };
        }

        public void ShowBalloonTip(string msg, int duration = 500, string hoverText = null)
        {
            hoverText ??= msg;

            trayIcon.BalloonTipText = msg;
            trayIcon.BalloonTipIcon = ToolTipIcon.Info;
            trayIcon.BalloonTipTitle = ""; // Title must not be empty for the icon to show.
            trayIcon.ShowBalloonTip(duration);

            ChangeIconHoverText(hoverText); // Hover text can be a max of 64 chars.
        }

        public void ChangeIconHoverText(string msg)
        {
            if (msg.Length > 64)
                msg = msg.Substring(0, 64);
            trayIcon.Text = msg;
        }

        protected override void OnLoad(EventArgs e)
        {
            Visible = false; // Hide form window.
            ShowInTaskbar = false; // Remove from taskbar.

            base.OnLoad(e);
        }

        private void OnExit(object sender, EventArgs e)
        {
            Application.Exit();
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                trayIcon.Dispose();
                trayMenu.Dispose();
            }

            base.Dispose(isDisposing);
        }
    }
}
