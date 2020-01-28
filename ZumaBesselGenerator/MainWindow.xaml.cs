using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using MessageBox = System.Windows.MessageBox;

namespace ZumaBesselPath
{
    class Point
    {
        public Point(double x, double y)
        {
            this.x = x;
            this.y = y;
            order = 00;
            isTunnel = false;
        }

        public Ellipse ellipse;

        public int order;
        public bool isTunnel;
        public double x, y;

        public static Point operator *(double num, Point point)
        {
            return new Point(point.x * num, point.y * num);
        }

        public static Point operator *(Point point, double num)
        {
            return num * point;
        }

        public static Point operator +(Point point1, Point point2)
        {
            return new Point(point1.x + point2.x, point1.y + point2.y);
        }

        public static Point operator -(Point point1, Point point2)
        {
            return new Point(point1.x - point2.x, point1.y - point2.y);
        }

        public double Distane(Point pos)
        {
            return Math.Sqrt(Math.Pow(x - pos.x, 2) + Math.Pow(y - pos.y, 2));
        }

        public override string ToString()
        {
            return $"({x}, {y})";
        }
    }
    interface IPath
    {
        Point GetPos(double t);
    }
    class MoveToPath : IPath
    {
        public Point pointTo;

        public Point GetPos(double t)
        {
            return new Point(pointTo.x, pointTo.y);
        }
    }
    class LinePath : IPath
    {
        public Point pointFrom;
        public Point pointTo;

        public Point GetPos(double t)
        {
            return (pointTo - pointFrom) * t + pointFrom;
        }
    }

    class BesselPath : IPath
    {
        public Point point0;
        public Point point1;
        public Point point2;
        public Point point3;

        public Point GetPos(double t)
        {
            if (t > 1)
                return point1;
            if (t < 0)
                return point0;

            return
                point0 * Math.Pow(1 - t, 3) +
                3 * point1 * t * Math.Pow(1 - t, 2) +
                3 * point2 * Math.Pow(t, 2) * (1 - t) +
                point3 * Math.Pow(t, 3);
        }

