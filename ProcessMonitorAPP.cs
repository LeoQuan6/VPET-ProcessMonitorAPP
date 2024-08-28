using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using LinePutScript;
using LinePutScript.Localization.WPF;
using Panuon.WPF.UI;
using VPet_Simulator.Core;
using VPet_Simulator.Windows.Interface;
using LinePutScript.Converter;
using VPet_Simulator.Windows;
using System.ComponentModel;
using System.Windows.Documents;

namespace ProcessMonitorAPP
{
    public class ProcessMonitor : MainPlugin
    {
        public Setting Set;
        public override string PluginName => "ProcessMonitorAPP";
        public ProcessMonitor(IMainWindow mainwin) : base(mainwin)
        {
        }
        // 使用字段初始化
        private List<(string programName, string processPath)> _processPaths = new List<(string programName, string processPath)>();
        private List<Task> _monitorTasks = new List<Task>();
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private bool _isMonitoring;


        /// <summary>
        /// 当桌宠开启，加载mod的时候会调用这个函数
        /// </summary>
        public override void LoadPlugin()
        {
            MenuItem modset = MW.Main.ToolBar.MenuMODConfig;
            modset.Visibility = Visibility.Visible;
            var menuItem = new MenuItem()
            {
                Header = "取消置顶",
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            menuItem.Click += (s, e) => { Setting(); };
            modset.Items.Add(menuItem);

            // 初始化 Set
            Set = new Setting(MW.Set["取消置顶"]);
            if (!Set.Enable)
            {
                MessageBox.Show("监控并未正常启动");
                return;
            }

            _isMonitoring = true;
            ReloadAndMonitorProcesses();

            // MessageBox.Show("Monitoring started.");
        }

        /// <summary>
        /// 添加自定义设置
        /// </summary>
        public override void LoadDIY()
        {
            MW.Main.ToolBar.AddMenuButton(VPet_Simulator.Core.ToolBar.MenuType.DIY, "取消置顶", Setting);
        }
        /// <summary>
        /// 生成winSetting对话框资源
        /// </summary>
        public winSetting winSetting;
        /// <summary>
        /// 生成/显示winSetting对话框
        /// </summary>
        public override void Setting()
        {
            if (winSetting == null)
            {
                winSetting = new winSetting(this);
                winSetting.Closed += (sender, e) => winSetting = null; // 确保在窗口关闭时将实例设置为 null
                winSetting.Show();
            }
            else
            {
                winSetting.Topmost = true;  // 确保窗口在最顶部
                winSetting.Activate();      // 激活窗口，确保用户能看到
                if (winSetting.WindowState == WindowState.Minimized)
                    winSetting.WindowState = WindowState.Normal;  // 如果窗口被最小化，恢复窗口
                winSetting.Visibility = Visibility.Visible;       // 确保窗口是可见的
            }
        }

        public void ReloadAndMonitorProcesses()
        {
            if (_processPaths == null) _processPaths = new List<(string, string)>();
            _cancellationTokenSource.Cancel();
            _monitorTasks.ForEach(task => task.Wait());
            _monitorTasks.Clear();
            _cancellationTokenSource = new CancellationTokenSource();

            _processPaths.Clear();

            try
            {
                var PathSave = LoaddllPath("ProcessMonitorAPP");
                string filePath = Path.Combine(PathSave, "process_paths.txt");

                if (!File.Exists(filePath))
                {
                    // 文件不存在，创建一个空的
                    File.Create(filePath).Dispose();
                }

                var lines = File.ReadAllLines(filePath);

                foreach (var line in lines)
                {
                    var parts = line.Split(new[] { '|' }, 2);
                    if (parts.Length == 2)
                    {
                        _processPaths.Add((parts[0], parts[1]));
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"读取文件错误: {ex.Message}");
                return;
            }

            foreach (var (programName, processPath) in _processPaths)
            {
                if (File.Exists(processPath))
                {
                    StartMonitoring(processPath);
                }
                else
                {
                    MessageBox.Show($"以下文件不存在: {processPath}");
                }
            }
        }


        private void StartMonitoring(string processPath)
        {
            var token = _cancellationTokenSource.Token;
            var monitorTask = Task.Run(() => MonitorProcess(processPath, token), token);
            _monitorTasks.Add(monitorTask);
        }


        private void MonitorProcess(string processPath, CancellationToken token)
        {
            string processName = Path.GetFileNameWithoutExtension(processPath);
            bool wasRunning = false;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    bool isRunning = IsProcessRunning(processName, processPath);

                    if (isRunning && !wasRunning)
                    {
                        OnProcessStarted(processName);
                    }
                    else if (!isRunning && wasRunning)
                    {
                        OnProcessStopped(processName);
                    }
                    
                    wasRunning = isRunning;
                }
                catch (System.ComponentModel.Win32Exception winEx)
                {
                    if (winEx.NativeErrorCode == 5)  // "拒绝访问"
                    {
                        continue;  // 忽略此错误，继续监控
                    }
                    else if (winEx.NativeErrorCode == 299) // "ERROR_PARTIAL_COPY"
                    {
                        continue; // 忽略部分复制错误，可能由进程状态变化引起
                    }
                    else
                    {
                        string error = $"Error monitoring process: {processName}. Win32 Error: {winEx.Message}";
                        MessageBox.Show(error);
                        LogErrorToFile(error);  // 记录错误到文件
                        break; // 对于其他严重错误，终止监控
                    }
                }
                catch (Exception ex)
                {
                    string error = $"Error monitoring process: {processName}. Exception: {ex.ToString()}";
                    MessageBox.Show(error);
                    LogErrorToFile(error);  // 记录错误到文件
                    break; // 对于非 Win32 错误，终止监控
                }
                Thread.Sleep(1000); // 每秒检测一次
            }
        }

