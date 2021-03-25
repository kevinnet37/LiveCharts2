﻿// The MIT License(MIT)

// Copyright(c) 2021 Alberto Rodriguez Orozco & LiveCharts Contributors

// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using LiveChartsCore.Kernel;
using LiveChartsCore.Drawing;
using System;
using LiveChartsCore.Measure;

namespace LiveChartsCore
{
    public class StackedColumnSeries<TModel, TVisual, TLabel, TDrawingContext> : StackedBarSeries<TModel, TVisual, TLabel, TDrawingContext>
        where TVisual : class, ISizedVisualChartPoint<TDrawingContext>, new()
        where TLabel : class, ILabelGeometry<TDrawingContext>, new()
        where TDrawingContext : DrawingContext
    {
        public StackedColumnSeries()
            : base(SeriesProperties.Bar | SeriesProperties.VerticalOrientation | SeriesProperties.Stacked)
        {

        }

        public override void Measure(
           CartesianChart<TDrawingContext> chart, IAxis<TDrawingContext> secondaryAxis, IAxis<TDrawingContext> primaryAxis)
        {
            var drawLocation = chart.DrawMaringLocation;
            var drawMarginSize = chart.DrawMarginSize;
            var secondaryScale = new Scaler(
                drawLocation, drawMarginSize, secondaryAxis.Orientation, secondaryAxis.DataBounds, secondaryAxis.IsInverted);
            var primaryScale = new Scaler(
                drawLocation, drawMarginSize, primaryAxis.Orientation, primaryAxis.DataBounds, primaryAxis.IsInverted);

            float uw = secondaryScale.ToPixels(1f) - secondaryScale.ToPixels(0f);
            float uwm = 0.5f * uw;
            float sw = Stroke?.StrokeThickness ?? 0;
            float p = primaryScale.ToPixels(pivot);

            var pos = chart.SeriesContext.GetStackedColumnPostion(this);
            var count = chart.SeriesContext.GetStackedColumnSeriesCount();
            float cp = 0f;

            if (count > 1)
            {
                uw = uw / count;
                uwm = 0.5f * uw;
                cp = (pos - count / 2f) * uw + uwm;
            }

            if (uw > MaxBarWidth)
            {
                uw = unchecked((float)MaxBarWidth);
                uwm = uw / 2f;
            }

            var actualZIndex = ZIndex == 0 ? ((ISeries)this).SeriesId : ZIndex;
            if (Fill != null)
            {
                Fill.ZIndex = actualZIndex + 0.1;
                chart.Canvas.AddDrawableTask(Fill); 
            }
            if (Stroke != null)
            {
                Stroke.ZIndex = actualZIndex + 0.2;
                chart.Canvas.AddDrawableTask(Stroke);
            }
            if (DataLabelsDrawableTask != null)
            {
                DataLabelsDrawableTask.ZIndex = actualZIndex + 0.3;
                chart.Canvas.AddDrawableTask(DataLabelsDrawableTask);
            }
            var dls = unchecked((float)DataLabelsSize);

            var stacker = chart.SeriesContext.GetStackPosition(this, GetStackGroup());
            if (stacker == null) throw new NullReferenceException("Unexpected null stacker");

            foreach (var point in Fetch(chart))
            {
                var visual = point.Context.Visual as TVisual;
                var secondary = secondaryScale.ToPixels(point.SecondaryValue);

                if (point.IsNull)
                {
                    if (visual != null)
                    {
                        visual.X = secondary - uwm + cp;
                        visual.Y = p;
                        visual.Width = uw;
                        visual.Height = 0;
                        visual.RemoveOnCompleted = true;
                        point.Context.Visual = null;
                    }
                    continue;
                }

                if (visual == null)
                {
                    var r = new TVisual
                    {
                        X = secondary - uwm + cp,
                        Y = p,
                        Width = uw,
                        Height = 0
                    };

                    visual = r;
                    point.Context.Visual = visual;
                    OnPointCreated(point);
                    r.CompleteAllTransitions();

                    if (Fill != null) Fill.AddGeometyToPaintTask(r);
                    if (Stroke != null) Stroke.AddGeometyToPaintTask(r);
                }

                var sizedGeometry = visual;

                var sy = stacker.GetStack(point);
                var primaryI = primaryScale.ToPixels(sy.Start);
                var primaryJ = primaryScale.ToPixels(sy.End);
                var x = secondary - uwm + cp;

                sizedGeometry.X = x;
                sizedGeometry.Y = primaryJ;
                sizedGeometry.Width = uw;
                sizedGeometry.Height = primaryI - primaryJ;
                sizedGeometry.RemoveOnCompleted = false;

                point.Context.HoverArea = new RectangleHoverArea().SetDimensions(secondary - uwm + cp, primaryJ, uw, primaryI - primaryJ);

                OnPointMeasured(point);
                chart.MeasuredDrawables.Add(sizedGeometry);

                if (DataLabelsDrawableTask != null)
                {
                    var label = point.Context.Label as TLabel;

                    if (label == null)
                    {
                        var l = new TLabel { X = secondary - uwm + cp, Y = p };

                        l.TransitionateProperties(nameof(l.X), nameof(l.Y))
                            .WithAnimation(a =>
                                a.WithDuration(chart.AnimationsSpeed)
                                .WithEasingFunction(chart.EasingFunction));

                        l.CompleteAllTransitions();
                        label = l;
                        point.Context.Label = label;
                        DataLabelsDrawableTask.AddGeometyToPaintTask(l);
                    }

                    label.Text = DataLabelFormatter(point);
                    label.TextSize = dls;
                    label.Padding = DataLabelsPadding;
                    var labelPosition = GetLabelPosition(
                        x, primaryJ, uw, primaryI - primaryJ, label.Measure(DataLabelsDrawableTask), DataLabelsPosition,
                        SeriesProperties, point.PrimaryValue > Pivot);
                    label.X = labelPosition.X;
                    label.Y = labelPosition.Y;

                    chart.MeasuredDrawables.Add(label);
                }
            }
        }

        public override DimensinalBounds GetBounds(
         CartesianChart<TDrawingContext> chart, IAxis<TDrawingContext> secondaryAxis, IAxis<TDrawingContext> primaryAxis)
        {
            var baseBounds = base.GetBounds(chart, secondaryAxis, primaryAxis);

            var tick = primaryAxis.GetTick(chart.ControlSize, baseBounds.PrimaryBounds);

            return new DimensinalBounds
            {
                SecondaryBounds = new Bounds
                {
                    Max = baseBounds.SecondaryBounds.Max + 0.5,
                    Min = baseBounds.SecondaryBounds.Min - 0.5
                },
                PrimaryBounds = new Bounds
                {
                    Max = baseBounds.PrimaryBounds.Max + tick.Value,
                    Min = baseBounds.PrimaryBounds.Min < 0 ? baseBounds.PrimaryBounds.Min - tick.Value : 0
                }
            };
        }

        protected override void SetDefaultPointTransitions(ChartPoint chartPoint)
        {
            var visual = chartPoint.Context.Visual as TVisual;
            var chart = chartPoint.Context.Chart;

            if (visual == null) throw new Exception("Unable to initialize the point instance.");

            visual
                .TransitionateProperties(
                    nameof(visual.X),
                    nameof(visual.Width))
                .WithAnimation(a => a.WithDuration(chart.AnimationsSpeed).WithEasingFunction(chart.EasingFunction));

            visual
                .TransitionateProperties(nameof(visual.Y), nameof(visual.Height))
                .WithAnimation(a =>
                    a.WithDuration((long)(chart.AnimationsSpeed.TotalMilliseconds * 1.5)).WithEasingFunction(elasticFunction));
        }
    }
}