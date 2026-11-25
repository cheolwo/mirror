using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace Ssalddel.BatchMonitor.Windows;

public sealed class BatchRouteMap : FrameworkElement
{
    private const double MapWidth = 960;
    private const double MapHeight = 590;
    private static readonly int[] Columns = [-100, -23, 48, 113, 184, 255, 323, 400, 480, 554, 629, 701, 771, 846, 930, 1010];
    private static readonly int[] Rows = [-70, -10, 50, 109, 174, 242, 305, 365, 426, 490, 553, 625, 695, 775];
    private static readonly Point[] Stops =
    [
        new(184, 242), new(323, 174), new(480, 109), new(629, 174),
        new(771, 242), new(629, 365), new(480, 426), new(323, 426),
        new(184, 490), new(113, 365), new(480, 305), new(771, 490)
    ];
    private static readonly Point Depot = new(400, 365);
    private static readonly Matrix StreetAngle = new(1, -.24, .27, 1, -21, 84);
    private static readonly Typeface Font = new("Malgun Gothic");
    private static readonly Brush Ground = MakeBrush("#F2F2EF");
    private static readonly Brush Block = MakeBrush("#E9E8E3");
    private static readonly Brush Building = MakeBrush("#DEDFDB");
    private static readonly Brush Park = MakeBrush("#DAE8D4");
    private static readonly Brush Ink = MakeBrush("#385864");
    private static readonly Brush OrderFill = MakeBrush("#C9F0E3");
    private static readonly Brush OrderInk = MakeBrush("#15584D");
    private static readonly Brush Transit = MakeBrush("#6844AE");
    private static readonly Brush Finished = MakeBrush("#248570");
    private static readonly Pen WhiteRoad = MakePen(Brushes.White, 10);
    private static readonly Pen StreetLine = MakePen(MakeBrush("#628DA8"), 1.35);
    private static readonly Pen JoinBorder = MakePen(Brushes.White, .7);
    private static readonly Pen WaitingLine = MakePen(MakeBrush("#437D9A"), 4, DashStyles.Dash);
    private static readonly Pen TransitLine = MakePen(Transit, 5);
    private static readonly Pen FinishedLine = MakePen(Finished, 4);
    private static readonly Pen PinBorder = MakePen(OrderInk, 1.3);
    private static readonly Pen VehicleBorder = MakePen(Brushes.White, 2);
    private static readonly DrawingGroup BaseMap = BuildBaseMap();

    private BatchSimulation? simulation;

    public void Update(BatchSimulation current)
    {
        simulation = current;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);
        context.DrawRectangle(Ground, null, new Rect(RenderSize));
        if (RenderSize.Width <= 0 || RenderSize.Height <= 0)
        {
            return;
        }

