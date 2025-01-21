using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using LinePutScript;
using LinePutScript.Converter;
using LinePutScript.Localization.WPF;
using Panuon.WPF.UI;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using VPet_Simulator.Core;
using VPet_Simulator.Windows.Interface;
using VPet_Simulator.Windows;
using System.Collections.Generic;
using System.Windows.Media;
using IWshRuntimeLibrary;
using File = System.IO.File;

namespace ProcessMonitorAPP
{
    /// <summary>
    /// winSetting.xaml 的交互逻辑
    /// </summary>
    public partial class winSetting : Window
    {
        ProcessMonitor vts;
        /// <summary>
        /// 添加字段来接收由ProcessMonitorAPP.cs传来的mod路径
        /// </summary>
        private string modPath;
        /// <summary>
        /// 添加字段来接收由ProcessMonitorAPP.cs传来的process_paths.txt的路径
        /// </summary>
        private string txtfilePath;
        /// <summary>
        /// 添加字段来接收由ProcessMonitorAPP.cs传来的monitor_set.txt的路径
        /// </summary>
        private string txtsetPath;
        /// <summary>
        /// 添加布尔值来接收由ProcessMonitorAPP.cs传来的EnableFullScreenMonitor属性值
        /// </summary>
        private bool EnableFullScreenMonitor;
        /// <summary>
        /// 添加布尔值来保存用户对于全屏取消置顶的临时设置
        /// </summary>
        private bool isFullScreenMonitorEnabled;
        /// <summary>
        /// 记录此时全屏窗口数量
        /// 如果EnableFullScreenMonitor为false时, 该数值应保持为0
        /// </summary>
        public int CountFullScreen { get; set; }


        /// <summary>
        /// 加载设置窗口
        /// </summary>
        /// <param name="vts"></param>
        public winSetting(ProcessMonitor vts)
        {
            InitializeComponent();
            this.vts = vts;
            modPath = vts.modPath;  // 从ProcessMonitor实例获取modPath
            txtfilePath = vts.txtfilePath;  // 从ProcessMonitor实例获取txtfilePath
            txtsetPath = vts.txtsetPath;  // 从ProcessMonitor实例获取txtsetPath
            LoadPaths(); // 加载保存的路径
            vts.LoadOtherSettings();
            EnableFullScreenMonitor = vts.EnableFullScreenMonitor;
            EnableFullScreenMonitorSwitchOn.IsChecked = EnableFullScreenMonitor; // 显示是否启用全屏监控状态
            SetupDragDrop();  // 设置拖放支持
        }
        
        private void Window_Closed(object sender, EventArgs e)
        {
            vts.winSetting = null;
        }

        /// <summary>
        /// 通过静态方法启动Processmonitor类中定时器_FullScreenMonitor
        /// </summary>
        private static void StartTimerFullScreenMonitor() => ProcessMonitor.StartTimerFullScreenMonitor();  // 调用静态方法启动定时器

        // 通过静态方法停止Processmonitor类中定时器_FullScreenMonitor
        private static void StopTimerFullScreenMonitor() => ProcessMonitor.StopTimerFullScreenMonitor();  // 调用静态方法停止定时器

        /// <summary>
        /// 配置窗口以允许拖拽文件进入窗口
        /// 此方法初始化窗口的拖拽相关事件处理 允许文件被拖放到窗口上
        /// </summary>
        private void SetupDragDrop()
        {
            this.AllowDrop = true;
            this.DragEnter += OnDragEnter;
            this.Drop += OnDrop;
        }

        /// <summary>
        /// 处理拖拽进入窗口的事件
        /// 当文件被拖拽到窗口边界时调用此方法 设置拖拽效果
        /// </summary>
        /// <param name="e">包含事件数据的 DragEventArgs 包括被拖拽的数据和影响拖拽操作的效果</param>
        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Link;
            else
                e.Effects = DragDropEffects.None;
        }