        public BesselPath(Point p0, Point p1, Point p2, Point p3)
        {
            point0 = p0;
            point1 = p1;
            point2 = p2;
            point3 = p3;
        }
        public override string ToString()
        {
            return $"[{point0},{point1},{point2},{point3}]";
        }
    }

    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<Point> pointList;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void LoadAI(string fileName)
        {
            var aiReader = File.OpenText(fileName);

            // 读取AI文件
            var isReadingCurve = false;
            var curveData = "";
            while (!aiReader.EndOfStream)
            {
                var line = aiReader.ReadLine();

                if (isReadingCurve)
                {
                    if (line == "N")
                        break;
                    curveData += (curveData == "" ? "" : "\r") + line;
                    continue;
                }
                if (line.StartsWith("%%BoundingBox:"))
                {
                    var args = line.Split(' ');
                    if (args.Length != 5)
                        continue;
                    WidthTextBox.Text = args[3];
                    HeightTextBox.Text = args[4];
                }
                if (line.StartsWith("1 XR"))
                    isReadingCurve = true;
            }

            BesselTextBox.Text = curveData;

            // 开始读取曲线
            WarningLabel.Content = "";

            if (!int.TryParse(WidthTextBox.Text, out var width))
            {
                WarningLabel.Content = "[错误]宽度非法";
                return;
            }

            if (!int.TryParse(HeightTextBox.Text, out var height))
            {
                WarningLabel.Content = "[错误]高度非法";
                return;
            }
            var reader = new StringReader(BesselTextBox.Text);
            var paths = new List<IPath>();

            while (true)
            {
                var line = reader.ReadLine();
                if (string.IsNullOrEmpty(line))
                    break;

                var args = line.Split(' ');
                switch (args[args.Length - 1])
                {
                    case "M":
                    case "m":
                        if (args.Length != 3)
                        {
                            WarningLabel.Content = "[错误]曲线信息出现错误";
                            return;
                        }
                        paths.Add(
                            new MoveToPath
                            {
                                pointTo = new Point(double.Parse(args[0]), double.Parse(args[1]))
                            });
                        break;
                    case "V":
                    case "v":
                        if (args.Length != 5)
                        {
                            WarningLabel.Content = "[错误]曲线信息出现错误";
                            return;
                        }
                        paths.Add(new BesselPath(
                            paths[paths.Count - 1].GetPos(1),
                            paths[paths.Count - 1].GetPos(1),
                            new Point(double.Parse(args[0]), double.Parse(args[1])),
                            new Point(double.Parse(args[2]), double.Parse(args[3])))
                            );
                        break;
                    case "Y":
                    case "y":
                        if (args.Length != 5)
                        {
                            WarningLabel.Content = "[错误]曲线信息出现错误";
                            return;
                        }
                        paths.Add(new BesselPath(
                            paths[paths.Count - 1].GetPos(1),
                            new Point(double.Parse(args[0]), double.Parse(args[1])),
                            new Point(double.Parse(args[2]), double.Parse(args[3])),
                            new Point(double.Parse(args[2]), double.Parse(args[3])))
                            );
                        break;
                    case "L":
                    case "l":
                        if (args.Length != 3)
                        {
                            WarningLabel.Content = "[错误]曲线信息出现错误";
                            return;
                        }
                        paths.Add(new LinePath
                        {
                            pointFrom = paths[paths.Count - 1].GetPos(1),
                            pointTo = new Point(double.Parse(args[0]), double.Parse(args[1]))
                        });
                        break;
                    case "C":
                    case "c":
                        if (args.Length != 7)
                        {
                            WarningLabel.Content = "[错误]曲线信息出现错误";
                            return;
                        }
                        paths.Add(new BesselPath(
                            paths[paths.Count - 1].GetPos(1),
                            new Point(double.Parse(args[0]), double.Parse(args[1])),
                            new Point(double.Parse(args[2]), double.Parse(args[3])),
                            new Point(double.Parse(args[4]), double.Parse(args[5])))
                            );
                        break;
                    default:
                        WarningLabel.Content = "[错误]未知曲线信息" + args[args.Length - 1];
                        return;
                }
            }

            reader.Close();

            double precision = double.Parse(PrecisionTextBox.Text);

            if (precision < 0.00005)
                MessageBox.Show("开始生成，程序可能会出现卡顿，请耐心等待");

            if (precision < 0.000001)
                precision = 0.000001;

            pointList = new List<Point>();

            // 添加起始点
            var startPoint = paths[0].GetPos(0);
            startPoint.x *= 640.0 / width;
            startPoint.y *= 480.0 / height;
            pointList.Add(startPoint);

            // 根据路径创建点
            foreach (var path in paths)
            {
                for (var t = 0.0; t <= 1.0; t += precision)
                {
                    var currentPoint = path.GetPos(t);
                    currentPoint.x *= 480.0 / height;
                    currentPoint.y *= 640.0 / width;

                    var lastPoint = pointList[pointList.Count - 1];
                    var deltaLength = currentPoint.Distane(lastPoint);
                    if (deltaLength < 1)
                        continue;
                    if (deltaLength - 1 > 0.1f)
                        WarningLabel.Content = "[警告]缺失部分中间点，可能无法导出，请修改精度";

                    deltaLength = 0;
                    pointList.Add(new Point(currentPoint.x, currentPoint.y));
                }
            }

            // 最终处理
            foreach (var point in pointList)
                point.y = 480 - point.y;
        }

