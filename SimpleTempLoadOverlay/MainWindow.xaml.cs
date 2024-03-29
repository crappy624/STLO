﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.ComponentModel;
using System.Threading;
using OpenHardwareMonitor.Hardware;
using System.Reflection;

namespace SimpleTempLoadOverlay
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window,INotifyPropertyChanged
    {

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string info)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(info));
            }
        }

        Canvas Canvas1 = new Canvas();
        Window overlay = new Window();

        TextBlock cpuload;
        TextBlock cputemp;
        TextBlock gputemp;

        Computer cpt = new Computer();
        public static int[,] data;
        public static int numberOfCores = Environment.ProcessorCount;
        public static bool twoGPUs = false;

        public MainWindow()
        {
            InitializeComponent();
        }


        public const int WS_EX_TRANSPARENT = 0x00000020;
        public const int GWL_EXSTYLE = (-20);

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Get this window's handle
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            IntPtr hwnd2 = new WindowInteropHelper(overlay).Handle;

            // Change the extended window style to include WS_EX_TRANSPARENT
            int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT);

            int extendedStyle2 = GetWindowLong(hwnd2, GWL_EXSTYLE);
            SetWindowLong(hwnd2, GWL_EXSTYLE, extendedStyle2 | WS_EX_TRANSPARENT);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

            var notifyIcon = new System.Windows.Forms.NotifyIcon
            {
                Visible = true,
                Icon = System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location),
                Text = Title
            };

            notifyIcon.ShowBalloonTip(1, "STLO", "Hardware Monitoring started", System.Windows.Forms.ToolTipIcon.Info);

            System.Windows.Forms.ContextMenu contextMenu1 = new System.Windows.Forms.ContextMenu();

            notifyIcon.ContextMenu = contextMenu1;

            notifyIcon.ContextMenu.MenuItems.Add("Exit", (s, ea) => Application.Current.Shutdown());
            

            overlay.Background = System.Windows.Media.Brushes.Transparent;
            overlay.Top = 0;
            overlay.Left = 0;
            overlay.WindowStyle = WindowStyle.None;
            overlay.Topmost = true;
            overlay.Content = Canvas1;
            overlay.AllowsTransparency = true;
            overlay.IsHitTestVisible = false;
            overlay.ShowInTaskbar = false;
            cpuload = Text(1, 1, System.Windows.Media.Color.FromArgb(255, 255, 255, 255) );
            cputemp = Text(1, 16, System.Windows.Media.Color.FromArgb(255, 255, 255, 255));
            gputemp = Text(1, 31, System.Windows.Media.Color.FromArgb(255, 255, 255, 255));
            overlay.Show();


            Thread mainloop = new Thread(Loop);
            mainloop.Name = "UpdateValues";
            mainloop.IsBackground = true;
            mainloop.Priority = ThreadPriority.BelowNormal;
            mainloop.Start();

            

    }

        private void Loop()
        {
            Pop_Data();
            InitComp(cpt);

            while (true)
            {
                //logik
                //new ManualResetEvent(false).WaitOne(1500);

                Update_Values(cpt);
                AvgMinMax();
                Display();

                Thread.Sleep(1500);

            }
        }

        private TextBlock Text(double x, double y, System.Windows.Media.Color color)
        {

            TextBlock textBlock = new TextBlock();
            textBlock.FontSize = 10;
            //textBlock.Name = tb_name;

            //textBlock.SetBinding(TextBlock.TextProperty, binding);
            //textBlock.Text = ((string)reference);

            textBlock.Foreground = new SolidColorBrush(color);
            textBlock.IsHitTestVisible = false;

            Canvas1.IsHitTestVisible = false;

            Canvas.SetLeft(textBlock, x);

            Canvas.SetTop(textBlock, y);


            Canvas1.Children.Add(textBlock);

            return textBlock;
        }

        public void Pop_Data()
        {

            int[,] tmp = new int[3, 4];

            for (int y = 0; y < 3; y++)
                for (int x = 0; x < 4; x++)
                    if (x != 2)
                    {
                        tmp[y, x] = 0;
                    }
                    else
                    {
                        tmp[y, x] = 100;
                    }

            data = tmp;
        }

        public static void InitComp(Computer cmp)
        {
            cmp.Open();
            cmp.CPUEnabled = true;
            cmp.GPUEnabled = true;
        }

        public static void Update_Values(Computer cmp)
        {

            data[0, 1] = 0;
            data[0, 0] = 0;
            int ct = 0;

            foreach (var hardware in cmp.Hardware)
            {

                hardware.Update();

                if (twoGPUs)
                {
                    if (ct == 0)
                        ct++;
                    else
                        ct--;
                }


                foreach (var sensor in hardware.Sensors)
                {

                    //CPU temps & Loads
                    if (sensor.Hardware.HardwareType == HardwareType.CPU)
                    {
                        if (sensor.SensorType == SensorType.Temperature && sensor.Value != null && sensor.Name != "CPU Package")
                        {
                            data[0, 1] += (int)sensor.Value;
                        }
                        if (sensor.SensorType == SensorType.Load && sensor.Name != "CPU Total" && sensor.Value != null)
                        {
                            data[0, 0] += (int)sensor.Value;
                        }
                    }
                    //GPU Temps & Loads
                    else if (sensor.Hardware.HardwareType == HardwareType.GpuNvidia || sensor.Hardware.HardwareType == HardwareType.GpuAti)
                    {

                        if (twoGPUs)
                        {
                            if (ct != 0)
                            {
                                if (sensor.SensorType == SensorType.Temperature && sensor.Value != null)
                                    data[1, 1] = (int)sensor.Value;
                                if (sensor.SensorType == SensorType.Load && sensor.Name == "GPU Core" && sensor.Value != null)
                                    data[1, 0] = (int)sensor.Value;
                            }
                            else
                            {
                                if (sensor.SensorType == SensorType.Temperature && sensor.Value != null)
                                    data[2, 1] = (int)sensor.Value;
                                if (sensor.SensorType == SensorType.Load && sensor.Name == "GPU Core" && sensor.Value != null)
                                    data[2, 0] = (int)sensor.Value;
                            }
                        }
                        else
                        {
                            if (sensor.SensorType == SensorType.Temperature && sensor.Value != null)
                                data[1, 1] = (int)sensor.Value;
                            if (sensor.SensorType == SensorType.Load && sensor.Name == "GPU Core" && sensor.Value != null)
                                data[1, 0] = (int)sensor.Value;
                        }

                    }

                }
            }


        }

        public static void AvgMinMax()
        {

            //CPU Average, divided by 4 for a 4-cores CPU
            data[0, 1] = data[0, 1] / numberOfCores;
            data[0, 0] = data[0, 0] / numberOfCores;

            //MinMax CPU
            if (data[0, 1] < data[0, 2])
                data[0, 2] = data[0, 1];
            if (data[0, 1] > data[0, 3])
                data[0, 3] = data[0, 1];

            //MinMax GPU1
            if (data[1, 1] < data[1, 2])
                data[1, 2] = data[1, 1];
            if (data[1, 1] > data[1, 3])
                data[1, 3] = data[1, 1];

            //MinMax GPU2
            if (twoGPUs)
            {
                if (data[2, 1] < data[2, 2])
                    data[2, 2] = data[2, 1];
                if (data[2, 1] > data[2, 3])
                    data[2, 3] = data[2, 1];
            }
        }

        public void Display()
        {
            overlay.Dispatcher.BeginInvoke((Action)(() =>
            {
                
                gputemp.Text = " GPU-Temp: " + data[1, 1].ToString() + " °C";
                //curr_gpu_load = data[1, 0].ToString() + " %";
                cputemp.Text = " CPU-Temp: " + data[0, 1].ToString() + " °C";
                cpuload.Text = " CPU-Load: " + data[0, 0].ToString() + " %";
            }));
            
        }

    }
}