        /// <summary>
        /// 对放入的lnk格式的快捷方式的处理
        /// </summary>
        /// <param name="e">包含事件数据的DragEventArgs对象 此对象提供对拖放的文件信息的访问</param>
        private void OnDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            foreach (string file in files)
            {
                string path = null;
                if (Path.GetExtension(file).ToLower() == ".lnk")
                {
                    path = ResolveShortcut(file);
                }

                if (!string.IsNullOrEmpty(path))
                {
                    // 直接添加路径到界面，不立即保存
                    AddPathTextBox(Path.GetFileNameWithoutExtension(path), path);
                }
            }
        }

        /// <summary>
        /// 解析Windows快捷方式文件(.lnk)并返回链接到的原始文件的路径
        /// 如果提供的路径不是有效的快捷方式或无法解析快捷方式 则返回 null
        /// </summary>
        /// <param name="shortcutPath">快捷方式文件的完整路径</param>
        /// <returns>快捷方式指向的文件的完整路径; 如果快捷方式无效或无法解析 则为 null</returns>
        private string ResolveShortcut(string shortcutPath)
        {
            try
            {
                if (Path.GetExtension(shortcutPath).ToLower() == ".lnk")
                {
                    WshShell wshShell = new WshShell();
                    IWshShortcut shortcut = (IWshShortcut)wshShell.CreateShortcut(shortcutPath);
                    return shortcut.TargetPath;
                }
            }
            catch (Exception ex)
            {
                string error = "Error resolving shortcut: " + ex.Message;
                MessageBox.Show(error);
                vts.LogErrorToFile(error);  // 记录错误到文件
            }
            return null;
        }


        /// <summary>
        /// 存储一组文本框对 每对文本框用于输入和显示进程的名称和路径
        /// 该列表被用于动态管理界面上的进程配置输入字段 允许用户添加、编辑和删除进程监控条目
        /// </summary>
        private List<(TextBox nameTextBox, TextBox pathTextBox)> _textBoxes = new List<(TextBox, TextBox)>();

        /// <summary>
        /// 从配置文件读取路径信息 并初始化对应的文本框
        /// </summary>
        private void LoadPaths()
        {
            if (File.Exists(txtfilePath))
            {
                var lines = File.ReadAllLines(txtfilePath);
                foreach (var line in lines)
                {
                    var parts = line.Split(new[] { '|' }, 2);
                    if (parts.Length == 2)
                    {
                        AddPathTextBox(parts[0], parts[1], true);
                    }
                }
            }
            // 如果 _textBoxes 为空或文件内容为空，确保至少有一行空的输入框
            if (_textBoxes.Count == 0)
            {
                AddPathTextBox(); // 添加一行空的输入框
            }
        }

        /// <summary>
        /// 处理点击事件以添加一个新的路径文本框到界面上
        /// </summary>
        private void AddPathTextBox_Click(object sender, RoutedEventArgs e)
        {
            AddPathTextBox();
        }

        /// <summary>
        /// 处理保存路径按钮的点击事件 该方法从界面中获取用户输入的进程名称和路径 验证并保存合法的进程路径到配置文件 然后重新加载和启动监控程序。
        /// </summary>
        private void SavePathsButton_Click(object sender, RoutedEventArgs e)
        {
            List<string> lines = new List<string>();
            foreach (var (nameTextBox, pathTextBox) in _textBoxes)
            {
                string name = nameTextBox.Text;
                string processPath = pathTextBox.Text.Trim(); // 移除路径两端的空白
                processPath = processPath.Trim('"');// 移除路径两端的引号

                // 路径不为空才进行保存等操作
                if (!string.IsNullOrWhiteSpace(processPath))
                {
                    // 如果路径不为空且名称为空，则自动生成名称
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        name = Path.GetFileNameWithoutExtension(processPath);
                        nameTextBox.Text = name;  // 自动填写名称到界面上
                        pathTextBox.Text = processPath;  // 自动填写整理后的路径到界面上
                    }

                    // 只有当文件存在时才添加到保存列表
                    if (File.Exists(processPath))
                    {
                        lines.Add($"{name}|{processPath}");
                    }
                }
            }
            File.WriteAllLines(txtfilePath, lines);

            // 如果 _textBoxes 为空或文件内容为空，确保至少有一行空的输入框
            if (_textBoxes.Count == 0)
            {
                AddPathTextBox(); // 添加一行空的输入框
            }

            MessageBox.Show("路径保存成功!".Translate(), "Information", MessageBoxButton.OK, MessageBoxImage.Information);

            // 重新加载并启动监控程序
            vts.ReloadAndMonitorProcesses();
        }

        /// <summary>
        /// 动态添加一行路径输入区域到界面上 包括一个名称输入框、一个路径输入框和一个移除按钮
        /// </summary>
        /// <param name="name">路径条目的名称，默认为空字符串，显示在第一个文本框中。</param>
        /// <param name="text">路径的具体文本，默认为空字符串，显示在第二个文本框中。</param>
        /// <param name="isSavedPath">指示该路径是否是已保存路径，默认为 false，此参数目前未使用，可以用于将来的扩展。</param>
        private void AddPathTextBox(string name = "", string text = "", bool isSavedPath = false)
        {
            StackPanel panel = new StackPanel { Orientation = Orientation.Horizontal };
            TextBox nameTextBox = new TextBox { Text = name, Width = 100, Margin = new Thickness(0, 10, 0, 0) };
            TextBox pathTextBox = new TextBox { Text = text, Width = 300, Margin = new Thickness(10, 10, 0, 0) };
            Button removeButton = new Button
            {
                Content = "移除".Translate(),
                Margin = new Thickness(10, 10, 0, 0),
                Background = AddPath_Button.Background,
                // Background = (Brush)new BrushConverter().ConvertFrom("#FFADD7F9"),
                BorderBrush = AddPath_Button.BorderBrush,
                // BorderBrush = (Brush)new BrushConverter().ConvertFrom("#FF6BB1E9"),
                BorderThickness = new Thickness(2)
            };
            // 设置按钮为圆角
            ButtonHelper.SetCornerRadius(removeButton, new CornerRadius(4));

            removeButton.Click += (s, e) => RemovePath(panel);

            panel.Children.Add(nameTextBox);
            panel.Children.Add(pathTextBox);
            panel.Children.Add(removeButton);

            InputPanel.Children.Add(panel);  // 确保新路径行添加在列表底部

            _textBoxes.Add((nameTextBox, pathTextBox));  // 将新文本框添加到跟踪列表中
        }

        /// <summary>
        /// 当用户开启全屏监控勾选框状态时调用
        /// </summary>
        private void EnableFullScreenMonitorSwitchOn_Checked(object sender, RoutedEventArgs e)
        {
            isFullScreenMonitorEnabled = true;
            SaveOtherSettings();
            vts.MonitorFullScreen();
            StartTimerFullScreenMonitor();  // 启动全屏窗口监测定时器
        }
        /// <summary>
        /// 当用户关闭全屏监控勾选框状态时调用
        /// </summary>
        private void EnableFullScreenMonitorSwitchOn_Unchecked(object sender, RoutedEventArgs e)
        {
            isFullScreenMonitorEnabled = false;
            SaveOtherSettings();
            vts.MonitorFullScreen();
            StopTimerFullScreenMonitor();  // 停止全屏窗口监测定时器
        }

        public void DisableFullScreenMonitor()
        {
            isFullScreenMonitorEnabled = false;
            SaveOtherSettings();
            vts.MonitorFullScreen();
            StopTimerFullScreenMonitor();  // 停止全屏窗口监测定时器
        }

        /// <summary>
        /// 保存其他设置
        /// </summary>
        private void SaveOtherSettings()
        {
            EnableFullScreenMonitor = isFullScreenMonitorEnabled;
            List<string> lines = new List<string>();
            lines.Add($"FullScreenMonitorSetting|{EnableFullScreenMonitor}");
            File.WriteAllLines(txtsetPath, lines);
        }

        /// <summary>
        /// 从界面中移除指定的StackPanel控件 并更新内部数据结构以反映这一变化
        /// </summary>
        /// <param name="panel"></param>
        private void RemovePath(StackPanel panel)
        {
            InputPanel.Children.Remove(panel);
            var nameTextBox = (TextBox)panel.Children[0];
            var pathTextBox = (TextBox)panel.Children[1];
            _textBoxes.Remove((nameTextBox, pathTextBox));

            // 如果 _textBoxes 为空或文件内容为空，确保至少有一行空的输入框
            if (_textBoxes.Count == 0)
            {
                AddPathTextBox(); // 添加一行空的输入框
            }
        }
    }
}
