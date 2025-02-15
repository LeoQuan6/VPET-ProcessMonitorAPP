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
using ProcessMonitorAPP;
using System.Windows.Threading;  // 引入DispatcherTimer

namespace ProcessMonitorAPP
{
    public class ProcessMonitor : MainPlugin
    {
        public Setting? Set;
        public override string PluginName => "ProcessMonitorAPP";
        public ProcessMonitor(IMainWindow mainwin) : base(mainwin)
        {
        }
        //-------------- 使用字段初始化--------------
        /// <summary>
        /// 监控任务统计
        /// </summary>
        private List<Task> _monitorTasks = new List<Task>();
        /// <summary>
        /// 取消标记源
        /// </summary>
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        /// <summary>
        /// 记录所有被监控的进程及运行状态
        /// </summary>
        private ConcurrentDictionary<string, bool> _runningProcesses = new ConcurrentDictionary<string, bool>();
        /// <summary>
        /// 存储mod路径
        /// </summary>
        public string modPath { get; set; }
        /// <summary>
        /// 存储process_paths.txt的路径
        /// </summary>
        public string txtfilePath { get; set; }
        /// <summary>
        /// 存储monitor_set.txt的路径
        ///</summary>
        public string txtsetPath { get; set; }
        /// <summary>
        /// 保存是否启用全屏监控的状态
        /// </summary>
        public bool EnableFullScreenMonitor {  get; set; }
        /// <summary>
        /// 定义全屏窗口监测定时器
        /// </summary>
        private static DispatcherTimer? _FullScreenMonitorTimer;
        /// <summary>
        /// 定义程序监控定时器
        /// </summary>
        private DispatcherTimer? _ProcessMonitorTimer;
        /// <summary>
        /// 记录前台窗口是否为全屏
        /// </summary>
        /// <remarks>如果EnableFullScreenMonitor为false时, 该值应保持为false</remarks>
        public bool HasFullScreen { get; set; }
        /// <summary>
        /// 当桌宠开启，加载mod的时候会调用这个函数
        /// </summary>
        public override void LoadPlugin()
        {
            MenuItem modset = MW.Main.ToolBar.MenuMODConfig;
            modset.Visibility = Visibility.Visible;
            var menuItem = new MenuItem()
            {
                Header = "取消置顶".Translate(),
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            menuItem.Click += (s, e) => { Setting(); };
            modset.Items.Add(menuItem);

            // 初始化 Set
            Set = new Setting(MW.Set["取消置顶".Translate()]);
            if (!Set.Enable)
            {
                MessageBox.Show("监控并未正常启动".Translate());
                return;
            }

            modPath = LoaddllPath("ProcessMonitorAPP"); // 初始化mod路径
            string parentDirectory = Directory.GetParent(modPath).FullName;// 获取modPath的父目录
            string newFolder = System.IO.Path.Combine(parentDirectory, "取消置顶文件");// 创建名为"取消置顶文件"的新文件夹
            if (!Directory.Exists(newFolder))
            {
                Directory.CreateDirectory(newFolder);
            }
            txtfilePath = System.IO.Path.Combine(newFolder, "process_paths.txt");// 设置process_paths.txt的新路径
            txtsetPath = System.IO.Path.Combine(newFolder, "monitor_set.txt");// 设置monitor_set.txt的新路径
            if (!File.Exists(txtfilePath) | !File.Exists(txtsetPath))
            {
                // MessageBox.Show("配置文件不存在，监控不会启动。");
                return;
            }
            LoadOtherSettings();
            LoadAndMonitorProcesses();
            MonitorFullScreen();
            _ProcessMonitorTimer = new DispatcherTimer();
            _ProcessMonitorTimer.Interval = TimeSpan.FromSeconds(0.2);  // 每秒检查一次
            _ProcessMonitorTimer.Tick += ShouldToggleTopMost; // 每秒调用检查方法
            _ProcessMonitorTimer.Start();  // 启动程序监控定时器
        }

        /// <summary>
        /// 添加自定义设置
        /// </summary>
        public override void LoadDIY()
        {
            MW.Main.ToolBar.AddMenuButton(VPet_Simulator.Core.ToolBar.MenuType.DIY, "取消置顶".Translate(), Setting);
        }
        /// <summary>
        /// 生成winSetting对话框资源
        /// </summary>
        public winSetting? winSetting;
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
                if (!File.Exists(txtsetPath))
                {
                    File.Create(txtsetPath).Dispose(); // 创建并立即释放文件以避免锁定
                    DefaultOtherSettings();
                }
                winSetting = new winSetting(this);
                winSetting.Closed += (sender, e) => winSetting = null; // 确保在窗口关闭时将实例设置为 null
                winSetting.Show();
            }
            else
            {
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
            

            // 读取路径并初始化监控
            var lines = File.ReadAllLines(txtfilePath);
            foreach (var line in lines)
            {
                var parts = line.Split(new[] { '|' }, 2);
                if (parts.Length != 2) // 首先检查格式是否正确
                {
                    MessageBox.Show("配置文件错误, 请不要私自修改文件!\n如需修改, 请严格按照 '名称|路径' 的格式进行修改".Translate());
                    return; // 发现格式错误即退出方法
                }

                if (!File.Exists(parts[1])) // 检查文件是否存在
                {
                    continue; // 文件不存在则不进行监控, 等待用户打开设置窗口时才进行提示，并处理下一行
                }

                // 文件存在且格式正确时，添加到监控列表
                _runningProcesses[parts[1]] = false;
                StartMonitoring(parts[1], false);
            }
        }

