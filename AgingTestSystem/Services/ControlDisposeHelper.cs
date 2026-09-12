using System.Windows.Forms;

namespace AgingTestSystem.Services
{
    /// <summary>
    /// 动态控件容器释放助手（【V1.72.16 新增】修"点/拖节点后随机时刻终结器跨线程崩溃"）。
    ///
    /// 【为什么不能 foreach 直接 Dispose】
    /// WinForms 里 Control.Dispose() 会把自己从父容器的 Controls 集合中摘除
    /// （先 Dispose 再 Clear 的本意是对的，但枚举器正指着这个集合）：
    /// 释放第 0 个孩子后，后面的孩子整体前移一位，枚举器下标 +1 就跳过了一个——
    /// 被跳过的孩子既没释放、又随后被 Clear() 摘掉父子关系，变成"有句柄、无引用、
    /// 未释放"的孤儿。GC 终结时走终结器线程 Dispose，Sunny UITextBox 的释放路径
    /// 里读内部 TextBox/UIScrollBar.Handle，创建线程（UI）≠终结器线程 →
    /// InvalidOperationException"线程间操作无效"，堆栈终点
    /// ResetAutoComplete←Dispose←Finalize，且 Name 全空（动态控件都没设 Name）。
    /// 现场症状极具迷惑性：炸的时机是 GC 时机（比如正在拖节点），不是泄漏的时机
    /// （早先某次切换节点）。V1.72.12 的 foreach 版"先 Dispose 再 Clear"只修对一半，
    /// 漏了"枚举中集合被改"这一层，流程驾驶舱拖业务框照样炸。
    ///
    /// 【正确姿势】先 CopyTo 快照成数组（枚举的是快照，不怕原集合被改），
    /// 再逐个 Dispose，最后 Clear。两步缺一不可：只快照不 Clear 会留空引用；
    /// 只 Clear 不 Dispose 进终结器（V1.72.12 血泪）。
    ///
    /// 【调用方】ProcessPolicyForm.DisposeEditorControls（右栏节点编辑器）、
    /// MainForm.CreateWorkstationPanels（左侧工位区重建）。以后凡是"动态重建容器"
    /// 一律调这里，不要手写 foreach。
    /// </summary>
    public static class ControlDisposeHelper
    {
        /// <summary>
        /// 快照后逐个释放再清空（线程：必须在创建控件的 UI 线程调；
        /// 单个释放失败不影响其余，全部走完后集合必空）。
        /// </summary>
        /// <param name="controls">要清空的容器控件集合（可为 null，null 直接返回）</param>
        public static void DisposeAllAndClear(Control.ControlCollection controls)
        {
            if (controls == null) return;
            // 快照：枚举快照数组，后续 Dispose 摘除原集合元素不影响遍历，
            // 既不会抛"集合已修改"，也不会跳过任何一个孩子。
            Control[] snapshot = new Control[controls.Count];
            controls.CopyTo(snapshot, 0);
            foreach (Control c in snapshot)
            {
                try
                {
                    if (c != null && !c.IsDisposed) c.Dispose();
                }
                catch { /* 单个释放失败不影响其余，Clear 照样执行防残留 */ }
            }
            controls.Clear();
        }
    }
}
