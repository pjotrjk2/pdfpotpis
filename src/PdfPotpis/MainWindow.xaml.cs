using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using Microsoft.Win32;
using PdfPotpis.Models;
using PdfPotpis.Services;

namespace PdfPotpis;

public partial class MainWindow : Window
{
    private const double MinZoom = 0.25;
    private const double MaxZoom = 3.0;
    private const double ZoomStep = 0.05;
    private const double ArrowScrollStep = 40;

    private readonly PdfDocumentService _document = new();
    private readonly PdfRenderService _renderer = new();
    private readonly CertificateService _certificates = new();
    private readonly PdfSignService _signer = new();

    private IReadOnlyList<PdfPageImage> _pages = Array.Empty<PdfPageImage>();
    private bool _placementMode;
    private Border? _stampPreview;
    private Canvas? _activeCanvas;
    private PdfPageImage? _activePage;
    private bool _isDragging;
    private Point _dragOffset;
    private bool _isPanning;
    private Point _panStart;
    private double _panOriginX;
    private double _panOriginY;
    private double _zoom = 1.0;
    private bool _updatingZoomUi;

    public MainWindow()
    {
        InitializeComponent();
        UpdateCommandState();
        ApplyZoom(1.0);
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Open, (_, _) => Open_Click(this, new RoutedEventArgs())));
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Save, (_, _) => Save_Click(this, new RoutedEventArgs())));
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Print, (_, _) => Print_Click(this, new RoutedEventArgs())));
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Otvori PDF",
            Filter = "PDF fajlovi (*.pdf)|*.pdf",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            CancelPlacementInternal();
            _document.Open(dialog.FileName);
            ReloadViewer();
            StatusText.Text = $"Otvoreno: {dialog.FileName}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Neuspešno otvaranje:{Environment.NewLine}{ex.Message}", "Greška",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }

        UpdateCommandState();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!_document.HasDocument)
        {
            return;
        }

        try
        {
            if (string.IsNullOrWhiteSpace(_document.FilePath))
            {
                SaveAs_Click(sender, e);
                return;
            }

            _document.Save();
            StatusText.Text = $"Sačuvano: {_document.FilePath}";
            UpdateCommandState();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Neuspešno čuvanje:{Environment.NewLine}{ex.Message}", "Greška",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        if (!_document.HasDocument)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Sačuvaj PDF kao",
            Filter = "PDF fajlovi (*.pdf)|*.pdf",
            AddExtension = true,
            DefaultExt = ".pdf",
            FileName = Path.GetFileName(_document.FilePath) ?? "dokument.pdf"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            _document.SaveAs(dialog.FileName);
            StatusText.Text = $"Sačuvano kao: {dialog.FileName}";
            UpdateCommandState();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Neuspešno čuvanje:{Environment.NewLine}{ex.Message}", "Greška",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        if (!_document.HasDocument || _pages.Count == 0)
        {
            MessageBox.Show(this, "Prvo otvorite PDF dokument.", "PDFPotpis",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var dialog = new PrintDialog();
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var document = BuildPrintDocument(dialog.PrintableAreaWidth, dialog.PrintableAreaHeight);
            dialog.PrintDocument(document.DocumentPaginator, Title);
            StatusText.Text = "Dokument je poslat na štampu.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Neuspešno štampanje:{Environment.NewLine}{ex.Message}", "Greška",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private FixedDocument BuildPrintDocument(double printableWidth, double printableHeight)
    {
        var document = new FixedDocument();
        document.DocumentPaginator.PageSize = new Size(printableWidth, printableHeight);

        foreach (PdfPageImage page in _pages)
        {
            double scale = Math.Min(
                printableWidth / Math.Max(page.Image.PixelWidth, 1),
                printableHeight / Math.Max(page.Image.PixelHeight, 1));
            double width = page.Image.PixelWidth * scale;
            double height = page.Image.PixelHeight * scale;

            var image = new Image
            {
                Source = page.Image,
                Width = width,
                Height = height,
                Stretch = Stretch.Uniform
            };
            FixedPage.SetLeft(image, (printableWidth - width) / 2);
            FixedPage.SetTop(image, (printableHeight - height) / 2);

            var fixedPage = new FixedPage
            {
                Width = printableWidth,
                Height = printableHeight
            };
            fixedPage.Children.Add(image);

            var pageContent = new PageContent();
            ((IAddChild)pageContent).AddChild(fixedPage);
            document.Pages.Add(pageContent);
        }

        return document;
    }

    private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded || _updatingZoomUi)
        {
            return;
        }

        ApplyZoom(e.NewValue);
    }

    private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
        {
            return;
        }

        double delta = e.Delta > 0 ? ZoomStep : -ZoomStep;
        ApplyZoom(_zoom + delta);
        e.Handled = true;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_document.HasDocument || PageHost.Children.Count == 0)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.PageDown:
                ScrollPageDown();
                e.Handled = true;
                break;
            case Key.PageUp:
                ScrollPageUp();
                e.Handled = true;
                break;
            case Key.Up:
                PdfScroll.ScrollToVerticalOffset(PdfScroll.VerticalOffset - ArrowScrollStep);
                e.Handled = true;
                break;
            case Key.Down:
                PdfScroll.ScrollToVerticalOffset(PdfScroll.VerticalOffset + ArrowScrollStep);
                e.Handled = true;
                break;
            case Key.Left:
                PdfScroll.ScrollToHorizontalOffset(PdfScroll.HorizontalOffset - ArrowScrollStep);
                e.Handled = true;
                break;
            case Key.Right:
                PdfScroll.ScrollToHorizontalOffset(PdfScroll.HorizontalOffset + ArrowScrollStep);
                e.Handled = true;
                break;
        }
    }

    private void ScrollPageDown()
    {
        const double tolerance = 3;
        IReadOnlyList<(double Top, double Bottom)> bounds = GetPageScrollBounds();
        if (bounds.Count == 0)
        {
            return;
        }

        double viewportTop = PdfScroll.VerticalOffset;
        double viewportHeight = PdfScroll.ViewportHeight;
        int pageIndex = FindPageAtViewportTop(bounds, viewportTop);
        (double top, double bottom) = bounds[pageIndex];
        double pageBottomOffset = Math.Max(top, bottom - viewportHeight);

        if (viewportTop >= pageBottomOffset - tolerance)
        {
            if (pageIndex + 1 < bounds.Count)
            {
                ScrollToVerticalOffset(bounds[pageIndex + 1].Top);
            }
        }
        else
        {
            ScrollToVerticalOffset(pageBottomOffset);
        }
    }

    private void ScrollPageUp()
    {
        const double tolerance = 3;
        IReadOnlyList<(double Top, double Bottom)> bounds = GetPageScrollBounds();
        if (bounds.Count == 0)
        {
            return;
        }

        double viewportTop = PdfScroll.VerticalOffset;
        double viewportHeight = PdfScroll.ViewportHeight;
        int pageIndex = FindPageAtViewportTop(bounds, viewportTop);
        (double top, double bottom) = bounds[pageIndex];

        if (viewportTop <= top + tolerance)
        {
            if (pageIndex > 0)
            {
                (double prevTop, double prevBottom) = bounds[pageIndex - 1];
                ScrollToVerticalOffset(Math.Max(prevTop, prevBottom - viewportHeight));
            }
            else
            {
                ScrollToVerticalOffset(0);
            }
        }
        else
        {
            ScrollToVerticalOffset(top);
        }
    }

    private void ScrollToVerticalOffset(double offset)
    {
        PdfScroll.ScrollToVerticalOffset(Math.Clamp(offset, 0, PdfScroll.ScrollableHeight));
    }

    private static int FindPageAtViewportTop(IReadOnlyList<(double Top, double Bottom)> bounds, double viewportTop)
    {
        for (int i = 0; i < bounds.Count; i++)
        {
            if (viewportTop < bounds[i].Bottom - 1)
            {
                return i;
            }
        }

        return bounds.Count - 1;
    }

    private IReadOnlyList<(double Top, double Bottom)> GetPageScrollBounds()
    {
        var result = new List<(double Top, double Bottom)>(PageHost.Children.Count);
        double viewportOffset = PdfScroll.VerticalOffset;

        foreach (object child in PageHost.Children)
        {
            if (child is not FrameworkElement page)
            {
                continue;
            }

            GeneralTransform transform = page.TransformToAncestor(PdfScroll);
            Rect rect = transform.TransformBounds(new Rect(page.RenderSize));
            result.Add((rect.Top + viewportOffset, rect.Bottom + viewportOffset));
        }

        return result;
    }

    private void PdfScroll_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_placementMode || !_document.HasDocument || e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        _isPanning = true;
        _panStart = e.GetPosition(PdfScroll);
        _panOriginX = PdfScroll.HorizontalOffset;
        _panOriginY = PdfScroll.VerticalOffset;
        PdfScroll.CaptureMouse();
        PdfScroll.Cursor = Cursors.ScrollAll;
        e.Handled = true;
    }

    private void PdfScroll_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isPanning)
        {
            return;
        }

        Point pos = e.GetPosition(PdfScroll);
        PdfScroll.ScrollToHorizontalOffset(_panOriginX - (pos.X - _panStart.X));
        PdfScroll.ScrollToVerticalOffset(_panOriginY - (pos.Y - _panStart.Y));
    }

    private void PdfScroll_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isPanning)
        {
            return;
        }

        EndPan();
        e.Handled = true;
    }

    private void PdfScroll_LostMouseCapture(object sender, MouseEventArgs e)
    {
        EndPan();
    }

    private void EndPan()
    {
        if (!_isPanning)
        {
            return;
        }

        _isPanning = false;
        if (PdfScroll.IsMouseCaptured)
        {
            PdfScroll.ReleaseMouseCapture();
        }

        UpdatePanCursor();
    }

    private void UpdatePanCursor()
    {
        PdfScroll.Cursor = !_placementMode && _document.HasDocument
            ? Cursors.Hand
            : Cursors.Arrow;
    }

    private void ApplyZoom(double zoom)
    {
        _zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        PageHost.LayoutTransform = new ScaleTransform(_zoom, _zoom);

        _updatingZoomUi = true;
        try
        {
            if (ZoomSlider is not null && Math.Abs(ZoomSlider.Value - _zoom) > 0.0001)
            {
                ZoomSlider.Value = _zoom;
            }

            if (ZoomLabel is not null)
            {
                ZoomLabel.Text = $"{(int)Math.Round(_zoom * 100)}%";
            }
        }
        finally
        {
            _updatingZoomUi = false;
        }
    }

    private void Sign_Click(object sender, RoutedEventArgs e)
    {
        if (!_document.HasDocument || _pages.Count == 0)
        {
            MessageBox.Show(this, "Prvo otvorite PDF dokument.", "PDFPotpis",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        EnterPlacementMode();
    }

    private void ConfirmSign_Click(object sender, RoutedEventArgs e)
    {
        if (!_placementMode || _document.PdfBytes is null || _activePage is null || _stampPreview is null || _activeCanvas is null)
        {
            return;
        }

        try
        {
            var certificate = _certificates.PickSigningCertificate(this);
            if (certificate is null)
            {
                StatusText.Text = "Potpisivanje otkazano — sertifikat nije izabran.";
                return;
            }

            SignaturePlacement placement = BuildPlacementFromPreview(_activePage, _activeCanvas, _stampPreview);
            byte[] signed = _signer.Sign(_document.PdfBytes, certificate, placement);
            _document.LoadBytes(signed, dirty: true);
            CancelPlacementInternal();
            ReloadViewer();
            StatusText.Text = "Dokument je potpisan. Sačuvajte fajl (Sačuvaj / Sačuvaj kao).";
            UpdateCommandState();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Neuspešno potpisivanje:{Environment.NewLine}{ex.Message}", "Greška",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CancelPlacement_Click(object sender, RoutedEventArgs e)
    {
        CancelPlacementInternal();
        StatusText.Text = "Postavljanje potpisa otkazano.";
        UpdateCommandState();
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var about = new AboutWindow { Owner = this };
        about.ShowDialog();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void EnterPlacementMode()
    {
        EndPan();
        CancelPlacementInternal();
        _placementMode = true;
        PlacementHint.Visibility = Visibility.Visible;
        BtnConfirmSign.Visibility = Visibility.Visible;
        BtnConfirmSign.IsEnabled = true;
        BtnCancelPlacement.Visibility = Visibility.Visible;
        BtnCancelPlacement.IsEnabled = true;
        MenuCancelPlacement.IsEnabled = true;

        PdfPageImage first = _pages[0];
        FrameworkElement? pageContainer = FindPageContainer(0);
        if (pageContainer is null)
        {
            return;
        }

        var canvas = (Canvas)((Grid)pageContainer).Children[1];
        _activeCanvas = canvas;
        _activePage = first;

        double stampW = first.Image.PixelWidth * (PdfSignService.DefaultStampWidthPts / first.PageWidthPts);
        double stampH = first.Image.PixelHeight * (PdfSignService.DefaultStampHeightPts / first.PageHeightPts);

        _stampPreview = CreateStampPreview(stampW, stampH);
        Canvas.SetLeft(_stampPreview, 40);
        Canvas.SetTop(_stampPreview, 40);
        canvas.Children.Add(_stampPreview);

        StatusText.Text = "Režim potpisa: prevucite okvir, zatim potvrdite.";
        UpdateCommandState();
        UpdatePanCursor();
    }

    private void CancelPlacementInternal()
    {
        _placementMode = false;
        _isDragging = false;
        PlacementHint.Visibility = Visibility.Collapsed;
        BtnConfirmSign.Visibility = Visibility.Collapsed;
        BtnConfirmSign.IsEnabled = false;
        BtnCancelPlacement.Visibility = Visibility.Collapsed;
        BtnCancelPlacement.IsEnabled = false;
        MenuCancelPlacement.IsEnabled = false;

        if (_stampPreview is not null && _activeCanvas is not null)
        {
            _activeCanvas.Children.Remove(_stampPreview);
        }

        _stampPreview = null;
        _activeCanvas = null;
        _activePage = null;
        UpdatePanCursor();
    }

    private Border CreateStampPreview(double width, double height)
    {
        var border = new Border
        {
            Width = width,
            Height = height,
            BorderBrush = new SolidColorBrush(Color.FromRgb(31, 77, 58)),
            BorderThickness = new Thickness(2),
            Background = new SolidColorBrush(Color.FromArgb(210, 247, 244, 236)),
            Cursor = Cursors.SizeAll,
            Child = new TextBlock
            {
                Text = "IME PREZIME … Sign\nhh:mm:ss dd.mm.yyyy.\nID: …",
                Margin = new Thickness(6),
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(28, 42, 31))
            }
        };

        border.MouseLeftButtonDown += Stamp_MouseLeftButtonDown;
        border.MouseLeftButtonUp += Stamp_MouseLeftButtonUp;
        border.MouseMove += Stamp_MouseMove;
        return border;
    }

    private void Stamp_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_stampPreview is null || _activeCanvas is null)
        {
            return;
        }

        _isDragging = true;
        _dragOffset = e.GetPosition(_stampPreview);
        _stampPreview.CaptureMouse();
        e.Handled = true;
    }

    private void Stamp_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_stampPreview is null)
        {
            return;
        }

        _isDragging = false;
        _stampPreview.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void Stamp_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || _stampPreview is null || _activeCanvas is null || _activePage is null)
        {
            return;
        }

        Point pos = e.GetPosition(_activeCanvas);
        double left = pos.X - _dragOffset.X;
        double top = pos.Y - _dragOffset.Y;

        left = Math.Clamp(left, 0, Math.Max(0, _activeCanvas.ActualWidth - _stampPreview.Width));
        top = Math.Clamp(top, 0, Math.Max(0, _activeCanvas.ActualHeight - _stampPreview.Height));

        Canvas.SetLeft(_stampPreview, left);
        Canvas.SetTop(_stampPreview, top);
        UpdateLivePreviewText();
    }

    private void UpdateLivePreviewText()
    {
        if (_stampPreview?.Child is not TextBlock text || _activePage is null || _activeCanvas is null)
        {
            return;
        }

        SignaturePlacement placement = BuildPlacementFromPreview(_activePage, _activeCanvas, _stampPreview);
        text.Text =
            $"Digitalni potpis (pregled){Environment.NewLine}" +
            $"Strana {_activePage.PageIndex + 1}{Environment.NewLine}" +
            $"X={placement.PdfX:0}, Y={placement.PdfY:0}";
    }

    private static SignaturePlacement BuildPlacementFromPreview(
        PdfPageImage page,
        Canvas canvas,
        Border stamp)
    {
        double leftPx = Canvas.GetLeft(stamp);
        double topPx = Canvas.GetTop(stamp);
        double scaleX = page.PageWidthPts / Math.Max(canvas.ActualWidth, 1);
        double scaleY = page.PageHeightPts / Math.Max(canvas.ActualHeight, 1);

        float pdfX = (float)(leftPx * scaleX);
        float widthPts = (float)(stamp.Width * scaleX);
        float heightPts = (float)(stamp.Height * scaleY);
        float pdfY = (float)(page.PageHeightPts - ((topPx + stamp.Height) * scaleY));

        return new SignaturePlacement
        {
            PageIndex = page.PageIndex,
            PdfX = pdfX,
            PdfY = pdfY,
            WidthPts = widthPts,
            HeightPts = heightPts
        };
    }

    private void ReloadViewer()
    {
        PageHost.Children.Clear();
        _pages = Array.Empty<PdfPageImage>();

        if (_document.PdfBytes is null)
        {
            return;
        }

        _pages = _renderer.RenderAllPages(_document.PdfBytes);

        foreach (PdfPageImage page in _pages)
        {
            var image = new Image
            {
                Source = page.Image,
                Width = page.Image.PixelWidth,
                Height = page.Image.PixelHeight,
                Stretch = Stretch.None
            };

            var canvas = new Canvas
            {
                Width = page.Image.PixelWidth,
                Height = page.Image.PixelHeight,
                Background = Brushes.Transparent
            };

            var grid = new Grid
            {
                Margin = new Thickness(0, 0, 0, 16),
                Tag = page.PageIndex
            };
            grid.Children.Add(image);
            grid.Children.Add(canvas);
            grid.MouseLeftButtonDown += Page_MouseLeftButtonDown;

            var frame = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(180, 174, 160)),
                BorderThickness = new Thickness(1),
                Child = grid
            };

            PageHost.Children.Add(frame);
        }
    }

    private void Page_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_placementMode || _stampPreview is null || sender is not Grid grid)
        {
            return;
        }

        if (e.OriginalSource is Border)
        {
            return;
        }

        int pageIndex = grid.Tag is int idx ? idx : 0;
        if (pageIndex < 0 || pageIndex >= _pages.Count)
        {
            return;
        }

        var canvas = (Canvas)grid.Children[1];
        if (!ReferenceEquals(canvas, _activeCanvas))
        {
            _activeCanvas?.Children.Remove(_stampPreview);
            _activeCanvas = canvas;
            _activePage = _pages[pageIndex];
            canvas.Children.Add(_stampPreview);
        }

        Point pos = e.GetPosition(canvas);
        double left = Math.Clamp(pos.X - _stampPreview.Width / 2, 0, Math.Max(0, canvas.ActualWidth - _stampPreview.Width));
        double top = Math.Clamp(pos.Y - _stampPreview.Height / 2, 0, Math.Max(0, canvas.ActualHeight - _stampPreview.Height));
        Canvas.SetLeft(_stampPreview, left);
        Canvas.SetTop(_stampPreview, top);
        UpdateLivePreviewText();
        e.Handled = true;
    }

    private FrameworkElement? FindPageContainer(int pageIndex)
    {
        foreach (var child in PageHost.Children)
        {
            if (child is Border border && border.Child is Grid grid && grid.Tag is int idx && idx == pageIndex)
            {
                return grid;
            }
        }

        return null;
    }

    private void UpdateCommandState()
    {
        bool hasDoc = _document.HasDocument;
        EmptyState.Visibility = hasDoc ? Visibility.Collapsed : Visibility.Visible;
        MenuSave.IsEnabled = hasDoc;
        MenuSaveAs.IsEnabled = hasDoc;
        MenuPrint.IsEnabled = hasDoc;
        MenuSign.IsEnabled = hasDoc && !_placementMode;
        BtnSave.IsEnabled = hasDoc;
        BtnSaveAs.IsEnabled = hasDoc;
        BtnPrint.IsEnabled = hasDoc;
        BtnSign.IsEnabled = hasDoc && !_placementMode;
        UpdatePanCursor();

        string title = "PDFPotpis";
        if (!string.IsNullOrWhiteSpace(_document.FilePath))
        {
            title += " — " + Path.GetFileName(_document.FilePath);
            if (_document.IsDirty)
            {
                title += " *";
            }
        }
        else if (hasDoc && _document.IsDirty)
        {
            title += " — (neimenovan) *";
        }

        Title = title;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_document.IsDirty)
        {
            MessageBoxResult result = MessageBox.Show(
                this,
                "Dokument ima nesačuvane izmene. Želite li da izađete bez čuvanja?",
                "PDFPotpis",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.No)
            {
                e.Cancel = true;
            }
        }

        base.OnClosing(e);
    }
}