        private void LoadTxtPath(string fileName)
        {
            pointList = new List<Point>();

            var reader = File.OpenText(fileName);

            double currentX = 0;
            double currentY = 0;

            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();

                if (line.StartsWith("#"))
                    continue;

                var args = line.Split(' ');
                if (args.Length != 4)
                    continue;

                if (!double.TryParse(args[0], out var x) ||
                    !double.TryParse(args[1], out var y) ||
                    !int.TryParse(args[2], out var isTunnel) ||
                    !int.TryParse(args[3], out var order))
                {
                    WarningLabel.Content = "[警告]无法加载文件，文件内有错误";
                    return;
                }

                Point point = new Point(x, y)
                {
                    isTunnel = isTunnel > 0,
                    order = order
                };

                if (pointList.Count != 0)
                {
                    point.x /= 100;
                    point.y /= 100;
                    point.x += currentX;
                    point.y += currentY;
                }
                currentX = point.x;
                currentY = point.y;

                pointList.Add(point);
            }
        }

        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Filter = "AI曲线文件|*.ai|文本格式轨道|*.txt";

            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            var fileInfo = new FileInfo(dialog.FileName);
            var extension = fileInfo.Extension.ToLower();
            if (extension == ".ai")
                LoadAI(dialog.FileName);
            else if (extension == ".txt")
                LoadTxtPath(dialog.FileName);
            else
                return;

            RedrawPath();
            MessageBox.Show("总点数： " + pointList.Count());
        }

        private bool isDirty = false;

        private SolidColorBrush GetPointBrush(Point point)
        {
            Color color;

            switch (point.order)
            {
                case 0:
                    if (point.isTunnel)
                        color = Color.FromArgb(
                                255,
                                255,
                                230,
                                230);
                    else
                        color = Color.FromArgb(
                                255,
                                230,
                                255,
                                230);
                    break;
                case 1:
                    if (point.isTunnel)
                        color = Color.FromArgb(
                                255,
                                255,
                                170,
                                170);
                    else
                        color = Color.FromArgb(
                                255,
                                170,
                                255,
                                170);
                    break;
                case 2:
                    if (point.isTunnel)
                        color = Color.FromArgb(
                                255,
                                255,
                                0,
                                0);
                    else
                        color = Color.FromArgb(
                                255,
                                0,
                                255,
                                0);
                    break;
                case 3:
                    if (point.isTunnel)
                        color = Color.FromArgb(
                                255,
                                170,
                                100,
                                100);
                    else
                        color = Color.FromArgb(
                                255,
                                100,
                                170,
                                100);
                    break;
                case 4:
                    if (point.isTunnel)
                        color = Color.FromArgb(
                                255,
                                100,
                                64,
                                64);
                    else
                        color = Color.FromArgb(
                                255,
                                64,
                                100,
                                64);
                    break;
                default:
                    color = Colors.Black;
                    break;
            }
            return new SolidColorBrush(color);
        }

        private void RedrawPath()
        {
            isDirty = false;

            PreviewCanvas.Children.Clear();
            foreach (var point in pointList)
            {
                var ellipse = new Ellipse();
                ellipse.Fill = GetPointBrush(point);

                ellipse.Width = 2;
                ellipse.Height = 2;
                ellipse.Margin = new Thickness(point.x, point.y, 0, 0);
                PreviewCanvas.Children.Add(ellipse);
                point.ellipse = ellipse;
            }
        }

        private void RefreshPath()
        {
            isDirty = false;

            foreach (var point in pointList)
            {
                if (point.ellipse == null)
                    continue;

                point.ellipse.Fill = GetPointBrush(point);
            }
        }

        private void PrecisionTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!double.TryParse(PrecisionTextBox.Text, out _))
                e.Handled = false;
        }

        private void OutputButton_Click(object sender, RoutedEventArgs e)
        {
            if (pointList == null)
            {
                MessageBox.Show("请先加载");
                return;
            }
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "轨道文件|*.dat";

            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            var reader = File.CreateText(dialog.FileName + ".txt");

            reader.WriteLine("#Zuma Bessel Generator");
            reader.WriteLine($"#By GSciencce Studio");
            reader.WriteLine($"#{DateTime.Now.ToString()}");

            reader.WriteLine($"{pointList[0].x} {pointList[0].y} 0 0");

            for (int i = 1; i < pointList.Count; ++i)
            {
                var lastPoint = pointList[i - 1];
                var currentPoint = pointList[i];

                var offset = currentPoint - lastPoint;

                offset *= 100;

                if (offset.x > 255 || offset.x < -256 ||
                    offset.y > 255 || offset.y < -256)
                {
                    MessageBox.Show("生成失败，无法计算偏移量");
                    return;
                }
                int p1 = currentPoint.isTunnel ? 1 : 0;
                int p2 = currentPoint.order;
                reader.WriteLine($"{(int) offset.x} {(int) offset.y} {p1} {p2}");
            }
            reader.Close();

            var levelGeneratorPath = Environment.CurrentDirectory + "\\ZumaLevelBuilder.exe";
            if (!File.Exists(levelGeneratorPath))
            {
                MessageBox.Show("未找到ZumaLevelBuilder.exe，请手动生成");
                return;
            }

            var process = Process.Start(levelGeneratorPath, $"\"{dialog.FileName + ".txt"}\" \"{dialog.FileName}\"");
            if (!process.WaitForExit(10000) || process.ExitCode != 0)
            {
                MessageBox.Show("出现异常，生成失败");
                process.Kill();
            }
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("由GScience Studio瞎做");
            MessageBox.Show("首先在PS里使用钢笔工具绘制路径", "第一步");
            MessageBox.Show("点击 文件->导出->路径到illustrator", "第二步");
            MessageBox.Show("点击加载，然后选择导出的ai文件。\r若显示精度问题，则需要把精度调的更小，一般不会出现问题", "第三步");
            MessageBox.Show("点击导出来生成祖玛关卡文件", "第四步");
        }

        private void BrushSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (BrushEllipse == null)
                return;

            BrushEllipse.Width = BrushSlider.Value;
            BrushEllipse.Height = BrushSlider.Value;
        }

        private int lastSetPropertyPointIndex = -1;

        private void Windows_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (BrushEllipse == null)
                return;

            if (pointList == null || pointList.Count == 0)
                return;

            var pos = e.GetPosition(PreviewCanvas);

            if (pos.X < 0 || pos.Y < 0 ||
                pos.X > PreviewCanvas.Width || pos.Y > PreviewCanvas.Height)
                return;

            if (!int.TryParse(OrderTextBox.Text, out var order))
            {
                WarningLabel.Content = "[错误]顺序必须为数字";
                return;
            }

            var mousePos = new Point(pos.X, pos.Y);

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                for (var i = 0; i < pointList.Count; ++i)
                {
                    if (lastSetPropertyPointIndex != -1 &&
                        Math.Abs(lastSetPropertyPointIndex - i) > BrushSlider.Value &&
                        ContinueCheckBox.IsChecked.Value)
                        continue;

                    var point = pointList[i];

                    if (point.Distane(mousePos) > BrushSlider.Value / 2)
                        continue;

                    point.isTunnel = TunnelCheckBox.IsChecked.Value;
                    point.order = order;
                    isDirty = true;

                    lastSetPropertyPointIndex = i;
                }
            }
            else
                lastSetPropertyPointIndex = -1;

            if (isDirty)
                RefreshPath();

            BrushEllipse.Margin = new Thickness(
                e.GetPosition(this).X - BrushEllipse.Width / 2, 
                e.GetPosition(this).Y - BrushEllipse.Height / 2, 
                0, 0);
        }

        private void LoadImageButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Filter = "图像文件|*.png;*.jpg;*.gif";

            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            PreviewCanvas.Background = new ImageBrush(new BitmapImage(new Uri(dialog.FileName)));
        }

        private void WarningLabel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            WarningLabel.Content = "";
        }
    }
}
