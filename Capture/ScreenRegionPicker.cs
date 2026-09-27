using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.IO;

namespace TableSnap.Capture;

public static class ScreenRegionPicker
{
    public static byte[]? PickPng()
    {
        var virtualScreen = SystemInformation.VirtualScreen;
        using var screenshot = new Bitmap(virtualScreen.Width, virtualScreen.Height);
        using (var graphics = Graphics.FromImage(screenshot))
            graphics.CopyFromScreen(virtualScreen.Location, Point.Empty, virtualScreen.Size);

        using var overlay = new SelectionForm(screenshot, virtualScreen);
        if (overlay.ShowDialog() != DialogResult.OK || overlay.SelectedRegion is not Rectangle selected)
            return null;

        using var cropped = new Bitmap(selected.Width, selected.Height);
        using (var graphics = Graphics.FromImage(cropped))
            graphics.DrawImage(screenshot, new Rectangle(0, 0, cropped.Width, cropped.Height), selected,
                GraphicsUnit.Pixel);
        using var stream = new MemoryStream();
        cropped.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        return stream.ToArray();
    }

    private sealed class SelectionForm : Form
    {
        private readonly Bitmap _screenshot;
        private Point? _start;
        private Point _current;

        public Rectangle? SelectedRegion { get; private set; }

        public SelectionForm(Bitmap screenshot, Rectangle virtualScreen)
        {
            _screenshot = screenshot;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = virtualScreen;
            TopMost = true;
            ShowInTaskbar = false;
            DoubleBuffered = true;
            KeyPreview = true;
            Cursor = Cursors.Cross;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.DrawImageUnscaled(_screenshot, 0, 0);
            using var shade = new SolidBrush(Color.FromArgb(110, 0, 0, 0));
            e.Graphics.FillRectangle(shade, ClientRectangle);
            if (_start is null) return;

            var region = MakeRectangle(_start.Value, _current);
            if (region.Width < 1 || region.Height < 1) return;
            e.Graphics.DrawImage(_screenshot, region, region, GraphicsUnit.Pixel);
            using var pen = new Pen(Color.DodgerBlue, 2) { DashStyle = DashStyle.Solid };
            e.Graphics.DrawRectangle(pen, region);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            _start = e.Location;
            _current = e.Location;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_start is null) return;
            _current = e.Location;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_start is null || e.Button != MouseButtons.Left) return;
            var region = Rectangle.Intersect(MakeRectangle(_start.Value, e.Location), ClientRectangle);
            if (region.Width < 8 || region.Height < 8) return;
            SelectedRegion = region;
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Escape) return;
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private static Rectangle MakeRectangle(Point a, Point b) =>
            Rectangle.FromLTRB(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y),
                Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
    }
}
