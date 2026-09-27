using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using TableSnap.Capture;
using TableSnap.Export;
using TableSnap.Models;
using TableSnap.Recognition;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using TextBox = System.Windows.Controls.TextBox;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;

namespace TableSnap;

public partial class MainWindow : Window
{
    private const int HotkeyId = 0x5442;
    private const int WmHotkey = 0x0312;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint Vk2 = 0x32;
    private IntPtr _handle;
    private System.Windows.Forms.NotifyIcon? _tray;
    private TableDocument? _table;
    private bool _reallyClose;
    private bool _busy;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        Loaded += (_, _) => SetupTray();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _handle = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(_handle)?.AddHook(WindowMessageHook);
        if (!RegisterHotKey(_handle, HotkeyId, ModControl | ModShift, Vk2))
            SetStatus("Could not register Ctrl+Shift+2. Another app may be using it. Use Capture Region instead.");
    }

    private IntPtr WindowMessageHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            _ = CaptureAndRecognizeAsync();
        }
        return IntPtr.Zero;
    }

    private void SetupTray()
    {
        if (_tray is not null) return;
        _tray = new System.Windows.Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "TableSnap",
            Visible = true,
            ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip()
        };
        _tray.DoubleClick += (_, _) => ShowWindow();
        _tray.ContextMenuStrip.Items.Add("Open TableSnap", null, (_, _) => ShowWindow());
        _tray.ContextMenuStrip.Items.Add("Capture Region", null, (_, _) => Dispatcher.InvokeAsync(() => _ = CaptureAndRecognizeAsync()));
        _tray.ContextMenuStrip.Items.Add("Exit", null, (_, _) => Dispatcher.InvokeAsync(ExitApp));
    }

    private void ShowWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void SetStatus(string message) => StatusText.Text = message;

    private async void Capture_Click(object sender, RoutedEventArgs e) => await CaptureAndRecognizeAsync();

    private async Task CaptureAndRecognizeAsync()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            Hide();
            await Task.Delay(250);
            var image = ScreenRegionPicker.PickPng();
            ShowWindow();
            if (image is null) { SetStatus("Capture cancelled."); return; }
            await RecognizeImageAsync(image);
        }
        catch (Exception ex)
        {
            ShowWindow();
            SetStatus("Capture failed: " + ex.Message);
        }
        finally { _busy = false; }
    }

    private async void OpenImage_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var dialog = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp|All files|*.*" };
        if (dialog.ShowDialog(this) != true) return;
        _busy = true;
        try { await RecognizeImageAsync(await File.ReadAllBytesAsync(dialog.FileName)); }
        catch (Exception ex) { SetStatus("Could not open or recognize the image: " + ex.Message); }
        finally { _busy = false; }
    }

    private async Task RecognizeImageAsync(byte[] imageBytes)
    {
        var bitmap = new BitmapImage();
        using (var stream = new MemoryStream(imageBytes))
        {
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
        }
        bitmap.Freeze();
        SourceImage.Source = bitmap;
        _table = null;
        TableGrid.Children.Clear();

        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _table = null;
            TableGrid.Children.Clear();
            SetStatus("Image loaded. Set OPENAI_API_KEY and try again, or open Demo Table to preview the interface.");
            return;
        }

        SetStatus("Recognizing table structure...");
        var pngBytes = EncodeAsPng(bitmap);
        var model = Environment.GetEnvironmentVariable("TABLESNAP_MODEL") ?? "gpt-4o";
        var recognizer = new OpenAiTableRecognizer(apiKey, model);
        _table = await recognizer.RecognizeAsync(pngBytes);
        RenderTable();
        SetStatus($"Recognition complete: {_table.Rows} rows x {_table.Columns} columns. Review before exporting.");
    }

    private static byte[] EncodeAsPng(BitmapSource bitmap)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private void Demo_Click(object sender, RoutedEventArgs e)
    {
        _table = TableDocument.Demo();
        SourceImage.Source = null;
        RenderTable();
        SetStatus("Demo table loaded. Edit cells, copy the table, or export an XLSX file.");
    }

    private void RenderTable()
    {
        TableGrid.Children.Clear();
        TableGrid.RowDefinitions.Clear();
        TableGrid.ColumnDefinitions.Clear();
        if (_table is null) return;

        for (var row = 0; row < _table.Rows; row++)
            TableGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto, MinHeight = 42 });
        for (var column = 0; column < _table.Columns; column++)
            TableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });

        foreach (var cell in _table.Cells)
        {
            var editor = new TextBox
            {
                Text = cell.Text,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                FontWeight = cell.Bold ? FontWeights.SemiBold : FontWeights.Normal,
                TextAlignment = cell.Align switch
                {
                    "center" => TextAlignment.Center,
                    "right" => TextAlignment.Right,
                    _ => TextAlignment.Left
                },
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(9, 6, 9, 6),
                MinHeight = 40
            };
            editor.TextChanged += (_, _) => cell.Text = editor.Text;
            var border = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(220, 226, 234)),
                BorderThickness = new Thickness(0, 0, 1, 1),
                Background = cell.Bold ? new SolidColorBrush(Color.FromRgb(245, 247, 250)) : Brushes.White,
                Child = editor
            };
            Grid.SetRow(border, cell.Row);
            Grid.SetColumn(border, cell.Column);
            Grid.SetRowSpan(border, cell.RowSpan);
            Grid.SetColumnSpan(border, cell.ColumnSpan);
            TableGrid.Children.Add(border);
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (_table is null) { SetStatus("Recognize an image or open the demo table first."); return; }
        try
        {
            ClipboardExporter.Copy(_table);
            SetStatus("Table copied. Paste it into Word or Excel.");
        }
        catch (Exception ex) { SetStatus("Copy failed: " + ex.Message); }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (_table is null) { SetStatus("Recognize an image or open the demo table first."); return; }
        var dialog = new SaveFileDialog { Filter = "Excel workbook|*.xlsx", FileName = "table.xlsx" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            XlsxExporter.Save(_table, dialog.FileName);
            SetStatus("Exported: " + dialog.FileName);
        }
        catch (Exception ex) { SetStatus("Export failed: " + ex.Message); }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_reallyClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        if (_handle != IntPtr.Zero) UnregisterHotKey(_handle, HotkeyId);
        _tray?.Dispose();
        base.OnClosing(e);
    }

    private void ExitApp()
    {
        _reallyClose = true;
        Close();
        System.Windows.Application.Current.Shutdown();
    }
}
