﻿using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

using Timer = System.Threading.Timer;

namespace PictureInPicture
{
    public partial class Pip : Form
    {
        // Constants that define what the mouse analog should look like.
        // Starting with floats because lots of division will happen later
        //  prevents rounding errors like intergers would have.
        const float CircleRadius = 15;
        const float DotRadius = 3;
        const float PenWidth = 2;

        // The drawing tools that will be used to draw the mouse analog.
        private readonly Color transparentRed;
        private readonly Brush brush;
        private readonly Pen pen;

        // The image tools that will be used to show the screen capture.
        private readonly object captureLock = new object();
        private Graphics graphics;
        private Rectangle bounds;
        private Bitmap image;

        // Timer for automatically triggering the screen capture.
        private readonly Timer timer;

        // The margin for hitting the screen resize.
        private Rectangle HitTop;
        private Rectangle HitLeft;
        private Rectangle HitBottom;
        private Rectangle HitRight;
        private Rectangle HitTopLeft;
        private Rectangle HitTopRight;
        private Rectangle HitBottomLeft;
        private Rectangle HitBottomRight;
        private const int hitMargin = 10;
        
        public Pip()
        {
            // Setup the windows as designed in the drag-and-drop designer.
            //  code is automatically generated by Visual Studio as a convenience.
            InitializeComponent();
            SetStyle(ControlStyles.ResizeRedraw, true);

            // Pull some setup info from the settings.
            Size = Properties.Settings.Default.LastSize;
            Location = Properties.Settings.Default.LastPosition;
            TopMost = Properties.Settings.Default.AlwaysOnTop;

            // Setup the drawing tools.
            // Colors can be defined as alpha, red, green, and blue values between 0 and 255.
            transparentRed = Color.FromArgb(255 / 4, 255, 0, 0);
            brush = new SolidBrush(transparentRed);
            pen = new Pen(Color.Black);

            // Calculate rectangles that make up the resize areas.
            CalculateHitRectangles();

            // Set the capture screen to the default.
            SetCaptureScreen(Properties.Settings.Default.CaptureScreenIndex);

            // Begin a loop of capturing a screenshot and showing it in  window.
            // Use a PictureBox to render the bitmap screenshot.
            timer = new Timer(CaptureScreen);
            SetupCaptureTimer(Properties.Settings.Default.RefreshDelay);
        }

        // Get the size of the secondary window.
        // Create a bitmap matching that size.
        // Then create a Graphics object that is connected to the bitmap.
        // The bitmap's Graphics will allow us to copy from the screen.
        // Set the PictureBox's image to the new Bitmap.
        public void SetCaptureScreen(int screenIndex)
        {
            // Save the new setting if it is changed.
            if (screenPicture.Image == null ||
                screenIndex != Properties.Settings.Default.CaptureScreenIndex)
            {
                // Lock the capture objects so they aren't used for taking a screen shot while being updated.
                lock (captureLock)
                {
                    bounds = Screen.AllScreens[screenIndex].Bounds;
                    image = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
                    graphics = Graphics.FromImage(image);
                    screenPicture.Image = image;
                }

                Properties.Settings.Default.CaptureScreenIndex = screenIndex;
                Properties.Settings.Default.Save();
            }
        }

        // Set the interval of the capture timer.
        public void SetupCaptureTimer(float interval)
        {
            timer.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(interval));

            // Save the new settings if it is changed.
            if (interval != Properties.Settings.Default.RefreshDelay)
            {
                Properties.Settings.Default.RefreshDelay = interval;
                Properties.Settings.Default.Save();
            }
        }

        // Sets and saves if the form is top most.
        public void SaveTopMost(bool topMost)
        {
            TopMost = topMost;
            if (Properties.Settings.Default.AlwaysOnTop != topMost)
            {
                Properties.Settings.Default.AlwaysOnTop = topMost;
                Properties.Settings.Default.Save();
            }
        }

