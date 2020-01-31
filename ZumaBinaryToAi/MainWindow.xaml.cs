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
using Z.Expressions;
using MessageBox = System.Windows.MessageBox;

namespace ZumaBinaryToAi
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private struct Point
        {
            public double x;
            public double y;
            public int canHit;
            public int layer;
        }

        public MainWindow()
        {
            InitializeComponent();
        }

        private List<Point> pointList;
        
        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();

            dialog.Filter = "轨道文件|*.dat";

            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            var levelGeneratorPath = Environment.CurrentDirectory + "\\ZumaLevelBuilder.exe";
            if (!File.Exists(levelGeneratorPath))
            {
                MessageBox.Show("未找到ZumaLevelBuilder.exe");
                return;
            }

            var process = Process.Start(levelGeneratorPath, $"\"{dialog.FileName}\" \"{dialog.FileName + ".txt"}\" btt");
            if (!process.WaitForExit(10000) || process.ExitCode != 0)
            {
                MessageBox.Show("出现异常，生成失败");
                process.Kill();
                return;
            }

            var reader = File.OpenText(dialog.FileName + ".txt");

            pointList = new List<Point>();

            while (!reader.EndOfStream)
            {
                var point = new Point();
                var args = reader.ReadLine().Split(' ');

                if (args.Length != 4)
                    continue;

                if (!double.TryParse(args[0], out point.x) ||
                    !double.TryParse(args[1], out point.y) ||
                    !int.TryParse(args[2], out point.canHit) ||
                    !int.TryParse(args[3], out point.layer))
                    continue;

                pointList.Add(point);
            }

            reader.Close();

            PointCountLabel.Content = "" + pointList.Count;
        }

        private double DoExpression(string str, double num)
        {
            try
            {
                return Eval.Execute<double>(str, new { num = num });
            }
            catch
            {
                return 0;
            }
        }

        private void OutputButton_Click(object sender, RoutedEventArgs e)
        {
            if (pointList == null || pointList.Count == 0)
                return;

            var dialog = new SaveFileDialog();

            dialog.Filter = "AI曲线|*.ai";

            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            var writer = File.CreateText(dialog.FileName);

            var xExpression = XExpressionTextBox.Text;
            var yExpression = YExpressionTextBox.Text;

            double currentPosX = pointList[0].x;
            double currentPosY = pointList[0].y;

            writer.WriteLine("%%BoundingBox: 0 0 640 480");
            writer.WriteLine("1 XR");

            writer.WriteLine($"{DoExpression(xExpression, currentPosX)} {DoExpression(yExpression, 480 - currentPosY)} m");

            for (var i = 1; i < pointList.Count; ++i)
            {
                currentPosX += pointList[i].x / 100.0;
                currentPosY += pointList[i].y / 100.0;
                writer.WriteLine($"{DoExpression(xExpression, currentPosX)} {DoExpression(yExpression, 480 - currentPosY)} l");
            }
            writer.WriteLine("N");
            writer.Close();
            MessageBox.Show("导出成功");
            
        }
    }
}
