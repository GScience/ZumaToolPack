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
using Application = System.Windows.Application;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using MessageBox = System.Windows.MessageBox;

namespace ZumaBinaryToAi
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        ResourceDictionary lang;

        private struct Point
        {
            public double x;
            public double y;
            public int canHit;
            public int layer;
        }

        private string fileName;

        public MainWindow()
        {
            List<ResourceDictionary> dictionaryList = new List<ResourceDictionary>();
            foreach (ResourceDictionary dictionary in Application.Current.Resources.MergedDictionaries)
            {
                dictionaryList.Add(dictionary);
            }

            string requestedCulture;
            if (System.Threading.Thread.CurrentThread.CurrentCulture.Name == "zh-CN")
                requestedCulture = @"Resources\Lang\zh-cn.xaml";
            else
                requestedCulture = @"Resources\Lang\en-us.xaml";
            ResourceDictionary resourceDictionary 
                = dictionaryList.FirstOrDefault(d => d.Source.OriginalString.Equals(requestedCulture));

            Application.Current.Resources.MergedDictionaries.Remove(resourceDictionary);
            Application.Current.Resources.MergedDictionaries.Add(resourceDictionary);

            lang = resourceDictionary;

            InitializeComponent();
        }

        private List<Point> pointList;
        
        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();

            dialog.Filter = $"{lang["RailFile"]}| *.dat";

            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            LoadFile(dialog.FileName);
        }

        private double DoExpression(string str, double x, double y)
        {
            try
            {
                return Eval.Execute<double>(str, new { x = x, y = y });
            }
            catch
            {
                return 0;
            }
        }

        private void LoadFile(string filePath)
        {
            fileName = filePath;

            var levelGeneratorPath = Environment.CurrentDirectory + "\\ZumaLevelBuilder.exe";
            if (!File.Exists(levelGeneratorPath))
            {
                MessageBox.Show(lang["ExpectionToolNotFound"].ToString());
                return;
            }

            var process = Process.Start(levelGeneratorPath, $"\"{filePath}\" \"{filePath + ".txt"}\" btt");
            if (!process.WaitForExit(10000) || process.ExitCode != 0)
            {
                MessageBox.Show(lang["Expection"].ToString());
                process.Kill();
                return;
            }

            var reader = File.OpenText(filePath + ".txt");

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
            ToTextBox.Text = "" + pointList.Count;
        }

        private void GenAI(string filePath, List<Point> points)
        {
            var writer = File.CreateText(filePath);
            writer.WriteLine("%%BoundingBox: 0 0 640 480");
            writer.WriteLine("1 XR");

            writer.WriteLine($"{points[0].x} {480 - points[0].y} m");

            for (var i = 1; i < points.Count; ++i)
                writer.WriteLine($"{points[i].x} {480 - points[i].y} l");

            writer.WriteLine("N");
            writer.Close();
        }

        private void GenDat(string filePath, List<Point> points)
        {
            var writer = File.CreateText(filePath + ".txt");
            writer.WriteLine("# By Zuma Binary To AI");

            double lastPointX;
            double lastPointY;

            writer.WriteLine($"{points[0].x} {points[0].y} {points[0].canHit} {points[0].layer}");

            lastPointX = points[0].x;
            lastPointY = points[0].y;

            for (var i = 1; i < points.Count; ++i)
            {
                writer.WriteLine($"{(points[i].x - lastPointX) * 100} {(points[i].y - lastPointY) * 100} {points[i].canHit} {points[i].layer}");
                lastPointX = points[i].x;
                lastPointY = points[i].y;
            }

            writer.Close();

            var levelGeneratorPath = Environment.CurrentDirectory + "\\ZumaLevelBuilder.exe";
            if (!File.Exists(levelGeneratorPath))
            {
                MessageBox.Show(lang["ExpectionToolNotFound"].ToString());
                return;
            }

            var process = Process.Start(levelGeneratorPath, $"\"{filePath + ".txt"}\" \"{filePath}\" ttb");
            if (!process.WaitForExit(10000) || process.ExitCode != 0)
            {
                MessageBox.Show(lang["Expection"].ToString());
                process.Kill();
                return;
            }
        }

        private void OutputButton_Click(object sender, RoutedEventArgs e)
        {
            if (pointList == null || pointList.Count == 0)
                return;

            var dialog = new SaveFileDialog();

            dialog.Filter = $"{lang["RailFile"]}|*.dat|{lang["AIFile"]}|*.ai";
            var fileInfo = new FileInfo(fileName);
            dialog.InitialDirectory = fileInfo.DirectoryName;
            dialog.FileName = fileInfo.Name;

            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            var realPointList = new List<Point>();

            var xExpression = XExpressionTextBox.Text;
            var yExpression = YExpressionTextBox.Text;

            double currentPosX = pointList[0].x;
            double currentPosY = pointList[0].y;

            realPointList.Add(new Point
            {
                x = DoExpression(xExpression, currentPosX, currentPosY),
                y = DoExpression(yExpression, currentPosX, currentPosY),
                canHit = pointList[0].canHit,
                layer = pointList[0].layer
            });

            for (var i = 1; i < pointList.Count; ++i)
            {
                currentPosX += pointList[i].x / 100.0;
                currentPosY += pointList[i].y / 100.0;

                realPointList.Add(new Point
                {
                    x = DoExpression(xExpression, currentPosX, currentPosY),
                    y = DoExpression(yExpression, currentPosX, currentPosY),
                    canHit = pointList[i].canHit,
                    layer = pointList[i].layer
                });
            }

            if (!int.TryParse(FromTextBox.Text, out var from))
                MessageBox.Show("范围非法");

            if (!int.TryParse(ToTextBox.Text, out var to))
                MessageBox.Show("范围非法");

            if (from >= to)
                MessageBox.Show("范围非法");

            if (to >= realPointList.Count)
                MessageBox.Show("范围非法");

            realPointList = realPointList.GetRange(from, to);

            if (InvertCheckBox.IsChecked == true)
                realPointList.Reverse();

            var extension = new FileInfo(dialog.FileName).Extension;

            if (extension == ".ai")
                GenAI(dialog.FileName, realPointList);
            else if (extension == ".dat")
                GenDat(dialog.FileName, realPointList);
            MessageBox.Show("导出成功");
            
        }

        private void Window_DragEnter(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Link;
            else
                e.Effects = DragDropEffects.None;
        }

        private void Window_Drop(object sender, System.Windows.DragEventArgs e)
        {
            var file = (Array)e.Data.GetData(DataFormats.FileDrop);
            LoadFile(file.GetValue(0).ToString());
        }
    }
}