        // Capture a screenshot.
        private void CaptureScreen(object state)
        {
            // Use a try-catch just in case there are any exceptions.
            // If there are exceptions ignore them and just try again on the next frame.
            try
            {
                // Lock the capture objects so they aren't changed while taking a screen shot.
                lock (captureLock)
                {
                    // Copy a screenshot from the secondary windows.
                    graphics.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size);

                    // Check if the mouse is on the second window.
                    // If so draw the mouse analog.
                    var position = Cursor.Position;
                    if (bounds.Contains(position))
                    {
                        // The bitmap coordinates start at 0, 0.
                        // The secondary screen's coordinates probably start somewhere more like 1920, 0.
                        // The mouse's coordinates need to be translated so they are accurate on the bitmap.
                        var x = position.X - bounds.X;
                        var y = position.Y - bounds.Y;

                        // The mouse analog will be drawn directly on the bitmap.
                        // Since the bitmap will be scaled down to fit to PictureBox so will the mouse analog.
                        // The mouse analog should stay a consistent size, so it needs to be scaled up.
                        // Then when the bitmap is scaled down the mouse analog will be the correct size.
                        // The scaleing factor can simply be calculated as the ratio of the bitmap's size to the PictureBox's.
                        // However, the bitmap's aspect ratio doesn't have to match the PictureBox's.
                        // Therefore, the max between the width and height ratios is used.
                        var scaleFactor = Math.Max((float)bounds.Width / screenPicture.Width,
                                              (float)bounds.Height / screenPicture.Height);

                        // Scale the circle's radius.
                        // Then define a square that circumscribes the desired circle.
                        // The square should be centered around the mouse position.
                        // Therefore, the X and Y of the square need to be offset by radius / 2.
                        float scaledCircleRadius = CircleRadius * scaleFactor;
                        var circle = new RectangleF(x - scaledCircleRadius / 2, y - scaledCircleRadius / 2,
                                                    scaledCircleRadius, scaledCircleRadius);

                        // Same as for the circle, just for the smaller center dot.
                        float scaledDotRadius = DotRadius * scaleFactor;
                        var dot = new RectangleF(x - scaledDotRadius / 2, y - scaledDotRadius / 2,
                                                 scaledDotRadius, scaledDotRadius);

                        // Adjust the pen's witdh using the scaling factor too.
                        //  keeps the border a consistent thickness.
                        pen.Width = PenWidth * scaleFactor;

                        // Finally draw the mouse analog.
                        graphics.FillEllipse(brush, circle);
                        graphics.DrawEllipse(pen, circle);
                        graphics.FillEllipse(Brushes.Black, dot);
                    }
                }

                // Force the PictureBox to redraw to it will show the new bitmap image.
                screenPicture.BeginInvoke(new Action(() => screenPicture.Refresh()));
            }
            catch { }
        }

        // Runs when the close options is selected in the right click menu.
        private void CloseClick(object sender, EventArgs e)
        {
            // Save the settings before closing.
            Properties.Settings.Default.Save();

            // Stop the timer from triggering another screen capture.
            timer.Change(Timeout.Infinite, Timeout.Infinite);

            // Finally close the window.
            Close();
        }

        // Runs when the settings options is selected in the right click menu.
        private void OnSettings(object sender, EventArgs e)
        {
            bool oldTopMost = TopMost;
            TopMost = false;

            if (new Settings(this).ShowDialog() != DialogResult.Yes)
            {
                TopMost = oldTopMost;
            }
        }

        // Allows the screen to be moved whenever there is a mouse click.
        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            // If a left mouse click happens anywhere on the window tell the window it was the top bar.
            //  lets us move the window anytime it is clicked and dragged.
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        // When the window is resized the resize hit rectangles must be recalculated
        // Also, the size will be saved.
        private void OnClientResize(object sender, EventArgs e)
        {
            CalculateHitRectangles();
            Properties.Settings.Default.LastSize = Size;
            Properties.Settings.Default.Save();
        }

        // When the window is moved save the setting.
        private void OnMove(object sender, EventArgs e)
        {
            Properties.Settings.Default.LastPosition = Location;
            Properties.Settings.Default.Save();
        }

        // Used (re)calculate the size of the resize areaa.
        private void CalculateHitRectangles()
        {
            HitTop = new Rectangle(0, 0, ClientSize.Width, hitMargin);
            HitLeft = new Rectangle(0, 0, hitMargin, ClientSize.Height);
            HitBottom = new Rectangle(0, ClientSize.Height - hitMargin, ClientSize.Width, hitMargin);
            HitRight = new Rectangle(ClientSize.Width - hitMargin, 0, hitMargin, ClientSize.Height);
            HitTopLeft = new Rectangle(0, 0, hitMargin, hitMargin);
            HitTopRight = new Rectangle(ClientSize.Width - hitMargin, 0, hitMargin, hitMargin);
            HitBottomLeft = new Rectangle(0, ClientSize.Height - hitMargin, hitMargin, hitMargin);
            HitBottomRight = new Rectangle(ClientSize.Width - hitMargin, ClientSize.Height - hitMargin, hitMargin, hitMargin);
        }

        // Overrides the default behavior when testing if the mouse in in the area to resize.
        protected override void WndProc(ref Message message)
        {
            // First run the original WndProc.
            base.WndProc(ref message);

            // If the message is a hit test check for resize area hit ourselves.
            if (message.Msg == WM_NCHITTEST)
            {
                var cursor = PointToClient(Cursor.Position);

                if (HitTopLeft.Contains(cursor)) message.Result = (IntPtr)HTTOPLEFT;
                else if (HitTopRight.Contains(cursor)) message.Result = (IntPtr)HTTOPRIGHT;
                else if (HitBottomLeft.Contains(cursor)) message.Result = (IntPtr)HTBOTTOMLEFT;
                else if (HitBottomRight.Contains(cursor)) message.Result = (IntPtr)HTBOTTOMRIGHT;

                else if (HitTop.Contains(cursor)) message.Result = (IntPtr)HTTOP;
                else if (HitLeft.Contains(cursor)) message.Result = (IntPtr)HTLEFT;
                else if (HitRight.Contains(cursor)) message.Result = (IntPtr)HTRIGHT;
                else if (HitBottom.Contains(cursor)) message.Result = (IntPtr)HTBOTTOM;
            }
        }

        #region Native Code

        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;

        private const int WM_NCHITTEST = 0x84;
        private const int WM_NCLBUTTONDOWN = 0xA1;

        private const int HT_CAPTION = 0x2;

        [DllImportAttribute("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd,
                         int Msg, int wParam, int lParam);
        [DllImportAttribute("user32.dll")]
        public static extern bool ReleaseCapture();

        #endregion

    }
}