        public void MonitorFullScreen()
        {
            if (EnableFullScreenMonitor)
            {
                if (_FullScreenMonitorTimer == null)
                {
                    _FullScreenMonitorTimer = new DispatcherTimer();
                    _FullScreenMonitorTimer.Interval = TimeSpan.FromSeconds(0.2);  // 每秒检查一次
                    _FullScreenMonitorTimer.Tick += CheckFullScreenStatus; // 每秒调用检查方法
                }
                HasFullScreen = false;
                StartTimerFullScreenMonitor();
            }
            else
            {
                HasFullScreen = false;
            }
        }

        /// <summary>
        /// 启动定时器_FullScreenMonitorTimer的静态方法
        /// </summary>
        public static void StartTimerFullScreenMonitor()
        {
            if (_FullScreenMonitorTimer != null && !_FullScreenMonitorTimer.IsEnabled)
            {
                _FullScreenMonitorTimer.Start();
            }
        }

        /// <summary>
        /// 停止定时器_FullScreenMonitorTimer的静态方法
        /// </summary>
        public static void StopTimerFullScreenMonitor()
        {
            if (_FullScreenMonitorTimer != null && _FullScreenMonitorTimer.IsEnabled)
            {
                _FullScreenMonitorTimer.Stop();
            }
        }


        /// <summary>
        /// 全屏窗口数量的更新
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void CheckFullScreenStatus(object? sender, EventArgs e)
        {
            HasFullScreen = await FullScreenDetector.GetFullScreenWindowCount();
        }

