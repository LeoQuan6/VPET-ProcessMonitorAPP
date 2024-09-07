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
using System.Collections.Concurrent;
using System.Windows.Shapes;

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
        private ConcurrentDictionary<string, bool> _runningProcesses = new ConcurrentDictionary<string, bool>(); // 记录所有被监控的进程的启动状态
        public string modPath;  // 用来存储mod路径的公共变量
        public string txtfilePath;  // 用来存储process_paths.txt的路径的公共变量

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
            modPath = LoaddllPath("ProcessMonitorAPP"); // 初始化mod路径
            txtfilePath = System.IO.Path.Combine(modPath, "process_paths.txt"); // 使用modPath变量
            if (!File.Exists(txtfilePath))
            {
                // MessageBox.Show("配置文件不存在，监控不会启动。");
                return;
            }

            LoadAndMonitorProcesses();
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
                // 确保文件存在，如果不存在则创建一个空文件
                if (!File.Exists(txtfilePath))
                {
                    File.Create(txtfilePath).Dispose(); // 创建并立即释放文件以避免锁定
                }
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

        /// <summary>
        /// 启动桌宠时 加载保存的文件中的路径并进行监控
        /// </summary>
        public void LoadAndMonitorProcesses()
        {
            _cancellationTokenSource.Cancel(); // 取消所有监控任务线程
            _runningProcesses.Clear(); // 清除字典中的所有条目
            _cancellationTokenSource = new CancellationTokenSource(); // 重新初始化监控任务线程
            List<string> missingFiles = new List<string>(); // 如果读取文件中有失效的路径, 记录在此处

            var lines = File.ReadAllLines(txtfilePath);

            foreach (var line in lines)
            {
                var parts = line.Split(new[] { '|' }, 2);
                if (parts.Length == 2)
                {
                    _runningProcesses[parts[1]] = false; // 添加到字典并初始化为false 
                }
            }
            foreach (var processPath in _runningProcesses.Keys.ToList())
            {
                if (File.Exists(processPath))
                {
                    StartMonitoring(processPath, false);
                }
                else
                {
                    missingFiles.Add(processPath);
                }
            }

            if (missingFiles.Count > 0)
            {
                string missingMessage = "以下程序不存在:\n" + string.Join("\n", missingFiles);
                MessageBox.Show(missingMessage);
            }
        }

        /// <summary>
        /// 重新加载监控程序
        /// </summary>
        public void ReloadAndMonitorProcesses()
        {
            _cancellationTokenSource.Cancel();
            // 等待并清理现有监控任务
            foreach (var task in _monitorTasks)
            {
                try
                {
                    task.Wait();
                }
                catch (AggregateException)
                {
                    // 忽略任务取消引发的异常
                }
            }

            _monitorTasks.Clear();

            List<string> missingFiles = new List<string>(); // 如果读取文件中有失效的路径, 记录在此处


            var lines = File.ReadAllLines(txtfilePath);
            List<string> newPaths = new List<string>();
            foreach (var line in lines)
            {
                var parts = line.Split(new[] { '|' }, 2);
                if (parts.Length == 2)
                {
                    newPaths.Add(parts[1]);
                }
            }

            // 对比现有的键，并统计运行中的进程
            List<string> runningProcesses = new List<string>();
            foreach (var key in _runningProcesses.Keys)
            {
                if (_runningProcesses[key] && newPaths.Contains(key))
                {
                    runningProcesses.Add(key);
                }
            }
            if (runningProcesses.Count == 0)
            {
                ToggleTopMost(true);
            }
            else
            {
                ToggleTopMost(false);
            }
            // 重置字典
            _runningProcesses.Clear();
            // 添加所有读取到的路径到字典，初始化值为false
            foreach (var path in newPaths)
            {
                _runningProcesses[path] = false;
            }
            // 如果存在正在运行的进程，更新其状态为true
            foreach (var runningPath in runningProcesses)
            {
                if (_runningProcesses.ContainsKey(runningPath))
                {
                    _runningProcesses[runningPath] = true;
                }
            }
            _cancellationTokenSource = new CancellationTokenSource(); // 重新初始化取消标记源

            // 重新启动监控
            foreach (var processpath in _runningProcesses.Keys.ToList())
            {
                if (File.Exists(processpath))
                {
                    bool initialState = runningProcesses.Contains(processpath); // 判断是否是之前正在运行的进程
                    StartMonitoring(processpath, initialState);
                }
                else
                {
                    missingFiles.Add(processpath);
                }
            }
            if (missingFiles.Count > 0)
            {
                string missingMessage = "以下程序不存在:\n" + string.Join("\n", missingFiles);
                MessageBox.Show(missingMessage);
            }
        }


        private void StartMonitoring(string processPath, bool initialState)
        {
            var token = _cancellationTokenSource.Token;
            var monitorTask = Task.Run(() => MonitorProcess(processPath, token, initialState), token);
            _monitorTasks.Add(monitorTask);
        }


        private void MonitorProcess(string processPath, CancellationToken token, bool wasRunning)
        {
            string processName = System.IO.Path.GetFileNameWithoutExtension(processPath);

            while (!token.IsCancellationRequested)
            {
                try
                {
                    bool isRunning = IsProcessRunning(processName, processPath);

                    if (isRunning && !wasRunning)
                    {
                        OnProcessStarted(processPath);
                    }
                    else if (!isRunning && wasRunning)
                    {
                        OnProcessStopped(processPath);
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
        // private int runningProcesses = 0;
        protected virtual async void OnProcessStarted(string processPath)
        {
            var mw = MW as MainWindow;
            if (mw != null)
            {
                Application.Current.Dispatcher.Invoke(() => {
                    _runningProcesses[processPath] = true;  // 更新字典值
                    if (_runningProcesses.Count(p => p.Value) > 0) // 检查是否有任何运行中的进程
                    {
                        ToggleTopMost(false);
                    }
                });

                await Task.Delay(500);  // 异步等待500毫秒
                Application.Current.Dispatcher.Invoke(() => {
                    if (_runningProcesses.Count(p => p.Value) > 0) // 检查是否有任何运行中的进程
                    {
                        ToggleTopMost(false); // 再次确认取消置顶
                    }
                });

                await Task.Delay(500);  // 异步等待500毫秒
                Application.Current.Dispatcher.Invoke(() => {
                    if (_runningProcesses.Count(p => p.Value) > 0) // 检查是否有任何运行中的进程
                    {
                        ToggleTopMost(false); // 再次确认取消置顶
                    }
                });
            }
        }

        protected virtual async void OnProcessStopped(string processPath)
        {
            var mw = MW as MainWindow;
            if (mw != null)
            {
                Application.Current.Dispatcher.Invoke(() => {
                    _runningProcesses[processPath] = false;  // 更新字典值
                    if (_runningProcesses.Count(p => p.Value) == 0)
                    {
                        ToggleTopMost(true); // 尝试恢复置顶
                    }
                });

                await Task.Delay(500);  // 异步等待500毫秒
                if (_runningProcesses.Count(p => p.Value) == 0)
                {
                    Application.Current.Dispatcher.Invoke(() => {
                        ToggleTopMost(true); // 再次确认置顶
                    });
                }

                await Task.Delay(500);  // 异步等待500毫秒
                if (_runningProcesses.Count(p => p.Value) == 0)
                {
                    Application.Current.Dispatcher.Invoke(() => {
                        ToggleTopMost(true); // 再次确认置顶
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
            string logFilePath = System.IO.Path.Combine(modPath, "ErrorLogs.txt"); // 创建日志文件的完整路径

            // 检查目录是否存在，如果不存在，则创建
            if (!Directory.Exists(modPath))
            {
                Directory.CreateDirectory(modPath);
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
