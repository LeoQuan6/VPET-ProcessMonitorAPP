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
using Shell32;  // 添加对Shell32的引用以解析快捷方式
using System.Collections.Generic;
using System.Windows.Media;

namespace ProcessMonitorAPP
{
    /// <summary>
    /// winSetting.xaml 的交互逻辑
    /// </summary>
    public partial class winSetting : Window
    {
        ProcessMonitor vts;
        private string modPath;  // 添加字段来存储mod路径
        private string txtfilePath;  // 用来存储process_paths.txt的路径的公共变量
        public winSetting(ProcessMonitor vts)
        {
            InitializeComponent();
            //Resources = Application.Current.Resources;
            this.vts = vts;
            modPath = vts.modPath;  // 从ProcessMonitor实例获取modPath
            txtfilePath = vts.txtfilePath;  // 从ProcessMonitor实例获取txtfilePath
            LoadPaths(); // 加载保存的路径
            SetupDragDrop();  // 设置拖放支持
        }
        
        private void Window_Closed(object sender, EventArgs e)
        {
            vts.winSetting = null;
        }

        private void SetupDragDrop()
        {
            this.AllowDrop = true;
            this.DragEnter += OnDragEnter;
            this.Drop += OnDrop;
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Link;
            else
                e.Effects = DragDropEffects.None;
        }

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
                /*else if (Path.GetExtension(file).ToLower() == ".url")
                {
                    path = ResolveURLFile(file);
                }*/

                if (!string.IsNullOrEmpty(path))
                {
                    // 直接添加路径到界面，不立即保存
                    AddPathTextBox(Path.GetFileNameWithoutExtension(path), path);
                }
            }
        }

        private string ResolveShortcut(string shortcutPath)
        {
            if (Path.GetExtension(shortcutPath).ToLower() == ".lnk")
            {
                Shell shell = new Shell();
                Folder folder = shell.NameSpace(Path.GetDirectoryName(shortcutPath));
                FolderItem folderItem = folder.ParseName(Path.GetFileName(shortcutPath));
                if (folderItem != null)
                {
                    Shell32.ShellLinkObject link = (Shell32.ShellLinkObject)folderItem.GetLink;
                    return link.Path;
                }
            }
            return null;
        }
        /*
        private string ResolveURLFile(string urlFilePath)
        {
            if (File.Exists(urlFilePath) && Path.GetExtension(urlFilePath).ToLower() == ".url")
            {
                var lines = File.ReadAllLines(urlFilePath);
                foreach (var line in lines)
                {
                    if (line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
                    {
                        return line.Substring(4);
                    }
                }
            }
            return null;
        }
        */
        private List<(TextBox nameTextBox, TextBox pathTextBox)> _textBoxes = new List<(TextBox, TextBox)>();
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
        }

        private void AddPathTextBox_Click(object sender, RoutedEventArgs e)
        {
            AddPathTextBox();
        }

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

            MessageBox.Show("路径保存成功", "Information", MessageBoxButton.OK, MessageBoxImage.Information);

            // 重新加载并启动监控程序
            vts.ReloadAndMonitorProcesses();
        }


        private void AddPathTextBox(string name = "", string text = "", bool isSavedPath = false)
        {
            StackPanel panel = new StackPanel { Orientation = Orientation.Horizontal };
            TextBox nameTextBox = new TextBox { Text = name, Width = 100, Margin = new Thickness(0, 10, 0, 0) };
            TextBox pathTextBox = new TextBox { Text = text, Width = 300, Margin = new Thickness(10, 10, 0, 0) };
            Button removeButton = new Button
            {
                Content = "移除",
                Margin = new Thickness(10, 10, 0, 0),
                Background = AddPath_Button.Background,
                // Background = (Brush)new BrushConverter().ConvertFrom("#FFADD7F9"),
                BorderBrush = AddPath_Button.BorderBrush,
                // BorderBrush = (Brush)new BrushConverter().ConvertFrom("#FF6BB1E9"),
                BorderThickness = new Thickness(2)
            };
            // 设置按钮为圆角
            pu: ButtonHelper.SetCornerRadius(removeButton, new CornerRadius(4));

            removeButton.Click += (s, e) => RemovePath(panel);

            panel.Children.Add(nameTextBox);
            panel.Children.Add(pathTextBox);
            panel.Children.Add(removeButton);

            InputPanel.Children.Add(panel);  // 确保新路径行添加在列表底部

            _textBoxes.Add((nameTextBox, pathTextBox));  // 将新文本框添加到跟踪列表中
        }



        private void RemovePath(StackPanel panel)
        {
            InputPanel.Children.Remove(panel);
            var nameTextBox = (TextBox)panel.Children[0];
            var pathTextBox = (TextBox)panel.Children[1];
            _textBoxes.Remove((nameTextBox, pathTextBox));
        }
    }
}