        private bool IsProcessRunning(string processName, string processPath)
        {
            try
            {
                var processes = Process.GetProcessesByName(processName);
                foreach (var process in processes)
                {
                    try
                    {
                        // 快速检查进程是否已结束，如果已结束，则跳过
                        if (process.WaitForExit(0))
                        {
                            continue; // 如果进程已结束，跳过当前循环迭代
                        }

                        if (!process.HasExited && (process.MainModule?.FileName.Equals(processPath, StringComparison.OrdinalIgnoreCase) == true))
                        {
                            return true; // 进程正在运行且路径匹配
                        }
                    }
                    catch (System.ComponentModel.Win32Exception winEx)
                    {
                        // 特别处理拒绝访问错误，允许监控继续
                        if (winEx.NativeErrorCode == 5)  // ERROR_ACCESS_DENIED
                        {
                            return true;
                        }
                        else if (winEx.NativeErrorCode == 299) // "ERROR_PARTIAL_COPY"
                        {
                            continue; // 忽略部分复制错误，可能由进程状态变化引起
                        }
                        //对于其他错误
                        else
                        {
                            // 抛出到更高层处理
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 抛出到更高层处理
                throw new Exception($"Error checking process: {processName}", ex);
            }
            return false; // 默认返回进程不在运行
        }

        // 终止所有监控
        public void StopAllMonitoring()
        {
            // 发送取消信号给所有监控任务
            _cancellationTokenSource.Cancel();

            // 等待所有任务尽可能完成
            foreach (var task in _monitorTasks)
            {
                try
                {
                    task.Wait();
                }
                catch (AggregateException ex)
                {
                    // 忽略任务取消引发的异常
                }
            }

            // 清空任务列表
            _monitorTasks.Clear();

            // 重置 CancellationTokenSource 以便后续使用
            _cancellationTokenSource = new CancellationTokenSource();
        }
        /*
        * ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        * ------------------------------------------------------------------------------------------------------------------------
        */

        public void ToggleTopMost(bool topMost)
        {
            var setting = MW.Set;
            MW.Set.SetTopMost(topMost);
        }


        /*
         * ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
         * ------------------------------------------------------------------------------------------------------------------------
         */
        private int runningProcesses = 0;
        protected virtual async void OnProcessStarted(string processName)
        {
            var mw = MW as MainWindow;
            if (mw != null)
            {
                Application.Current.Dispatcher.Invoke(() => {
                    Interlocked.Increment(ref runningProcesses);  // 原子操作增加计数器
                    ToggleTopMost(false);
                    //mw.Topmost = false; // 程序启动时取消置顶
                });

                await Task.Delay(500);  // 异步等待500毫秒
                Application.Current.Dispatcher.Invoke(() => {
                    ToggleTopMost(false);
                    //mw.Topmost = false; // 再次确认取消置顶
                });

                await Task.Delay(500);  // 异步等待500毫秒
                Application.Current.Dispatcher.Invoke(() => {
                    ToggleTopMost(false);
                    //mw.Topmost = false; // 再次确认取消置顶
                });
            }
        }

        protected virtual async void OnProcessStopped(string processName)
        {
            var mw = MW as MainWindow;
            if (mw != null)
            {
                Application.Current.Dispatcher.Invoke(() => {
                    if (Interlocked.Decrement(ref runningProcesses) == 0)
                    {
                        ToggleTopMost(true);
                        //mw.Topmost = true; // 尝试恢复置顶
                    }
                });

                if (runningProcesses == 0)
                {
                    await Task.Delay(500);  // 异步等待500毫秒

                    Application.Current.Dispatcher.Invoke(() => {
                        ToggleTopMost(true);
                        //mw.Topmost = true; // 再次确认置顶
                    });
                }

                if (runningProcesses == 0)
                {
                    await Task.Delay(500);  // 异步等待500毫秒

                    Application.Current.Dispatcher.Invoke(() => {
                        ToggleTopMost(true);
                        //mw.Topmost = true; // 再次确认置顶
                    });
                }
            }
        }

        /*
         * ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
         * ------------------------------------------------------------------------------------------------------------------------
         */

        private void LogErrorToFile(string errorMessage)
        {
            // 使用与 process_paths.txt 相同的路径
            string logDirectory = LoaddllPath("ProcessMonitorAPP"); // 获取保存路径
            string logFilePath = Path.Combine(logDirectory, "ErrorLogs.txt"); // 创建日志文件的完整路径

            // 检查目录是否存在，如果不存在，则创建
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            // 将错误信息追加到日志文件
            using (StreamWriter sw = new StreamWriter(logFilePath, true)) // true 表示追加数据到文件
            {
                sw.WriteLine($"{DateTime.Now}: {errorMessage}");
            }
        }




        /// <summary>
        /// 获取mod路径（By白草）
        /// </summary>
        /// <param name="dll">mod对应dll的名字</param>
        /// <returns>mod对应路径,或者""(如果没找到的话)</returns>
        public string LoaddllPath(string dll)
        {
            Assembly[] loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (Assembly assembly in loadedAssemblies)
            {
                string assemblyName = assembly.GetName().Name;

                if (assemblyName == dll)
                {
                    string assemblyPath = assembly.Location;

                    string assemblyDirectory = System.IO.Path.GetDirectoryName(assemblyPath);

                    string parentDirectory = Directory.GetParent(assemblyDirectory).FullName;

                    return parentDirectory;
                }
            }
            return "";
        }
    }
}
