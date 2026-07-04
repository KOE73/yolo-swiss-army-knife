using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using YoloHelperApp.ViewModels;
using YoloHelperApp.Models;

namespace YoloHelperApp.Views;

public partial class AnalyzeView : UserControl
{
    private AnalyzeViewModel? _viewModel;

    private readonly Dictionary<string, IBrush> _brushCache = new();

    public AnalyzeView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.ChartInvalidated -= RedrawChart;
        }

        _viewModel = DataContext as AnalyzeViewModel;
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.ChartInvalidated += RedrawChart;
            RedrawChart();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AnalyzeViewModel.SelectedRun))
            RedrawChart();
    }

    private void OnCanvasSizeChanged(object? sender, SizeChangedEventArgs e) => RedrawChart();

    private void RedrawChart()
    {
        ChartCanvas.Children.Clear();

        if (_viewModel == null) return;

        var activeRuns = _viewModel.Runs.Where(r => r.IsSelectedForChart).ToList();
        if (activeRuns.Count == 0)
        {
            DrawMessage("Check at least one run in the list on the left.");
            return;
        }

        var enabledToggles = _viewModel.MetricToggles.Where(t => t.IsEnabled).ToList();
        if (enabledToggles.Count == 0)
        {
            DrawMessage("Toggle at least one metric in the panel above.");
            return;
        }

        double maxLoss = 0.001;
        double maxOther = 0.001;
        bool hasPerf = false;
        bool hasLoss = false;
        bool hasOther = false;
        int maxEpochs = 1;

        var curves = new List<(TrainRun Run, MetricSeries Series, MetricToggle Toggle, int ScaleType)>();

        foreach (var run in activeRuns)
        {
            foreach (var toggle in enabledToggles)
            {
                var s = run.Metrics.FirstOrDefault(m => m.Name == toggle.Name);
                if (s != null && s.Values.Count > 0)
                {
                    int scaleType = 2; // Default to LR/Other
                    double localMax = s.Values.Max();

                    if (toggle.Group == "Performance")
                    {
                        scaleType = 0;
                        hasPerf = true;
                    }
                    else if (toggle.Group == "Val Loss" || toggle.Group == "Train Loss")
                    {
                        scaleType = 1;
                        hasLoss = true;
                        if (localMax > maxLoss) maxLoss = localMax;
                    }
                    else
                    {
                        scaleType = 2;
                        hasOther = true;
                        if (localMax > maxOther) maxOther = localMax;
                    }

                    if (s.Values.Count > maxEpochs)
                    {
                        maxEpochs = s.Values.Count;
                    }

                    curves.Add((run, s, toggle, scaleType));
                }
            }
        }

        if (curves.Count == 0)
        {
            DrawMessage("No metric data found for selected runs/metrics.");
            return;
        }

        double width = ChartCanvas.Bounds.Width;
        double height = ChartCanvas.Bounds.Height;
        if (width <= 0 || height <= 0) return;

        double marginL = 30.0;
        if (hasPerf) marginL += 35.0; // Axis 1
        if (hasLoss) marginL += 45.0; // Axis 2
        double marginR = hasOther ? 50.0 : 15.0; // Axis 3

        double marginT = 20.0;
        double marginB = 30.0;
        double chartW = width - marginL - marginR;
        double chartH = height - marginT - marginB;

        DrawGrid(marginL, marginR, marginT, marginB, chartW, chartH, width, height,
                 hasPerf, hasLoss, hasOther, maxLoss, maxOther, maxEpochs);

        foreach (var (run, series, toggle, scaleType) in curves)
        {
            var runBrush = GetBrush(run.Color);
            int count = series.Values.Count;
            double xStep = count > 1 ? chartW / (maxEpochs - 1) : chartW;

            double valRange = 1.0;
            if (scaleType == 1) valRange = maxLoss;
            else if (scaleType == 2) valRange = maxOther;

            var polyline = new Polyline
            {
                Stroke = runBrush,
                StrokeThickness = toggle.IsPrimary ? 2.2 : 1.2
            };

            if (toggle.Group == "Val Loss")
            {
                polyline.StrokeDashArray = new Avalonia.Collections.AvaloniaList<double>(new[] { 5.0, 3.0 });
            }
            else if (toggle.Group == "Train Loss")
            {
                polyline.StrokeDashArray = new Avalonia.Collections.AvaloniaList<double>(new[] { 1.5, 3.0 });
            }
            else if (toggle.Group == "LR" || toggle.Group == "Other")
            {
                polyline.StrokeDashArray = new Avalonia.Collections.AvaloniaList<double>(new[] { 6.0, 3.0, 2.0, 3.0 });
            }

            for (int i = 0; i < count; i++)
            {
                double val = series.Values[i];
                double x = marginL + i * xStep;
                double ratio = val / valRange;
                if (ratio < 0) ratio = 0;
                if (ratio > 1.1) ratio = 1.1;
                double y = height - marginB - (ratio * chartH);
                polyline.Points.Add(new Point(x, y));
            }

            ChartCanvas.Children.Add(polyline);
        }

        DrawLegend(curves, marginL + 12, marginT + 6);
    }

    private void DrawGrid(double mL, double mR, double mT, double mB,
        double cW, double cH, double w, double h,
        bool hasPerf, bool hasLoss, bool hasOther,
        double maxLoss, double maxOther, int epochs)
    {
        var gridBrush = new SolidColorBrush(Color.Parse("#F1F5F9"));
        var axisBrush = new SolidColorBrush(Color.Parse("#CBD5E1"));
        var textBrush = new SolidColorBrush(Color.Parse("#64748B"));

        int steps = 5;

        for (int i = 0; i <= steps; i++)
        {
            double ratio = (double)i / steps;
            double y = h - mB - ratio * cH;

            if (i > 0 && i < steps)
            {
                ChartCanvas.Children.Add(new Line
                {
                    StartPoint = new Point(mL, y),
                    EndPoint = new Point(w - mR, y),
                    Stroke = gridBrush, StrokeThickness = 1
                });
            }
        }

        if (hasPerf)
        {
            double axX = 35.0;
            var perfBrush = new SolidColorBrush(Color.Parse("#2563EB"));
            ChartCanvas.Children.Add(new Line { StartPoint = new Point(axX, mT), EndPoint = new Point(axX, h - mB), Stroke = perfBrush, StrokeThickness = 1 });
            for (int i = 0; i <= steps; i++)
            {
                double ratio = (double)i / steps;
                double y = h - mB - ratio * cH;
                var lbl = new TextBlock { Text = ratio.ToString("F1"), FontSize = 8, Foreground = perfBrush };
                Canvas.SetLeft(lbl, axX - 25);
                Canvas.SetTop(lbl, y - 6);
                ChartCanvas.Children.Add(lbl);
            }
            var title = new TextBlock { Text = "Perf", FontSize = 9, FontWeight = FontWeight.Bold, Foreground = perfBrush };
            Canvas.SetLeft(title, axX - 12);
            Canvas.SetTop(title, mT - 14);
            ChartCanvas.Children.Add(title);
        }

        if (hasLoss)
        {
            double axX = hasPerf ? 80.0 : 35.0;
            var lossBrush = new SolidColorBrush(Color.Parse("#F97316"));
            ChartCanvas.Children.Add(new Line { StartPoint = new Point(axX, mT), EndPoint = new Point(axX, h - mB), Stroke = lossBrush, StrokeThickness = 1 });
            for (int i = 0; i <= steps; i++)
            {
                double ratio = (double)i / steps;
                double y = h - mB - ratio * cH;
                double val = ratio * maxLoss;
                var lbl = new TextBlock { Text = val.ToString("F2"), FontSize = 8, Foreground = lossBrush };
                Canvas.SetLeft(lbl, axX - 30);
                Canvas.SetTop(lbl, y - 6);
                ChartCanvas.Children.Add(lbl);
            }
            var title = new TextBlock { Text = "Loss", FontSize = 9, FontWeight = FontWeight.Bold, Foreground = lossBrush };
            Canvas.SetLeft(title, axX - 12);
            Canvas.SetTop(title, mT - 14);
            ChartCanvas.Children.Add(title);
        }

        if (hasOther)
        {
            double axX = w - 40.0;
            var otherBrush = new SolidColorBrush(Color.Parse("#06B6D4"));
            ChartCanvas.Children.Add(new Line { StartPoint = new Point(axX, mT), EndPoint = new Point(axX, h - mB), Stroke = otherBrush, StrokeThickness = 1 });
            for (int i = 0; i <= steps; i++)
            {
                double ratio = (double)i / steps;
                double y = h - mB - ratio * cH;
                double val = ratio * maxOther;
                string format = val < 0.01 ? "E2" : "F3";
                var lbl = new TextBlock { Text = val.ToString(format), FontSize = 8, Foreground = otherBrush };
                Canvas.SetLeft(lbl, axX + 6);
                Canvas.SetTop(lbl, y - 6);
                ChartCanvas.Children.Add(lbl);
            }
            var title = new TextBlock { Text = "LR/Other", FontSize = 9, FontWeight = FontWeight.Bold, Foreground = otherBrush };
            Canvas.SetLeft(title, axX - 25);
            Canvas.SetTop(title, mT - 14);
            ChartCanvas.Children.Add(title);
        }

        ChartCanvas.Children.Add(new Line { StartPoint = new Point(mL, h - mB), EndPoint = new Point(w - mR, h - mB), Stroke = axisBrush, StrokeThickness = 1 });

        var xStart = new TextBlock { Text = "1", FontSize = 9, Foreground = textBrush };
        Canvas.SetLeft(xStart, mL + 2);
        Canvas.SetTop(xStart, h - mB + 4);
        ChartCanvas.Children.Add(xStart);

        var xEnd = new TextBlock { Text = $"{epochs}", FontSize = 9, Foreground = textBrush };
        Canvas.SetLeft(xEnd, w - mR - 18);
        Canvas.SetTop(xEnd, h - mB + 4);
        ChartCanvas.Children.Add(xEnd);

        var xMiddle = new TextBlock { Text = "Epoch", FontSize = 9, Foreground = textBrush };
        Canvas.SetLeft(xMiddle, mL + cW / 2 - 15);
        Canvas.SetTop(xMiddle, h - mB + 4);
        ChartCanvas.Children.Add(xMiddle);
    }

    private void DrawLegend(List<(TrainRun Run, MetricSeries Series, MetricToggle Toggle, int ScaleType)> curves, double x, double y)
    {
        double yOff = y;
        var grouped = curves.GroupBy(c => c.Run).ToList();

        foreach (var runGroup in grouped)
        {
            var run = runGroup.Key;
            var runBrush = GetBrush(run.Color);

            var runHeader = new TextBlock
            {
                Text = run.Name,
                FontSize = 10,
                FontWeight = FontWeight.Bold,
                Foreground = runBrush
            };
            Canvas.SetLeft(runHeader, x);
            Canvas.SetTop(runHeader, yOff);
            ChartCanvas.Children.Add(runHeader);
            yOff += 14;

            foreach (var curve in runGroup)
            {
                double lastVal = curve.Series.Values.LastOrDefault();
                string metricName = !string.IsNullOrEmpty(curve.Toggle.DisplayName) ? curve.Toggle.DisplayName : curve.Series.Name.Split('/').Last();

                var lineSwatch = new Line
                {
                    StartPoint = new Point(x + 6, yOff + 6),
                    EndPoint = new Point(x + 24, yOff + 6),
                    Stroke = runBrush,
                    StrokeThickness = curve.Toggle.IsPrimary ? 2.0 : 1.0
                };

                if (curve.Toggle.Group == "Val Loss")
                {
                    lineSwatch.StrokeDashArray = new Avalonia.Collections.AvaloniaList<double>(new[] { 4.0, 2.0 });
                }
                else if (curve.Toggle.Group == "Train Loss")
                {
                    lineSwatch.StrokeDashArray = new Avalonia.Collections.AvaloniaList<double>(new[] { 1.0, 2.0 });
                }
                else if (curve.Toggle.Group == "LR" || curve.Toggle.Group == "Other")
                {
                    lineSwatch.StrokeDashArray = new Avalonia.Collections.AvaloniaList<double>(new[] { 5.0, 2.0, 1.0, 2.0 });
                }

                ChartCanvas.Children.Add(lineSwatch);

                var lbl = new TextBlock
                {
                    Text = $"{metricName}: {lastVal:F4}",
                    FontSize = 9,
                    Foreground = new SolidColorBrush(Color.Parse("#334155"))
                };
                Canvas.SetLeft(lbl, x + 28);
                Canvas.SetTop(lbl, yOff);
                ChartCanvas.Children.Add(lbl);

                yOff += 13;
                if (yOff > ChartCanvas.Bounds.Height - 50) return;
            }
            yOff += 4;
        }
    }

    private void DrawMessage(string message)
    {
        var tb = new TextBlock
        {
            Text = message,
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.Parse("#94A3B8")),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        Canvas.SetLeft(tb, 0);
        Canvas.SetTop(tb, ChartCanvas.Bounds.Height / 2 - 10);
        ChartCanvas.Children.Add(tb);
    }

    private IBrush GetBrush(string hex)
    {
        if (!_brushCache.TryGetValue(hex, out var brush))
        {
            try { brush = new SolidColorBrush(Color.Parse(hex)); }
            catch { brush = Brushes.Gray; }
            _brushCache[hex] = brush;
        }
        return brush;
    }

    private void OnImageDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (sender is Control control && control.DataContext is Services.ThumbnailItem item && _viewModel?.SelectedRun != null)
        {
            var viewer = new ImageViewerWindow();
            viewer.LoadImages(_viewModel.SelectedRun.Images.Select(i => i.Path).ToList(), item.Path);
            viewer.Show();
        }
    }
}
