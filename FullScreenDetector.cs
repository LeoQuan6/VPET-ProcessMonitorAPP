using LinePutScript.Localization.WPF;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using static ProcessMonitorAPP.FullScreenDetector;

namespace ProcessMonitorAPP
{
    public class FullScreenDetector
    {
        /// <summary>
        /// 获取窗口标题
        /// </summary>
        /// <param name="hWnd">窗口句柄</param>
        /// <param name="lpString">用于存放窗口标题的 StringBuilder</param>
        /// <param name="nMaxCount">StringBuilder 的容量</param>
        /// <returns>窗口标题的字符数（不包括终止符）</returns>
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        /// <summary>
        /// 获取前台窗口句柄
        /// </summary>
        /// <returns>当前前台窗口的句柄</returns>
        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        /// <summary>
        /// 获取窗口的屏幕坐标矩形
        /// </summary>
        /// <param name="hWnd">窗口句柄</param>
        /// <param name="lpRect">存放窗口矩形信息的 RECT 结构</param>
        /// <returns>如果成功，返回 true</returns>
        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        /// <summary>
        /// 根据指定的点，获取对应显示器的句柄
        /// </summary>
        /// <param name="pt">屏幕上的一个点</param>
        /// <param name="dwFlags">查找策略的标志（例如 MONITOR_DEFAULTTONULL、MONITOR_DEFAULTTOPRIMARY）</param>
        /// <returns>显示器的句柄，如果未找到符合条件的显示器，则可能返回 IntPtr.Zero</returns>
        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        /// <summary>
        /// 获取显示器信息
        /// </summary>
        /// <param name="hMonitor">显示器句柄</param>
        /// <param name="lpmi">存放显示器信息的 MONITORINFO 结构</param>
        /// <returns>如果成功，返回 true</returns>
        [DllImport("user32.dll")]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        /// <summary>
        /// 设置窗口的位置和尺寸
        /// </summary>
        /// <param name="hWnd">窗口句柄</param>
        /// <param name="hWndInsertAfter">窗口 Z 顺序的插入位置句柄</param>
        /// <param name="X">新位置的 X 坐标</param>
        /// <param name="Y">新位置的 Y 坐标</param>
        /// <param name="cx">窗口的新宽度</param>
        /// <param name="cy">窗口的新高度</param>
        /// <param name="uFlags">控制窗口调整行为的标志</param>
        /// <returns>如果成功，返回 true</returns>
        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        /// <summary>
        /// 获取窗口类名
        /// </summary>
        /// <param name="hWnd">窗口句柄</param>
        /// <param name="className">存放类名的 StringBuilder</param>
        /// <param name="maxCount">StringBuilder 的容量</param>
        /// <returns>窗口类名的字符数（不包括终止符）</returns>
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder className, int maxCount);

        /// <summary>
        /// 获取窗口的客户区矩形
        /// </summary>
        /// <param name="hWnd">窗口句柄</param>
        /// <param name="lpRect">存放客户区矩形信息的 RECT 结构</param>
        /// <returns>如果成功，返回 true</returns>
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        /// <summary>
        /// 获取与指定窗口相关联的线程和进程 ID
        /// </summary>
        /// <param name="hWnd">窗口句柄</param>
        /// <param name="lpdwProcessId">接收窗口相关联的进程 ID</param>
        /// <returns>返回窗口所在的线程 ID</returns>
        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

        /// <summary>
        /// 获取窗口的位置、大小和显示状态
        /// </summary>
        /// <param name="hWnd">窗口句柄</param>
        /// <param name="lpwndpl">接收窗口状态和位置的 `WINDOWPLACEMENT` 结构体</param>
        /// <returns>如果成功，返回非零值；如果失败，返回零</returns>
        [DllImport("user32.dll")]
        public static extern int GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        // 定义常量
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_SHOWWINDOW = 0x0040;


