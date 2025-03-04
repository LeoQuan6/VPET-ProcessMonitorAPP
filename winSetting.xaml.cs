﻿using System;
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
using static ProcessMonitorAPP.winSetting;
using System.Globalization;
using System.Xml.Linq;

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
        /// 存储一组文本框对 每对文本框用于输入和显示进程的名称和路径
        /// 该列表被用于动态管理界面上的进程配置输入字段 允许用户添加、编辑和删除进程监控条目
        /// </summary>
        private List<(TextBox nameTextBox, TextBox pathTextBox)> _textBoxes = new List<(TextBox, TextBox)>();
        /// <summary>
        /// 存储一组Grid控件 每个控件用于输入和显示进程的名称和路径以及"移除"按钮
        /// 该列表被用于动态管理界面上的进程配置输入字段 允许用户添加、编辑和删除进程监控条目
        /// </summary>
        private List<PathGridElements> _pathGridElements = new List <PathGridElements>();
        /// <summary>
        /// 添加字符串列表来收集失效路径列表
        /// </summary>
        private List<string> missingFiles = [];


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
                string? path = null;
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
        private string? ResolveShortcut(string shortcutPath)
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
        /// 从配置文件读取路径信息 并初始化对应的文本框
        /// </summary>
        private void LoadPaths()
        {
            if (File.Exists(txtfilePath))
            {
                var lines = File.ReadAllLines(txtfilePath);
                missingFiles.Clear();
                foreach (var line in lines)
                {
                    var parts = line.Split(new[] { '|' }, 2);
                    if (parts.Length == 2)
                    {
                        AddPathTextBox(parts[0], parts[1], true);
                    }
                    
                    if (!File.Exists(parts[1]))
                    {
                        missingFiles.Add($"{parts[1]}");
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
            List<PathGridElements> deletepathpanel = new List<PathGridElements>();
            missingFiles.Clear();
            foreach (PathGridElements pathpanel in _pathGridElements)
            {
                string name = pathpanel.NameTextBox.Text;
                string processPath = pathpanel.PathTextBox.Text.Trim(); // 移除路径两端的空白
                processPath = processPath.Trim('"');// 移除路径两端的引号

                // 路径不为空才进行保存等操作
                if (!string.IsNullOrWhiteSpace(processPath))
                {
                    string processpathName = Path.GetFileNameWithoutExtension(processPath);
                    if (processpathName == "VPet-Simulator.Windows" || processpathName == "VPet.Solution")
                    {
                        deletepathpanel.Add(pathpanel);
                        MessageBox.Show("请不要用该mod监测桌宠程序自身".Translate());
                        continue;
                    }
                    // 如果路径不为空且名称为空，则自动生成名称
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        name = Path.GetFileNameWithoutExtension(processPath);
                        pathpanel.NameTextBox.Text = name;  // 自动填写名称到界面上
                        pathpanel.PathTextBox.Text = processPath;  // 自动填写整理后的路径到界面上
                    }
                    
                    // 只有当文件存在时才添加到保存列表
                    if (File.Exists(processPath))
                    {
                        lines.Add($"{name}|{processPath}");
                    }
                    else
                    {
                        missingFiles.Add($"{processPath}");
                    }

                }
            }
            File.WriteAllLines(txtfilePath, lines);
            foreach (PathGridElements pathpanel in deletepathpanel)
            {
                RemovePath(pathpanel.Grid);
            }

            // 如果 _textBoxes 为空或文件内容为空，确保至少有一行空的输入框
            if (_textBoxes.Count == 0)
            {
                AddPathTextBox(); // 添加一行空的输入框
            }

            // 提示已失效路径
            if (missingFiles.Count > 0)
            {
                string missingMessage = "以下程序不存在:\r\n".Translate() + string.Join("\n", missingFiles) + "\r\n其余路径保存成功!".Translate();
                MessageBox.Show(missingMessage, "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                MessageBox.Show("路径保存成功!".Translate(), "Information", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            // 重新加载并启动监控程序
            vts.ReloadAndMonitorProcesses();
        }

        /// <summary>
        /// 动态添加一行路径输入区域到界面上 包括一个名称输入框、一个路径输入框和一个移除按钮
        /// </summary>
        /// <param name="name">路径条目的名称，默认为空字符串，显示在第一个文本框中。</param>
        /// <param name="path">路径的具体文本，默认为空字符串，显示在第二个文本框中。</param>
        /// <param name="isSavedPath">指示该路径是否是已保存路径，默认为 false，此参数目前未使用，可以用于将来的扩展。</param>
        private void AddPathTextBox(string name = "", string path = "", bool isSavedPath = false)
        {
            var elements = CreatePathGrid(name, path);
            _pathGridElements.Add(elements);

            // 动态添加新行
            InputGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            // 设置新增控件所在行
            Grid.SetRow(elements.Grid, InputGrid.RowDefinitions.Count-1);
            // 添加到输入面板
            InputGrid.Children.Add(elements.Grid);  // 确保新路径行添加在列表底部
            // 将文本框添加到跟踪列表中
            _textBoxes.Add((elements.NameTextBox, elements.PathTextBox));
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

        /// <summary>
        /// 保存其他设置
        /// 要在每次更改其他设置时添加 并增加保存对应设置
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
        /// <param name="grid">要移除的一行 Grid 控件</param>
        private void RemovePath(Grid grid)
        {
            InputGrid.Children.Remove(grid);

            var nameTextBox = (TextBox)grid.Children[0];
            var pathTextBox = (TextBox)grid.Children[1];
            _textBoxes.Remove((nameTextBox, pathTextBox));
            PathGridElements DeleteGrid = _pathGridElements.Find(x => x.Grid == grid);
            _pathGridElements.Remove(DeleteGrid);

            // 如果 _textBoxes 为空或文件内容为空，确保至少有一行空的输入框
            if (_textBoxes.Count == 0 && _pathGridElements.Count == 0)
            {
                AddPathTextBox(); // 添加一行空的输入框
            }
        }

        /// <summary>
        /// 窗口加载完成后要执行的操作
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 提示已失效路径
            if (missingFiles.Count > 0)
            {
                string missingMessage = "以下程序不存在:\r\n".Translate() + string.Join("\n", missingFiles);
                // 延迟执行 MessageBox，确保它在窗口加载完成后显示
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    MessageBox.Show(missingMessage, "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                
            }
        }

        /// <summary>
        /// 创建一个包含名称输入框 路径输入框和移除按钮的 Grid 控件
        /// 并将其封装为 PathGridElements 结构体返回
        /// </summary>
        /// <param name="name">名称输入框的文本</param>
        /// <param name="path">路径输入框的文本</param>
        /// <returns>包含 Grid 和其内部控件的 PathGridElements 结构体</returns>
        public PathGridElements CreatePathGrid(string name, string path)
        {
            var grid = new Grid
            {
                Margin = new Thickness(0, 10, 0, 0)
            };

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100)});
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300)});
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100)});

            var nameTextBox = new TextBox
            {
                Text = name,
                Margin = new Thickness(5, 5, 5, 5),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
            };
            Grid.SetColumn(nameTextBox, 0);

            var pathTextBox = new TextBox
            {
                Text = path,
                Margin = new Thickness(5, 5, 5, 5),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            Grid.SetColumn(pathTextBox, 1);

            var removeButton = new Button
            {
                Content = "移除".Translate(),
                Background = AddPath_Button.Background,
                BorderBrush = AddPath_Button.BorderBrush,
                BorderThickness = new Thickness(2),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            ButtonHelper.SetCornerRadius(removeButton, new CornerRadius(5));
            Grid.SetColumn(removeButton, 2);

            removeButton.Click += (s, e) => RemovePath(grid);

            // 添加控件到 Grid
            grid.Children.Add(nameTextBox);
            grid.Children.Add(pathTextBox);
            grid.Children.Add(removeButton);

            return new PathGridElements(grid, nameTextBox, pathTextBox, removeButton);
        }

        /// <summary>
        /// 用于封装动态创建的 Grid 及其内部控件的结构体
        /// 包含 Grid 本行的名称输入框 路径输入框 移除按钮
        /// </summary>
        /// <remarks>
        /// 初始化 PathGridElements 的构造函数
        /// </remarks>
        /// <param name="grid">包含所有控件的 Grid</param>
        /// <param name="nameTextBox">名称输入框</param>
        /// <param name="pathTextBox">路径输入框</param>
        /// <param name="removeButton">移除按钮</param>
        public struct PathGridElements(Grid grid, TextBox nameTextBox, TextBox pathTextBox, Button removeButton)
        {
            public Grid Grid { get; } = grid;
            public TextBox NameTextBox { get; } = nameTextBox;
            public TextBox PathTextBox { get; } = pathTextBox;
            public Button RemoveButton { get; } = removeButton;
        }
    }
}
