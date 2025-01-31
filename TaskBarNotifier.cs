using System;
using System.Drawing;
using System.Windows.Forms;
using System.ComponentModel;

namespace NativeService
{
    class TaskBarNotifier : Form
    {
        private readonly NotifyIcon trayIcon;
        private readonly ContextMenu trayMenu; //TODO: Dispose?

        public TaskBarNotifier()
        {
            // Create a simple tray menu with only one item
            trayMenu = new ContextMenu();
            trayMenu.MenuItems.Add("Exit", OnExit);

            // Create a tray icon
            trayIcon = new NotifyIcon
            {
                Text = "Medical Device Service",
                Icon = new Icon(SystemIcons.Application, 40, 40),

                // Add menu to tray icon and show it
                ContextMenu = trayMenu,
                Visible = true
            };
        }

        public void ShowBalloonTip(string msg, int duration = 500, string hoverText = null)// ToolTipIcon icon = ToolTipIcon.Info, string title = "")
        {
            if (hoverText == null)
                hoverText = msg;

            trayIcon.BalloonTipText = msg;
            trayIcon.BalloonTipIcon = ToolTipIcon.Info;//icon;
            trayIcon.BalloonTipTitle = "";//title; //if you want the icon to show, the title must not be empty
            trayIcon.ShowBalloonTip(duration);

             ChangeIconHoverText(hoverText); //can be 64 chars at most
        }

        public void ChangeIconHoverText(string msg) //CAN BE MAXIMUM OF 64 CHARS!
        {
            trayIcon.Text = msg;
        }

        /*public void ChangeBalloonTipText(string newMsg)
        {
            trayIcon.BalloonTipText = "newMsg";
        }*/

        protected override void OnLoad(EventArgs e)
        {
            Visible = false; // Hide form window
            ShowInTaskbar = false; // Remove from taskbar

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
                // Release the icon resource.
                trayIcon.Dispose();
            }

            base.Dispose(isDisposing);
        }
    }
}