        /// <summary>
        /// 判断窗口是否全屏
        /// </summary>
        /// <param name="windowInfo">窗口信息</param>
        /// <returns>该窗口为全屏 返回true
        /// <para>该窗口不是全屏 返回false</para>
        /// </returns>
        /// <remarks>包含对Steam大屏模式的特殊判断</remarks>
        public static bool IsWindowFullScreen(WindowInfo windowInfo)
        {
            const int tolerance = 10;

            if (Math.Abs(windowInfo.WindowFactHeightAndWidth.Width - windowInfo.ScreenHeightAndWidth.Width) <= tolerance &&
                Math.Abs(windowInfo.WindowFactHeightAndWidth.Height - windowInfo.ScreenHeightAndWidth.Height) == 0)
            {
                return true;
            }
            else if (IsSteamBigPictureMode(windowInfo))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 判断前台窗口是否为全屏
        /// <para>并异步控制前台窗口覆盖桌宠的操作</para>
        /// </summary>
        /// <returns>前台窗口是全屏 返回true
        /// <para>前台窗口不是全屏 返回false</para>
        /// </returns>
        public static async Task<bool> GetFullScreenWindowCount()
        {
            bool fullScreen = false;
            IntPtr foreground_hWnd = GetForegroundWindow();
            WindowInfo windowinfo = GetWindowInfo(foreground_hWnd);
            if (
                IsWindowFullScreen(windowinfo) &&
                !IsSpecialProgramWindow(windowinfo) &&
                IsWinEtoExplorer(windowinfo)
                )
            {
                fullScreen = true;
            }

            // 返回之前进行异步延迟操作
            await Task.Delay(500); // 延迟0.5秒

            if (fullScreen && foreground_hWnd != IntPtr.Zero)
            {
                // 先置顶全屏窗口
                SetWindowPos(foreground_hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
            }
            if (fullScreen && foreground_hWnd != IntPtr.Zero)
            {
                // 再取消全屏窗口的置顶, 以达到覆盖桌宠但不永远覆盖
                SetWindowPos(foreground_hWnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
            }

            return fullScreen;
        }

        /// <summary>
        /// 获取当前窗口所在屏幕的屏幕大小
        /// </summary>
        /// <param name="windowRect">窗口位置, 用于定位窗口坐标</param>
        /// <param name="WindowPoint">对外提供窗口左上角坐标位置</param>
        /// <returns>屏幕的高度与宽度, 若未找到, 则返回0值的宽高</returns>
        public static HeightAndWidth GetForegroundWindowMonitor(RECT windowRect, out POINT WindowPoint)
        {
            HeightAndWidth monitorhw = new();

            // 获取窗口左上角的坐标
            WindowPoint = new POINT { X = windowRect.Left, Y = windowRect.Top };
            // 使用 MonitorFromPoint 来获取显示器句柄
            IntPtr hMonitor = MonitorFromPoint(WindowPoint, 2);
            if (hMonitor != IntPtr.Zero)
            {
                // 获取显示器信息
                MONITORINFO monitorInfo = new();
                monitorInfo.cbSize = Marshal.SizeOf(typeof(MONITORINFO));

                if (GetMonitorInfo(hMonitor, ref monitorInfo))
                {
                    monitorhw.Width = monitorInfo.rcMonitor.Right - monitorInfo.rcMonitor.Left;
                    monitorhw.Height = monitorInfo.rcMonitor.Bottom - monitorInfo.rcMonitor.Top;
                    return monitorhw;
                }
            }
            // 如果是单个显示器的情况下, 最大化但非全屏窗口的左上角坐标值会是负数, 进而导致返回(0, 0), 或许需要通过真正的多显示器来进行测试
            return monitorhw;
        }

        /// <summary>
        /// 判断是否为暂时无法处理的特殊程序的特殊窗口
        /// </summary>
        /// <param name="windowInfo">窗口信息</param>
        /// <returns>是特殊程序 返回true
        /// <para>不是特殊程序 返回false</para>
        /// </returns>
        /// <remarks>包含 Alt+Tab 呼出的任务切换窗口 和 火绒的"安全分析工具"</remarks>
        private static bool IsSpecialProgramWindow(WindowInfo windowInfo)
        {
            if (windowInfo.ClassName == "XamlExplorerHostIslandWindow" && windowInfo.ProcessName == "explorer")
            {
                return true; // 当前窗口是任务切换窗口, 不予理睬
            }
            else if (windowInfo.ClassName == "HRSWORD" && windowInfo.ProcessName == "SecAnalysis")
            {
                return true; // 当前窗口是火绒的"安全分析工具", 即使处于最大化, 也会被误识别为全屏, 以此方式暂时解决问题
            }
            return false;
        }

        /// <summary>
        /// 判断是否为普通资源管理器窗口
        /// </summary>
        /// <param name="windowInfo">窗口信息</param>
        /// <returns>是资源管理器或其他程序的窗口 返回true
        /// <para>独显直连情况下的桌面虚拟窗口 返回false</para>
        /// </returns>
        /// <remarks>如果是, 则正常识别是否全屏
        /// <para>如果不是, 则判断为独显直连情况下的桌面虚拟窗口, 不为全屏</para>
        /// <para>但不影响其他进程的窗口判断</para>
        /// </remarks>
        private static bool IsWinEtoExplorer(WindowInfo windowInfo)
        {
            if (windowInfo.ProcessName == "explorer")
            {
                if (windowInfo.ClassName == "CabinetWClass")
                {
                    return true; // 当前窗口是Win + E呼出的资源管理器窗口, 该窗口可以全屏, 应当正常识别
                }
                else if (windowInfo.WindowState == 1)
                {
                    return false;  // 该窗口应该是独显直连情况下的桌面虚拟窗口, 不应被识别为全屏
                }
            }
            return true;  // 应该是其他进程的窗口, 为了不影响正常判断, 默认返回true
        }

        /// <summary>
        /// 判断是否为 Steam 大屏模式
        /// </summary>
        /// <param name="windowInfo">窗口信息</param>
        /// <returns>大屏模式则必定全屏 返回true
        /// <para>如果不是Steam大屏模式或其他程序 返回false</para>
        private static bool IsSteamBigPictureMode(WindowInfo windowInfo)
        {
            if (
                windowInfo.ClassName == "SDL_app" &&
                windowInfo.ProcessName == "steamwebhelper" &&
                windowInfo.WindowState == 1 &&
                Math.Abs(windowInfo.WindowFactHeightAndWidth.Width - windowInfo.ScreenHeightAndWidth.Width) == 0 &&
                Math.Abs(windowInfo.WindowFactHeightAndWidth.Height - windowInfo.ScreenHeightAndWidth.Height) <= 2 &&
                // 其实以目前我的测试来看高度应该只比正常显示器大小小1像素, 但可惜我没有其他比例的屏幕以及4k的屏幕, 因此放宽到2像素的误差
                (windowInfo.WindowFactHeightAndWidth.Height < windowInfo.ScreenHeightAndWidth.Height)
                // 如此多的条件是为了保证是大屏幕模式而不是非最大化状态下的普通窗口
                )
            {
                return true; // 当前窗口是steam大屏幕模式, 由于steam自身原因造成大屏模式的窗口大小会比全屏小, 以此方式暂时解决问题
            }

            return false;
        }

        /// <summary>
        /// 获取窗口常用信息
        /// </summary>
        /// <param name="hWnd">窗口句柄</param>
        /// <returns>WindowInfo 结构的窗口信息</returns>
        public static WindowInfo GetWindowInfo(IntPtr hWnd)
        {
            WindowInfo windowInfo = new();

            windowInfo.hWnd = hWnd;

            GetWindowRect(hWnd, out RECT windowRect);
            windowInfo.WindowRect = windowRect;
            windowInfo.WindowRectHeightAndWidth.Width = windowRect.Right - windowRect.Left;
            windowInfo.WindowRectHeightAndWidth.Height = windowRect.Bottom - windowRect.Top;

            GetClientRect(hWnd, out RECT WindowFactRect);
            windowInfo.WindowFactRect = WindowFactRect;
            windowInfo.WindowFactHeightAndWidth.Width = WindowFactRect.Right - WindowFactRect.Left;
            windowInfo.WindowFactHeightAndWidth.Height = WindowFactRect.Bottom - WindowFactRect.Top;

            windowInfo.ScreenHeightAndWidth = GetForegroundWindowMonitor(windowRect, out windowInfo.WindowPoint);

            windowInfo.ClassName = GetWindowClassName(hWnd);

            windowInfo.ProcessName = GetWindowProcessInfo(hWnd);

            windowInfo.WindowState = GetWindowState(hWnd);

            windowInfo.Title = GetWindowTitle(hWnd);

            return windowInfo;
        }

        /// <summary>
        /// 获取窗口所属进程的辅助方法
        /// </summary>
        /// <param name="hWnd">窗口句柄</param>
        /// <returns>窗口进程名称</returns>
        private static string GetWindowProcessInfo(IntPtr hWnd)
        {
            int processId;
            GetWindowThreadProcessId(hWnd, out processId);
            Process process = Process.GetProcessById(processId);
            return process.ProcessName;
        }

        /// <summary>
        /// 获取窗口类名的辅助方法
        /// </summary>
        /// <param name="hWnd">窗口句柄</param>
        /// <returns>窗口类名 string</returns>
        private static string GetWindowClassName(IntPtr hWnd)
        {
            StringBuilder className = new StringBuilder(256);
            _ = GetClassName(hWnd, className, className.Capacity);
            return className.ToString();
        }

        /// <summary>
        /// 获取窗口标题的辅助方法
        /// </summary>
        /// <param name="hWnd">窗口句柄</param>
        /// <returns>窗口标题 string</returns>
        private static string GetWindowTitle(IntPtr hWnd)
        {
            StringBuilder title = new StringBuilder(256);
            _ = GetWindowText(hWnd, title, title.Capacity);
            return title.ToString();
        }

        /// <summary>
        /// 获取窗口显示状态的辅助方法
        /// </summary>
        /// <param name="hWnd">窗口句柄</param>
        /// <returns>
        /// 返回1 则为正常显示
        /// <para>返回2 则为最小化</para>
        /// 返回3 则为最大化
        /// <para>返回0 则为其他异常情况</para>
        /// </returns>
        private static int GetWindowState(IntPtr hWnd)
        {
            WINDOWPLACEMENT placement = new();
            _ = GetWindowPlacement(hWnd, ref placement);
            switch (placement.showCmd)
            {
                case 1:
                    return 1;  // "正常显示"
                case 3:
                    return 3;  // "最大化"
                case 2:
                    return 2;  // "最小化"
                default:
                    return 0; //  "其他异常可能性"
            }
        }

        /// <summary>
        /// 定义一个点坐标的结构体
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        /// <summary>
        /// 定义显示器信息结构体
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        /// <summary>
        /// 通用窗口或屏幕 矩形结构体
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        /// <summary>
        /// 表示一个窗口的位置信息和显示状态，包括最小化、最大化、常规状态下的窗口位置、尺寸以及显示方式。
        /// </summary>
        /// <remarks>
        /// 该结构体通常用于获取或设置窗口的位置、大小、状态以及窗口的显示命令（例如最大化、最小化等）。它是 Windows API 中用于窗口管理的结构体。
        /// </remarks>
        [StructLayout(LayoutKind.Sequential)]
        public struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public int showCmd;
            public POINT ptMinPosition;
            public POINT ptMaxPosition;
            public RECT rcNormalPosition;
        }

        /// <summary>
        /// 用于定义一个矩形(窗口或显示器)的宽高
        /// </summary>
        public struct HeightAndWidth
        {
            public int Height;
            public int Width;
        }

        /// <summary>
        /// 要用到的关于窗口的全部信息
        /// </summary>
        public struct WindowInfo
        {
            /// <summary>
            /// 窗口句柄
            /// </summary>
            public IntPtr hWnd;

            /// <summary>
            /// 窗口实际宽高
            /// </summary>
            public HeightAndWidth WindowFactHeightAndWidth;

            /// <summary>
            /// 窗口工作区域信息
            /// </summary>
            public RECT WindowFactRect;

            /// <summary>
            /// 窗口完整宽高
            /// </summary>
            public HeightAndWidth WindowRectHeightAndWidth;

            /// <summary>
            /// 窗口完整区域信息
            /// </summary>
            public RECT WindowRect;

            /// <summary>
            /// 窗口左上角坐标
            /// </summary>
            public POINT WindowPoint;

            /// <summary>
            /// 窗口所在屏幕的分辨率
            /// </summary>
            public HeightAndWidth ScreenHeightAndWidth;

            /// <summary>
            /// 窗口标题
            /// </summary>
            public string Title;

            /// <summary>
            /// 窗口类名
            /// </summary>
            public string ClassName;

            /// <summary>
            /// 窗口对应进程名称
            /// </summary>
            public string ProcessName;

            /// <summary>
            /// 窗口显示状态
            /// </summary>
            public int WindowState;
        }
    }
}