        /// <summary>
        /// 启动桌宠时 加载设置文件中的其他设置
        /// </summary>
        public void LoadOtherSettings()
        {
            var setlines = File.ReadAllLines(txtsetPath);
            if (setlines == null)
            {
                DefaultOtherSettings();
            }
            else
            {
                foreach (var line in setlines)
                {
                    var parts = line.Split(new[] { '|' }, 2);
                    if (parts.Length != 2) // 首先检查格式是否正确
                    {
                        MessageBox.Show("配置文件错误, 请不要私自修改文件!\n如需修改, 请严格按照 '设置名|bool' 的格式进行修改".Translate());
                        return; // 发现格式错误即退出方法
                    }
                    if (parts[0] == "FullScreenMonitorSetting")
                    {
                        if (parts[1] == "True")
                        {
                            EnableFullScreenMonitor = true;
                        }
                        else if (parts[1] == "False")
                        {
                            EnableFullScreenMonitor = false;
                        }
                        else
                        {
                            MessageBox.Show("配置文件错误, 请不要私自修改文件!\n如需修改, 请严格按照 '设置名|bool' 的格式进行修改".Translate());
                            return; // 发现格式错误即退出方法
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 将其他设置调整为默认值
        /// </summary>
        public void DefaultOtherSettings()
        {
            EnableFullScreenMonitor = false;
        }

        /// <summary>
        /// 重新加载监控程序
        /// </summary>
        public void ReloadAndMonitorProcesses()
        {
            // 取消现有的监控任务并清理任务列表
            _cancellationTokenSource.Cancel();
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
            // 读取所有新路径, 并将旧字典中旧路径的状态导入字典
            var lines = File.ReadAllLines(txtfilePath);
            var newPaths = new Dictionary<string, bool>();  // 新路径字典, 用于存储路径和初始监控状态
            foreach (var line in lines)
            {
                var parts = line.Split(new[] { '|' }, 2);
                if (parts.Length == 2)
                {
                    var wasRunning = _runningProcesses.TryGetValue(parts[1], out var running) && running;  // 只有既是旧路径且原值为true的情况下才返回true
                    newPaths[parts[1]] = wasRunning;  // 更新新字典, 并输入对应值
                }
            }
            _runningProcesses = new ConcurrentDictionary<string, bool>(newPaths); // 更新_runningProcesses为最新的路径集合
            _cancellationTokenSource = new CancellationTokenSource();  // 重新初始化取消标记源
            
            // 根据字典中的键值对, 重新启动监控, 并传入对应值
            foreach (var (processpath, wasRunning) in _runningProcesses)
            {
                if (File.Exists(processpath))
                {
                    StartMonitoring(processpath, wasRunning);
                }
            }
        }

        /// <summary>
        /// 对该进程开始监控运行状态
        /// </summary>
        /// <param name="processPath">监控的进程的路径</param>
        /// <param name="initialState">监控前 程序的运行状态</param>
        private void StartMonitoring(string processPath, bool initialState)
        {
            var token = _cancellationTokenSource.Token;
            var monitorTask = Task.Run(() => MonitorProcess(processPath, token, initialState), token);
            _monitorTasks.Add(monitorTask);
        }

        /// <summary>
        /// 监控指定路径的进程 并根据进程运行状态触发相应的事件
        /// 此方法将持续检查进程状态 直到接收到取消请求
        /// </summary>
        /// <param name="processPath">要监控的进程的完整文件路径</param>
        /// <param name="token">用于接收取消监控的信号的取消标记</param>
        /// <param name="wasRunning">监控开始时进程的运行状态 true表示进程已在运行 false表示进程未在运行。</param>
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
                    else if (winEx.NativeErrorCode == 299) // "部分复制错误"
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

        /// <summary>
        /// 检查指定名称和路径的进程是否正在运行
        /// </summary>
        /// <param name="processName">要检查的进程的名称</param>
        /// <param name="processPath">要检查的进程的完整路径</param>
        /// <returns>如果进程正在运行且路径匹配，则返回 true；否则返回 false</returns>
        /// <exception cref="System.ComponentModel.Win32Exception">访问进程信息时遇到问题，可能是权限不足或其他Windows API错误。</exception>
        /// <exception cref="Exception">处理进程信息时发生不可预见的异常。</exception>
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

        /// <summary>
        /// 终止所有监控
        /// </summary>
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
                catch (AggregateException)
                {
                    // 忽略任务取消引发的异常
                }
            }

            // 清空任务列表
            _monitorTasks.Clear();

            // 重置 CancellationTokenSource 以便后续使用
            _cancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// 判断是否需要置顶窗口
        /// </summary>
        private async void ShouldToggleTopMost(object? sender, EventArgs e)
        {
            if ((_runningProcesses.Count(p => p.Value) > 0) || HasFullScreen)
            {
                ToggleTopMost(false);
                await Task.Delay(200);  // 异步等待
                ToggleTopMost(false);
            }
            else if ((_runningProcesses.Count(p => p.Value) == 0) && !HasFullScreen)
            {
                ToggleTopMost(true);
                await Task.Delay(200);  // 异步等待
                ToggleTopMost(true);
            }
        }

        /// <summary>
        /// 设置桌宠窗口置顶状态
        /// </summary>
        /// <param name="topMost">true则置顶 false则取消置顶。</param>
        public void ToggleTopMost(bool topMost)
        {
            var setting = MW.Set;
            MW.Set.SetTopMost(topMost);
        }

        /// <summary>
        /// 当指定的进程启动时调用 判断是否需要进行取消置顶操作
        /// 并更新字典中对应进程的运行状态
        /// </summary>
        /// <param name="processPath">启动的进程的完整路径 用于更新字典中对应进程的运行状态</param>
        protected virtual void OnProcessStarted(string processPath)
        {
            var mw = MW as MainWindow;
            if (mw != null)
            {
                Application.Current.Dispatcher.Invoke(() => {
                    _runningProcesses[processPath] = true;  // 更新字典值
                });
            }
        }

        /// <summary>
        /// 当指定的进程关闭时调用 判断是否需要进行恢复置顶操作
        /// 并更新字典中对应进程的运行状态
        /// </summary>
        /// <param name="processPath">关闭的进程的完整路径 用于更新字典中对应进程的运行状态</param>
        protected virtual void OnProcessStopped(string processPath)
        {
            var mw = MW as MainWindow;
            if (mw != null)
            {
                Application.Current.Dispatcher.Invoke(() => {
                    _runningProcesses[processPath] = false;  // 更新字典值
                });
            }
        }

        /// <summary>
        /// 错误日志记录
        /// </summary>
        /// <param name="errorMessage"></param>
        public void LogErrorToFile(string errorMessage)
        {
            string parentDirectory = Directory.GetParent(modPath).FullName;// 获取modPath的父目录
            string newFolder = System.IO.Path.Combine(parentDirectory, "取消置顶文件");// 创建名为"取消置顶文件"的新文件夹
            if (!Directory.Exists(newFolder))
            {
                Directory.CreateDirectory(newFolder);
            }
            // 使用与 process_paths.txt 相同的路径
            string logFilePath = System.IO.Path.Combine(newFolder, "ErrorLogs.txt"); // 创建日志文件的完整路径

            // 检查目录是否存在，如果不存在，则创建
            if (!Directory.Exists(logFilePath))
            {
                Directory.CreateDirectory(logFilePath);
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
                string? assemblyName = assembly.GetName().Name;

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
