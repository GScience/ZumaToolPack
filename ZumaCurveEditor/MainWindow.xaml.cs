using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Z.Expressions;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace ZumaCurveEditor
{
    class Point
    {
        public Point(double x, double y)
        {
            this.x = x;
            this.y = y;
            isTunnel = false;
            order = 00;
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
        ResourceDictionary lang;

        private List<List<Point>> MultiPointList=new List<List<Point>>();
        private bool zdr;

        public MainWindow()
        {
            InitializeComponent();
        }
        private void language(object sender, SelectionChangedEventArgs e)
        {
            List<ResourceDictionary> dictionaryList = new List<ResourceDictionary>();
            foreach (ResourceDictionary dictionary in Application.Current.Resources.MergedDictionaries)
            {
                dictionaryList.Add(dictionary);
            }

            string requestedCulture;

            if (yuyan.SelectedIndex == 0)
                requestedCulture = @"Resources\Lang\zh-cn.xaml";
            else
                requestedCulture = @"Resources\Lang\en-us.xaml";
            ResourceDictionary resourceDictionary
                = dictionaryList.FirstOrDefault(d => d.Source.OriginalString.Equals(requestedCulture));

            Application.Current.Resources.MergedDictionaries.Remove(resourceDictionary);
            Application.Current.Resources.MergedDictionaries.Add(resourceDictionary);

            lang = resourceDictionary;
        }

        private void CanvasResolution(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(CanvasWidth.Text, out var width) || width < 0)
            {
                WarningLabel.Content = $"{lang["ExceptionError"]}{lang["ExceptionWrongWidth"]}";
                return;
            }
            if (!double.TryParse(CanvasHeight.Text, out var height) || height < 0)
            {
                WarningLabel.Content = $"{lang["ExceptionError"]}{lang["ExceptionWrongHeight"]}";
                return;
            }
            PreviewCanvas.Width = width;
            PreviewCanvas.Height = height;
        }
        private bool LoadAI(string fileName)
        {
            var aiReader = File.OpenText(fileName);

            // 读取AI文件
            var isReadingCurve = false;
            List<string> curveData = new List<string>();
            while (!aiReader.EndOfStream)
            {
                var line = aiReader.ReadLine();

                if (isReadingCurve)
                {
                    if (line == "N")
                    {
                        isReadingCurve = false;
                        continue;
                    }
                    curveData[curveData.Count-1] += (curveData[curveData.Count-1] == "" ? "" : "\r") + line;
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
                {
                    isReadingCurve = true;
                    curveData.Add("");
                }
            }

            // 开始读取曲线
            WarningLabel.Content = "";
            for (int i = 0; i < curveData.Count; i++)
            {
                var reader = new StringReader(curveData[i]);
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
                                WarningLabel.Content = $"{lang["ExceptionError"]}{lang["ExceptionWrongCurve"]}";
                                return false;
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
                                WarningLabel.Content = $"{lang["ExceptionError"]}{lang["ExceptionWrongCurve"]}";
                                return false;
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
                                WarningLabel.Content = $"{lang["ExceptionError"]}{lang["ExceptionWrongCurve"]}";
                                return false;
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
                                WarningLabel.Content = $"{lang["ExceptionError"]}{lang["ExceptionWrongCurve"]}";
                                return false;
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
                                WarningLabel.Content = $"{lang["ExceptionError"]}{lang["ExceptionWrongCurve"]}";
                                return false;
                            }
                            paths.Add(new BesselPath(
                                paths[paths.Count - 1].GetPos(1),
                                new Point(double.Parse(args[0]), double.Parse(args[1])),
                                new Point(double.Parse(args[2]), double.Parse(args[3])),
                                new Point(double.Parse(args[4]), double.Parse(args[5])))
                                );
                            break;
                        default:
                            WarningLabel.Content = $"{lang["ExceptionError"]}{lang["ExceptionWrongUnknownCurve"]}{args[args.Length - 1]}";
                            return false;
                    }
                }

                reader.Close();

                if (!double.TryParse(PrecisionTextBox.Text, out var precision))
                {
                    WarningLabel.Content = $"{lang["ExceptionError"]}{lang["ExceptionWrongEnter"]}";
                    return false;
                }

                if (precision < 0.00005)
                    MessageBox.Show(lang["GenerateStarted"].ToString());

                if (precision < 0.000001)
                    precision = 0.000001;

                MultiPointList.Add(new List<Point>());
                int now = MultiPointList.Count - 1;

                // 添加起始点
                double width = PreviewCanvas.Width, height = PreviewCanvas.Height;
                int aiWidth = int.Parse(WidthTextBox.Text), aiHeight = int.Parse(HeightTextBox.Text);//存储到变量里，大幅提高程序执行效率
                var startPoint = paths[0].GetPos(0);
                startPoint.x *= width / aiWidth;
                startPoint.y *= height / aiHeight;
                MultiPointList[now].Add(startPoint);

                // 根据路径创建点
                foreach (var path in paths)
                {
                    for (var t = 0.0; t <= 1.0; t += precision)
                    {
                        var currentPoint = path.GetPos(t);
                        currentPoint.x *= height / aiHeight;
                        currentPoint.y *= width / aiWidth;

                        var lastPoint = MultiPointList[now][MultiPointList[now].Count - 1];
                        var deltaLength = currentPoint.Distane(lastPoint);
                        if (deltaLength < 1)
                            continue;
                        if (deltaLength - 1 > 0.1f)
                            WarningLabel.Content = $"{lang["ExceptionWarn"]}{lang["ExceptionWrongLowPrecision"]}";

                        deltaLength = 0;
                        MultiPointList[now].Add(new Point(currentPoint.x, currentPoint.y));
                    }
                }

                // 最终处理
                foreach (var point in MultiPointList[now])
                    point.y = height - point.y;
            }
            return true;
        }

        private bool LoadTxtPath(string fileName)
        {
            MultiPointList.Add(new List<Point>());
            int now = MultiPointList.Count - 1;

            var reader = File.OpenText(fileName);

            double currentX = 0;
            double currentY = 0;

            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();

                if (line.StartsWith("#"))
                    continue;

                if (line == "zr")
                {
                    start.Text = reader.ReadLine().Substring(1);
                    repeat.Text = reader.ReadLine().Substring(1);
                    single.Text = reader.ReadLine().Substring(1);
                    color.Text = reader.ReadLine().Substring(1);
                    speed.Text = reader.ReadLine().Substring(1);
                    danger.Text = reader.ReadLine().Substring(1);
                    score.Text = reader.ReadLine().Substring(1);
                    skullrot.Text = reader.ReadLine().Substring(1);
                    zumaback.Text = reader.ReadLine().Substring(1);
                    zumaslow.Text = reader.ReadLine().Substring(1);
                    dangerratio.Text = reader.ReadLine().Substring(1);
                    maxclumps.Text = reader.ReadLine().Substring(1);
                    destroyall.IsChecked = reader.ReadLine().Substring(1) == "True" ? true : false;
                    hide.IsChecked = reader.ReadLine().Substring(1) == "True" ? true : false;
                    invincible.IsChecked = reader.ReadLine().Substring(1) == "True" ? true : false;
                    continue;
                }
                else if (line == "zd")
                    continue;

                if(line== "=======")
                {
                    MultiPointList.Add(new List<Point>());
                    now ++;
                    warp.IsChecked = true;
                }

                var args = line.Split(' ');
                if (args.Length != 4)
                    continue;

                WarningLabel.Content = "";
                if (!double.TryParse(args[0], out var x) ||
                    !double.TryParse(args[1], out var y) ||
                    !int.TryParse(args[2], out var isTunnel) ||
                    !int.TryParse(args[3], out var order))
                {
                    WarningLabel.Content = $"{lang["ExceptionWarn"]}{lang["ExceptionWrongFileInternalError"]}";
                    return false;
                }

                Point point = new Point(x, y)
                {
                    isTunnel = isTunnel > 0,
                    order = order
                };

                if (MultiPointList[now].Count != 0)
                {
                    point.x /= 100;
                    point.y /= 100;
                    point.x += currentX;
                    point.y += currentY;
                }
                currentX = point.x;
                currentY = point.y;

                MultiPointList[now].Add(point);
            }
            return true;
        }

        private bool LoadDatPath(string fileName)
        {
            WarningLabel.Content = "";
            MultiPointList.Add(new List<Point>());
            int now = MultiPointList.Count - 1;

            var reader = File.OpenRead(fileName);
            BinaryReader br = new BinaryReader(reader);
            bool iszr;

            var head = br.ReadBytes(12);
            if (head[0] == 0x43 && head[1] == 0x55 && head[2] == 0x52 && head[3] == 0x56 && head[4] == 0x02 && head[5] == 0x00 && head[6] == 0x00 && head[7] == 0x00 && head[8] == 0x01 && head[9] == 0x00 && head[10] == 0x00 && head[11] == 0x00)
            {
                iszr = false;
            }
            else
            {
                br.BaseStream.Seek(0, SeekOrigin.Begin);
                head = br.ReadBytes(9);
                if (head[0] == 0x43 && head[1] == 0x55 && head[2] == 0x52 && head[3] == 0x56 && head[4] == 0x0F && head[5] == 0x00 && head[6] == 0x00 && head[7] == 0x00 && (head[8] == 0x00|| head[8] == 0x01))
                {
                    iszr = true; 

                    start.Text = br.ReadInt32().ToString();
                    br.BaseStream.Seek(4, SeekOrigin.Current);
                    repeat.Text = br.ReadInt32().ToString();
                    single.Text = br.ReadInt32().ToString();
                    color.Text = br.ReadInt32().ToString();
                    speed.Text = br.ReadSingle().ToString();
                    danger.Text = br.ReadInt32().ToString();
                    br.BaseStream.Seek(8, SeekOrigin.Current);
                    score.Text = br.ReadInt32().ToString();
                    skullrot.Text = br.ReadInt32().ToString();
                    zumaback.Text = br.ReadInt32().ToString();
                    zumaslow.Text = br.ReadInt32().ToString();
                    dangerratio.Text = br.ReadSingle().ToString();
                    maxclumps.Text = br.ReadInt32().ToString();
                    br.BaseStream.Seek(br.ReadInt32() * 8 + 6, SeekOrigin.Current);
                    destroyall.IsChecked = !br.ReadBoolean();
                    hide.IsChecked = !br.ReadBoolean();
                    invincible.IsChecked = !br.ReadBoolean();
                    br.BaseStream.Seek(6, SeekOrigin.Current);
                }
                else
                {
                    WarningLabel.Content = $"{lang["ExceptionWarn"]}{lang["ExceptionWrongHead"]}";
                    return false;
                }
            }

            br.BaseStream.Seek(br.ReadInt32() + 4, SeekOrigin.Current);//+4跳过轨道长度
            if (iszr) br.BaseStream.Seek(2, SeekOrigin.Current);


            double currentX = 0;
            double currentY = 0;

            while (br.BaseStream.Position < br.BaseStream.Length)
            {
                double x;
                double y;
                byte isTunnel;
                byte order;

                var fourbytes = br.ReadBytes(4);
                if (fourbytes.Length == 2) break;
                if (MultiPointList[now].Count == 0)
                {
                    br.BaseStream.Seek(-4, SeekOrigin.Current);
                    x = br.ReadSingle();
                    y = br.ReadSingle();
                    isTunnel = br.ReadByte();
                    order = br.ReadByte();
                }
                else if (iszr == true && fourbytes[2] > 0x01)
                {
                    MultiPointList.Add(new List<Point>());
                    now++;
                    warp.IsChecked = true;
                    x = br.ReadSingle();
                    y = br.ReadSingle();
                    isTunnel = br.ReadByte();
                    order = br.ReadByte();
                }
                else
                {
                    x = fourbytes[0] <= 127 ? fourbytes[0] : fourbytes[0] - 256;//8位无符号转有符号
                    y = fourbytes[1] <= 127 ? fourbytes[1] : fourbytes[1] - 256;
                    isTunnel = fourbytes[2];
                    order = fourbytes[3];
                }

                Point point = new Point(x, y)
                {
                    isTunnel = isTunnel > 0,
                    order = order
                };

                if (MultiPointList[now].Count != 0)
                {
                    point.x /= 100;
                    point.y /= 100;
                    point.x += currentX;
                    point.y += currentY;
                }
                currentX = point.x;
                currentY = point.y;

                MultiPointList[now].Add(point);
            }
            br.Close();
            return true;
        }

        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Filter = $"{lang["AIFile"]}|*.ai|{lang["RailFile"]}|*.dat|{lang["TextFile"]}|*.txt|{lang["AllFile"]}|*.*";

            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            var fileInfo = new FileInfo(dialog.FileName);
            var extension = fileInfo.Extension.ToLower();
            bool load;
            if (extension == ".ai")
                load = LoadAI(dialog.FileName);
            else if (extension == ".dat")
                load = LoadDatPath(dialog.FileName);
            else if (extension == ".txt")
                load = LoadTxtPath(dialog.FileName);
            else
            {
                MessageBox.Show(lang["ExceptionWrongLoad"].ToString());
                return;
            }

            if (!load) return;
            RedrawPath();
            th.Text = MultiPointList.Count.ToString();
            From.Text = "1";
            To.Text = MultiPointList[MultiPointList.Count - 1].Count().ToString();
            string show = "";
            int count = 0;
            for (int i = 0; i < MultiPointList.Count; i++)
            { 
                show += lang["curve"].ToString() + (i + 1) + lang["TotalPoint"].ToString() + MultiPointList[i].Count() + "\r"; 
                count += MultiPointList[i].Count(); 
            }
            show += lang["TotalPoint"].ToString() + count;
            MessageBox.Show(show);
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
                                170,
                                170);
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
                                170,
                                100,
                                100);
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
                                255);
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
                                255,
                                0,
                                0);
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
                                125,
                                0,
                                0);
                    else
                        color = Color.FromArgb(
                                255,
                                0,
                                86,
                                31);
                    break;
                default:
                    color = Colors.Black;
                    break;
            }
            return new SolidColorBrush(color);
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            PreviewCanvas.Children.Clear();
            MultiPointList=new List<List<Point>>();
            WarningLabel.Content = "";
        }

        private void RedrawPath()
        {
            isDirty = false;

            PreviewCanvas.Children.Clear();
            foreach (var pointlist in MultiPointList)//每加载一条新轨道就全部轨道重画一次，问题不大，不然包含瞬移的话不好搞。
                foreach (var point in pointlist)
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
            foreach (var pointList in MultiPointList)
            {
                foreach (var point in pointList)
                {
                    if (point.ellipse == null)
                        continue;

                    point.ellipse.Fill = GetPointBrush(point);
                }
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            if (MultiPointList.Count == 0) return;
            WarningLabel.Content = "";

            if (!int.TryParse(th.Text, out var t)|| !int.TryParse(From.Text, out var from) || !int.TryParse(To.Text, out var to))
            {
                WarningLabel.Content = $"{lang["ExceptionError"]}{lang["ExceptionWrongEnter"]}";
                return;
            }
            t--;
            if (t >= MultiPointList.Count)
                t = MultiPointList.Count - 1;
            if (t < 0)
                t = 0;
            if (from > to)
            {
                int swap = from;
                from = to;
                to = swap;
            }
            if (to > MultiPointList[t].Count)
                to = MultiPointList[t].Count;
            MultiPointList[t] = MultiPointList[t].GetRange(from - 1, to - from + 1);

            for (int mp = 0; mp < MultiPointList.Count; mp++)
            {
                var pointList = MultiPointList[mp];

                if (!(xexp.Text == "x" && yexp.Text == "y"))
                {
                    for (int i = 0; i < pointList.Count; i++)
                    {
                        try
                        {
                            var newx = Eval.Execute<double>(xexp.Text, new { x = pointList[i].x, y = pointList[i].y });
                            var newy = Eval.Execute<double>(yexp.Text, new { x = pointList[i].x, y = pointList[i].y });
                            pointList[i].x = newx;
                            pointList[i].y = newy;
                        }
                        catch
                        {
                            MessageBox.Show(lang["ExceptionWrongExpression"].ToString());
                            return;
                        }
                    }

                    List<Point> newpointList = new List<Point>();
                    newpointList.Add(pointList[0]);
                    int inew = 0, iold = 1;
                    while (true)//参考来源：https://github.com/C8N16O32/ZumaDeluxe-PathZoomTool
                    {
                        if (Math.Sqrt(Math.Pow(pointList[iold].x - newpointList[inew].x, 2) + Math.Pow(pointList[iold].y - newpointList[inew].y, 2)) < 1.0)
                        {
                            if (iold < pointList.Count - 1) { iold++; continue; }
                            else break;
                        }
                        double dx = pointList[iold].x - pointList[iold - 1].x, dy = pointList[iold].y - pointList[iold - 1].y, d = Math.Sqrt(dx * dx + dy * dy), ex = dx / d, ey = dy / d;
                        double dline = (newpointList[inew].y * ex - newpointList[inew].x * ey) - (pointList[iold - 1].y * ex - pointList[iold - 1].x * ey);
                        double len = Math.Sqrt(1 - (Math.Abs(dline) > 1 ? 1 : dline * dline));
                        newpointList.Add(new Point(newpointList[inew].x + dline * ey + len * ex, newpointList[inew].y - dline * ex + len * ey) { isTunnel = pointList[iold].isTunnel, order = pointList[iold].order });
                        inew++;
                    }
                    MultiPointList[mp] = newpointList;
                }

                if (InvertCheckBox.IsChecked == true) pointList.Reverse();
            }
            if (InvertCheckBox.IsChecked == true) MultiPointList.Reverse();
            th.Text = (t + 1).ToString();
            From.Text = "1";
            To.Text = (MultiPointList[t].Count).ToString();
            RedrawPath();
        }

        private void OutputButton_Click(object sender, RoutedEventArgs e)
        {
            for (int mp = 0; mp < MultiPointList.Count; mp++)
            {
                var pointList = MultiPointList[mp];
                SaveFileDialog dialog = new SaveFileDialog();
                dialog.Filter = $"{lang["RailFile"]}|*.dat|{lang["AIFile"]}|*.ai|{lang["TextFile"]}|*.txt";

                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                    return;

                var fileInfo = new FileInfo(dialog.FileName);
                var extension = fileInfo.Extension.ToLower();
                if (extension == ".dat")
                {
                    var reader = File.Create(dialog.FileName);
                    BinaryWriter bw = new BinaryWriter(reader);
                    // 写入文件头
                    if (zdr == false)
                    {
                        byte[] head = { 0x43, 0x55, 0x52, 0x56, 0x02, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00 };
                        bw.Write(head);
                    }
                    else
                    {
                        if (!(int.TryParse(start.Text, out var _start) && int.TryParse(repeat.Text, out var _repeat) && int.TryParse(single.Text, out var _single)
                            && int.TryParse(color.Text, out var _color) && float.TryParse(speed.Text, out var _speed) && int.TryParse(danger.Text, out var _danger)
                            && int.TryParse(score.Text, out var _score) && int.TryParse(skullrot.Text, out var _skullrot) && int.TryParse(zumaback.Text, out var _zumaback)
                            && int.TryParse(zumaslow.Text, out var _zumaslow) && float.TryParse(dangerratio.Text, out var _dangerratio) && int.TryParse(maxclumps.Text, out var _maxclumps)))
                        {
                            WarningLabel.Content = $"{lang["ExceptionError"]}{lang["ExceptionWrongEnter"]}";
                            return;
                        }
                        byte[] head = { 0x43, 0x55, 0x52, 0x56, 0x0F, 0x00, 0x00, 0x00, 0x00 };
                        bw.Write(head);
                        bw.Write(_start);
                        {
                            byte[] temp = { 0x00, 0x00, 0x00, 0x00 };
                            bw.Write(temp);
                        }
                        bw.Write(_repeat);
                        bw.Write(_single);
                        bw.Write(_color);
                        bw.Write(_speed);
                        bw.Write(_danger);
                        {
                            byte[] temp = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xC8, 0x42 };
                            bw.Write(temp);
                        }
                        bw.Write(_score);
                        bw.Write(_skullrot);
                        bw.Write(_zumaback);
                        bw.Write(_zumaslow);
                        bw.Write(_dangerratio);
                        bw.Write(_maxclumps);
                        {
                            byte[] temp = { 0x00, 0x00, 0x00, 0x00, 0x58, 0x02, 0x00, 0x00, 0x01, 0x01 };
                            bw.Write(temp);
                        }
                        bw.Write(destroyall.IsChecked == false);
                        bw.Write(hide.IsChecked == false);
                        bw.Write(invincible.IsChecked == false);
                        {
                            byte[] temp = { 0x00, 0x01, 0x01, 0x00, 0x00, 0x00 };
                            bw.Write(temp);
                        }
                    }

                    // 使用关键点位置来写版权信息
                    string credit = "\rMadeByGScienceStudio(一身正气小完能)&ImprovedByLookingForDreams(抹蜜的蜂)\t" + DateTime.Now.ToString() + "\tVersion: 2.0.0\r";
                    bw.Write(Encoding.Default.GetByteCount(credit));
                    bw.Write(Encoding.Default.GetBytes(credit));

                    if (warp.IsChecked == true && zdr == true)
                    {
                        int total = 0;
                        foreach (var part in MultiPointList)
                            total += part.Count + 1;
                        bw.Write(total - 1);//点的总数，加上起点
                    }
                    else
                        bw.Write(pointList.Count);

                    while (true)
                    {
                        if (zdr == true) { byte[] temp = { 0x02, 0x01 }; bw.Write(temp); }
                        bw.Write(((float)pointList[0].x));
                        bw.Write(((float)pointList[0].y));
                        bw.Write(pointList[0].isTunnel);
                        bw.Write((byte)pointList[0].order);

                        for (int i = 1; i < pointList.Count; ++i)
                        {
                            var offset = pointList[i] - pointList[i - 1];

                            offset *= 100;

                            if (offset.x > 255 || offset.x < -256 ||
                                offset.y > 255 || offset.y < -256)
                            {
                                MessageBox.Show(lang["ExceptionWrongCantCalculateOffset"].ToString());
                                return;
                            }
                            bw.Write((sbyte)offset.x);
                            bw.Write((sbyte)offset.y);
                            bw.Write(pointList[i].isTunnel);
                            bw.Write((byte)pointList[i].order);
                        }
                        if (warp.IsChecked == true && zdr == true)
                        {
                            if (++mp == MultiPointList.Count) break;

                            var vec = (pointList[pointList.Count - 1] - pointList[pointList.Count - 2]) * 10;
                            bw.Write((sbyte)vec.x);
                            bw.Write((sbyte)vec.y);

                            pointList = MultiPointList[mp];
                        }
                        else
                        {
                            break;
                        }
                    }
                    bw.Close();
                }
                else if (extension == ".ai")
                {
                    var writer = File.CreateText(dialog.FileName);

                    writer.WriteLine($"%%BoundingBox: 0 0 {PreviewCanvas.Width} {PreviewCanvas.Height}");

                    while (true)
                    {
                        writer.WriteLine("1 XR");

                        writer.WriteLine($"{pointList[0].x} {PreviewCanvas.Height - pointList[0].y} m");

                        for (int i = 1; i < pointList.Count; ++i)
                            writer.WriteLine($"{pointList[i].x} {PreviewCanvas.Height - pointList[i].y} l");

                        writer.WriteLine("N");
                        if (warp.IsChecked == true && zdr == true)
                        {
                            if (++mp == MultiPointList.Count) break;
                            pointList = MultiPointList[mp];
                        }
                        else
                        {
                            break;
                        }
                    }
                    writer.Close();
                }
                else if (extension == ".txt")
                {
                    var writer = File.CreateText(dialog.FileName);

                    writer.WriteLine("#Zuma Curve Editor");
                    writer.WriteLine("#Made By GScience Studio(一身正气小完能), Improved By LookingForDreams(抹蜜的蜂)");
                    writer.WriteLine($"#{DateTime.Now.ToString()}");

                    if (zdr == true)
                    {
                        if (!(int.TryParse(start.Text, out _) && int.TryParse(repeat.Text, out _) && int.TryParse(single.Text, out _) && int.TryParse(color.Text, out _)
                            && float.TryParse(speed.Text, out _) && int.TryParse(danger.Text, out _) && int.TryParse(score.Text, out _) && int.TryParse(skullrot.Text, out _)
                            && int.TryParse(zumaback.Text, out _) && int.TryParse(zumaslow.Text, out _) && float.TryParse(dangerratio.Text, out _) && int.TryParse(maxclumps.Text, out _)))
                        {
                            WarningLabel.Content = $"{lang["ExceptionError"]}{lang["ExceptionWrongEnter"]}";
                            return;
                        }
                        writer.WriteLine("zr");

                        writer.WriteLine("z" + start.Text);

                        writer.WriteLine("z" + repeat.Text);
                        writer.WriteLine("z" + single.Text);
                        writer.WriteLine("z" + color.Text);
                        writer.WriteLine("z" + speed.Text);
                        writer.WriteLine("z" + danger.Text);


                        writer.WriteLine("z" + score.Text);
                        writer.WriteLine("z" + skullrot.Text);
                        writer.WriteLine("z" + zumaback.Text);
                        writer.WriteLine("z" + zumaslow.Text);
                        writer.WriteLine("z" + dangerratio.Text);
                        writer.WriteLine("z" + maxclumps.Text);

                        writer.WriteLine("z" + destroyall.IsChecked);
                        writer.WriteLine("z" + hide.IsChecked);
                        writer.WriteLine("z" + invincible.IsChecked);
                    }
                    else
                    {
                        writer.WriteLine("zd");
                    }

                    while (true)
                    {
                        writer.WriteLine($"{pointList[0].x} {pointList[0].y} {(pointList[0].isTunnel ? 1 : 0)} {pointList[0].order}");

                        for (int i = 1; i < pointList.Count; ++i)
                        {
                            var lastPoint = pointList[i - 1];
                            var currentPoint = pointList[i];

                            var offset = currentPoint - lastPoint;

                            offset *= 100;

                            if (offset.x > 255 || offset.x < -256 ||
                                offset.y > 255 || offset.y < -256)
                            {
                                MessageBox.Show(lang["ExceptionWrongCantCalculateOffset"].ToString());
                                return;
                            }
                            int p1 = currentPoint.isTunnel ? 1 : 0;
                            int p2 = currentPoint.order;
                            writer.WriteLine($"{(int)offset.x} {(int)offset.y} {p1} {p2}");
                        }
                        if (warp.IsChecked == true && zdr == true)
                        {
                            if (++mp == MultiPointList.Count) break;
                            pointList = MultiPointList[mp];
                            writer.WriteLine("=======");
                        }
                        else
                        {
                            break;
                        }
                    }
                    writer.Close();
                }
                else
                    return;
            }
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

            if (MultiPointList == null || MultiPointList.Count == 0)
                return;

            var pos = e.GetPosition(PreviewWindow);
            if (pos.X < 0 || pos.Y < 0
              || pos.X > PreviewWindow.ViewportWidth
              || pos.Y > PreviewWindow.ViewportHeight)
                return;

            if (!int.TryParse(OrderTextBox.Text, out var order))
            {
                WarningLabel.Content = $"{lang["ExceptionError"]}{lang["ExceptionWrongOrderShouldBeNumber"]}";
                return;
            }

            pos = e.GetPosition(PreviewCanvas);//修复bug
            var mousePos = new Point(pos.X, pos.Y);

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                for (var j = 0; j < MultiPointList.Count; ++j)
                {
                    for (var i = 0; i < MultiPointList[j].Count; ++i)
                    {
                        if (lastSetPropertyPointIndex != -1 &&
                            Math.Abs(lastSetPropertyPointIndex - i) > BrushSlider.Value &&
                            ContinueCheckBox.IsChecked.Value)
                            continue;

                        var point = MultiPointList[j][i];

                        if (point.Distane(mousePos) > BrushSlider.Value / 2)
                            continue;

                        point.isTunnel = TunnelCheckBox.IsChecked.Value;
                        point.order = order;
                        isDirty = true;

                        lastSetPropertyPointIndex = i;
                    }
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
            dialog.Filter = $"{lang["ImageFile"]}|*.png; *.jpg; *.jpeg; *.gif|{lang["AllFile"]}|*.*";

            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;
            try
            {
                PreviewCanvas.Background = new ImageBrush(new BitmapImage(new Uri(dialog.FileName)));
            }
            catch 
            {
                MessageBox.Show(lang["ExceptionWrongLoad"].ToString());
            }
        }

        private void WarningLabel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            WarningLabel.Content = "";
        }

        private void zd_RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            zdr = false;
            zrdiff.IsEnabled = false;
            CanvasWidth.Text = "640";
            CanvasHeight.Text = "480";
            PreviewCanvas.Width = 640;
            PreviewCanvas.Height = 480;
        }
        private void zr_RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            zdr = true;
            zrdiff.IsEnabled = true;
            CanvasWidth.Text = "800";
            CanvasHeight.Text = "600";
            PreviewCanvas.Width = 800;
            PreviewCanvas.Height = 600;
        }
    }
}
