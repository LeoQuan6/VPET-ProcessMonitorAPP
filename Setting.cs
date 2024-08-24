using LinePutScript;
using LinePutScript.Converter;

namespace ProcessMonitorAPP
{
    public class Setting : Line
    {
        public Setting(ILine line) : base(line)
        {
        }
        public Setting()
        {
        }
        internal Setting Set { get; set; }
        /// <summary>
        /// 启用ProcessMonitorApp
        /// </summary>
        [Line]
        public bool Enable { get; set; } = true;
    }
}