        var scale = Math.Min(RenderSize.Width / MapWidth, RenderSize.Height / MapHeight);
        context.PushTransform(new TranslateTransform((RenderSize.Width - MapWidth * scale) / 2, (RenderSize.Height - MapHeight * scale) / 2));
        context.PushTransform(new ScaleTransform(scale, scale));
        context.DrawDrawing(BaseMap);
        if (simulation is { } current)
        {
            foreach (var batch in current.Batches.Reverse())
            {
                DrawBatch(context, batch, current.Elapsed);
            }

            var depot = StreetAngle.Transform(Depot);
            context.DrawRoundedRectangle(Brushes.White, StreetLine, new Rect(depot.X - 10, depot.Y - 10, 20, 20), 3, 3);
            DrawLabel(context, "집결지", depot + new Vector(15, -9), 12, Ink);
            foreach (var order in current.Orders.Reverse())
            {
                DrawOrder(context, order);
            }
        }
        context.Pop();
        context.Pop();
    }

    private void DrawBatch(DrawingContext context, BatchMonitorBatch batch, TimeSpan elapsed)
    {
        var points = new List<Point> { StreetAngle.Transform(Depot) };
        var from = Depot;
        foreach (var number in batch.OrderNumbers)
        {
            var to = Location(number);
            if (from.X != to.X && from.Y != to.Y)
            {
                points.Add(StreetAngle.Transform(new Point(to.X, from.Y)));
            }
            points.Add(StreetAngle.Transform(to));
            from = to;
        }

        var pen = batch.Phase switch
        {
            BatchMonitorPhase.Completed => FinishedLine,
            BatchMonitorPhase.InTransit => TransitLine,
            _ => WaitingLine
        };
        for (var index = 1; index < points.Count; index++)
        {
            context.DrawLine(pen, points[index - 1], points[index]);
        }

        if (batch.Phase == BatchMonitorPhase.InTransit)
        {
            var travelAge = elapsed - batch.CreatedAt - BatchSimulation.TransitAge;
            var fraction = Math.Clamp(travelAge / (BatchSimulation.CompletionAge - BatchSimulation.TransitAge), 0, 1);
            var travelDistance = points.Zip(points.Skip(1), (start, end) => (end - start).Length).Sum() * fraction;
            for (var index = 1; index < points.Count; index++)
            {
                var segment = points[index] - points[index - 1];
                if (travelDistance <= segment.Length && segment.Length > 0)
                {
                    var position = points[index - 1] + segment * (travelDistance / segment.Length);
                    context.DrawEllipse(Transit, VehicleBorder, position, 7, 7);
                    break;
                }
                travelDistance -= segment.Length;
            }
        }
    }

    private void DrawOrder(DrawingContext context, BatchMonitorOrder order)
    {
        var anchor = StreetAngle.Transform(Location(order.Number));
        var center = anchor + new Vector(0, -18);
        context.DrawLine(PinBorder, anchor, center);
        context.DrawEllipse(order.Phase == BatchMonitorPhase.Waiting ? Brushes.White : OrderFill, PinBorder, center, 13, 13);
        var label = order.Number.ToString(CultureInfo.InvariantCulture);
        var text = MakeLabel(label, label.Length > 3 ? 8 : 11, OrderInk);
        context.DrawText(text, center - new Vector(text.Width / 2, text.Height / 2));
    }

    private static Point Location(long orderNumber) => Stops[(orderNumber - 1) % Stops.Length];

    private void DrawLabel(DrawingContext context, string text, Point origin, double size, Brush brush) =>
        context.DrawText(MakeLabel(text, size, brush), origin);

    private FormattedText MakeLabel(string label, double size, Brush brush) =>
        new(label, CultureInfo.GetCultureInfo("ko-KR"), FlowDirection.LeftToRight, Font, size, brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

    private static DrawingGroup BuildBaseMap()
    {
        var drawing = new DrawingGroup();
        using (var context = drawing.Open())
        {
            context.PushClip(new RectangleGeometry(new Rect(0, 0, MapWidth, MapHeight)));
            context.DrawRectangle(Ground, null, new Rect(0, 0, MapWidth, MapHeight));
            context.PushTransform(new MatrixTransform(StreetAngle));
            for (var row = 0; row < Rows.Length - 1; row++)
            {
                for (var column = 0; column < Columns.Length - 1; column++)
                {
                    var x = Columns[column];
                    var y = Rows[row];
                    var width = Columns[column + 1] - x;
                    var height = Rows[row + 1] - y;
                    var green = (column * 7 + row * 13) % 23 == 0;
                    context.DrawRectangle(green ? Park : Block, null, new Rect(x + 6, y + 6, width - 12, height - 12));
                    if (!green)
                    {
                        context.DrawRectangle(Building, null, new Rect(x + 11, y + 11, width * .36, height - 23));
                        context.DrawRectangle(Building, null, new Rect(x + width * .59, y + 14, width * .23, height * .30));
                        context.DrawRectangle(Building, null, new Rect(x + width * .58, y + height * .62, width * .29, height * .19));
                    }
                }
            }

            foreach (var pen in new[] { WhiteRoad, StreetLine })
            {
                foreach (var x in Columns)
                {
                    context.DrawLine(pen, new Point(x, Rows[0]), new Point(x, Rows[^1]));
                }
                foreach (var y in Rows)
                {
                    context.DrawLine(pen, new Point(Columns[0], y), new Point(Columns[^1], y));
                }
            }
            foreach (var y in Rows)
            {
                foreach (var x in Columns)
                {
                    context.DrawEllipse(StreetLine.Brush, JoinBorder, new Point(x, y), 2, 2);
                }
            }
            context.Pop();
            context.Pop();
        }
        drawing.Freeze();
        return drawing;
    }

    private static SolidColorBrush MakeBrush(string color)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromInvariantString(color)!;
        brush.Freeze();
        return brush;
    }

    private static Pen MakePen(Brush brush, double thickness, DashStyle? dash = null)
    {
        var pen = new Pen(brush, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        if (dash is not null)
        {
            pen.DashStyle = dash;
        }
        pen.Freeze();
        return pen;
    }
}
