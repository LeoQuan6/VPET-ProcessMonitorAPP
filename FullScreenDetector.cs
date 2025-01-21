using LinePutScript.Localization.WPF;
using System;
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
        /// 枚举所有顶级窗口
        /// </summary>
        /// <param name="enumProc">回调方法，用于处理每个窗口</param>
        /// <param name="lParam">传递给回调方法的参数</param>
        /// <returns>如果枚举成功并且未中止，则返回 true</returns>
        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
        /// <summary>
        /// 回调方法的委托定义，用于 EnumWindows
        /// </summary>
        /// <param name="hWnd">当前枚举到的窗口句柄</param>
        /// <param name="lParam">传递给回调的额外参数</param>
        /// <returns>如果返回 true，继续枚举；否则停止枚举</returns>
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

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

        // 定义常量
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_SHOWWINDOW = 0x0040;

        /// <summary>
        /// 定义矩形结构体
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
        /// 检查窗口是否处于全屏状态
        /// </summary>
        /// <param name="hWnd">目标窗口句柄</param>
        /// <param name="windowRect">窗口区域(含任务栏等无用区域)</param>
        /// <param name="windowFactRect">窗口区域(客户使用区域)</param>
        /// <returns>该窗口为全屏则为true, 不是全屏则为false</returns>

        public static bool IsWindowFullScreen(IntPtr hWnd, out RECT windowRect, out RECT windowFactRect)
        {
            windowRect = new RECT();
            windowFactRect = new RECT();

            StringBuilder className = new StringBuilder(256);
            GetClassName(hWnd, className, className.Capacity);
            if (className.ToString() == "XamlExplorerHostIslandWindow")
            {
                return false;  // 若窗口是windows系统"任务切换"窗口, 不予理睬
            }

            if (GetWindowRect(hWnd, out windowRect) && GetClientRect(hWnd, out windowFactRect))
            {
                // 获取物理屏幕分辨率
                var (screenWidth, screenHeight) = GetForegroundWindowMonitor();

                int windowWidth = windowRect.Right - windowRect.Left;
                int windowHeight = windowRect.Bottom - windowRect.Top;

                int windowFactWidth = windowFactRect.Right - windowFactRect.Left;
                int windowFactHeight = windowFactRect.Bottom - windowFactRect.Top;

                const int tolerance = 10;

                if (Math.Abs(windowWidth - screenWidth) <= tolerance &&
                    Math.Abs(windowHeight - screenHeight) == 0)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 获取所有全屏窗口计数
        /// </summary>
        /// <returns></returns> 全屏窗口的数量int
        public static async Task<bool> GetFullScreenWindowCount()
        {
            bool fullScreen = false;
            IntPtr targetHWnd = IntPtr.Zero; // 用于保存检测到的全屏窗口句柄

            EnumWindows((hWnd, lParam) =>
            {
                StringBuilder title = new StringBuilder(256);
                GetWindowText(hWnd, title, title.Capacity);
                IntPtr foregroundHwnd = GetForegroundWindow();

                if (string.IsNullOrWhiteSpace(title.ToString())  // 无实际标题名称的窗口一般为无用窗口, 不监测
                    || hWnd != foregroundHwnd  // 后台窗口不监测
                    )
                {
                    return true;
                }

                RECT windowRect;
                RECT windowFactRect;
                if (IsWindowFullScreen(hWnd, out windowRect, out windowFactRect))
                {
                    fullScreen = true;
                    targetHWnd = hWnd;  // 保存句柄
                }
                return false;

            }, IntPtr.Zero);

            // 返回之前进行异步延迟操作
            await Task.Delay(500); // 延迟1秒

            if (fullScreen && targetHWnd != IntPtr.Zero)
            {
                // 先置顶全屏窗口
                SetWindowPos(targetHWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
            }
            // await Task.Delay(200); // 延迟0.2秒
            if (fullScreen && targetHWnd != IntPtr.Zero)
            {
                // 再取消全屏窗口的置顶, 以达到覆盖桌宠但不永远覆盖
                SetWindowPos(targetHWnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
            }

            return fullScreen;
        }

        /// <summary>
        /// 获取当前窗口所在屏幕的屏幕大小
        /// </summary>
        /// <returns> 正常返回(屏幕宽度, 屏幕高度)  若并未找到屏幕大小, 则返回(0,0)</returns>
        public static (int monitorWidth, int monitorHeight) GetForegroundWindowMonitor()
        {
            IntPtr hwnd = GetForegroundWindow();
            RECT windowRect;
            if (GetWindowRect(hwnd, out windowRect))
            {
                // 获取窗口左上角的坐标
                POINT windowPoint = new POINT { X = windowRect.Left, Y = windowRect.Top };
                // 使用 MonitorFromPoint 来获取显示器句柄
                IntPtr hMonitor = MonitorFromPoint(windowPoint, 2);
                if (hMonitor != IntPtr.Zero)
                {
                    // 获取显示器信息
                    MONITORINFO monitorInfo = new MONITORINFO();
                    monitorInfo.cbSize = Marshal.SizeOf(typeof(MONITORINFO));

                    if (GetMonitorInfo(hMonitor, ref monitorInfo))
                    {
                        var monitorWidth = monitorInfo.rcMonitor.Right - monitorInfo.rcMonitor.Left;
                        var monitorHeight = monitorInfo.rcMonitor.Bottom - monitorInfo.rcMonitor.Top;
                        return (monitorWidth, monitorHeight);
                    }
                }
                //或许需要通过真正的多显示器来进行测试
            }
            MessageBox.Show("无法获取到屏幕的物理分辨率, 请联系'取消置顶'的mod制作者 夜柠, 来反馈此bug\r\n请通过在steam中强制停止或通过windows系统托盘中的桌宠小标来关闭桌宠".Translate());
            return (0, 0);
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
    }
}
