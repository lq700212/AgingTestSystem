using System;
using System.Drawing;
using System.Windows.Forms;

namespace AgingTestSystem.Controls
{
    /// <summary>
    /// 内嵌在密码/机器码输入框右缘内侧的"眼睛"图标（自绘小控件 20x18）。
    /// 仿 HJVision mFormLicenseView.EyeIcon 同款做法（V1.86 授权窗机器码显隐）。
    ///
    /// 点击事件由宿主窗体挂接（Click += …）翻转明文/圆点；本控件只负责画和报态。
    /// 为什么自绘而不用 emoji/Unicode 字符：老工控机/精简字体下 emoji 会字体回退成
    /// 方块（HJVision V4.4.3 同坑），GDI+ 画线条在任何字体环境下都有保障。
    ///
    /// 两种状态（外部经 <see cref="Shown"/> 设置）：
    /// - Shown=false → 眼睛轮廓 + 空心瞳孔 + 斜线（当前是圆点隐藏，点我显示明文）；
    /// - Shown=true  → 眼睛轮廓 + 实心瞳孔（当前明文可见，点我藏回圆点）。
    ///
    /// 【宿主要做三件事】（宿主窗体构造里，参照 LicenseForm.ApplyEditState）
    /// ①把本控件 Add 进输入框.Controls（子控件天然浮在输入框上，不会被组件遮）；
    /// ②绑输入框 Resize 调 PositionEye 精确定位（输入框缩放/DPI 变化时眼睛跟右缘走）；
    /// ③BackColor 设成输入框底色（单行框文本让不出右边距，眼睛只能盖在文本右端，
    ///   底色一致看起来才像"嵌在框里"）。
    /// 不需要显式 Dispose 本控件：它是输入框的子控件，随输入框一起释放。
    /// </summary>
    public class EyeIcon : Control
    {
        /// <summary>当前是否明文可见（true=实心瞳孔，false=带斜线）。</summary>
        private bool _shown;

        /// <summary>当前显示态：true=明文（实心瞳孔）；false=掩码（空心+斜线）。</summary>
        public bool Shown
        {
            get { return _shown; }
            set { if (_shown != value) { _shown = value; Invalidate(); } }
        }

        public EyeIcon()
        {
            // 双缓冲防闪烁 + 不透明 BackColor 盖在文本右端（单行输入框文本让不出
            // 右边距，只能盖；底色已由宿主设为输入框底色，视觉无缝）。
            SetStyle(ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.UserPaint
                | ControlStyles.ResizeRedraw, true);
            Size = new Size(20, 18);
            Cursor = Cursors.Hand;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            // 眼睛外轮廓：横向椭圆，左右各留 2px、上下留 4px
            Rectangle eye = new Rectangle(2, 4, Width - 5, Height - 9);
            using (Pen pen = new Pen(Color.FromArgb(96, 96, 96), 1.5f))
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(96, 96, 96)))
            {
                g.DrawEllipse(pen, eye);
                // 瞳孔：眼睛中心的小圆，直径约轮廓短轴的一半
                int d = Math.Min(eye.Width, eye.Height) / 2;
                int cx = eye.X + eye.Width / 2, cy = eye.Y + eye.Height / 2;
                if (_shown)
                {
                    g.FillEllipse(brush, cx - d / 2, cy - d / 2, d, d);   // 可见=实心瞳孔
                }
                else
                {
                    g.DrawEllipse(pen, cx - d / 2, cy - d / 2, d, d);     // 隐藏=空心瞳孔
                    // 斜线（左上→右下）盖住眼睛表示"隐藏中"
                    using (Pen slash = new Pen(Color.FromArgb(96, 96, 96), 1.8f))
                        g.DrawLine(slash, eye.Left - 1, eye.Bottom + 1, eye.Right + 1, eye.Top - 1);
                }
            }
        }
    }
}