using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace BarometerWinform.Controls
{
    /// <summary>
    /// 下拉选项：Display 为界面显示文本，Value 为保存到配置的实际值。
    /// 用于"显示中文/数字、存枚举名"的场景（如校验位、停止位）。
    /// </summary>
    public class ComboOption
    {
        public string Display { get; set; }
        public string Value { get; set; }

        public ComboOption(string display, string value)
        {
            Display = display;
            Value = value;
        }
    }

    /// <summary>
    /// 可手输的下拉单元格：允许用户从下拉列表选，也可以直接输入自定义值
    /// （用于波特率：列出常用档位，也支持自定义波特率）。
    /// </summary>
    public class DataGridViewEditableComboBoxCell : DataGridViewComboBoxCell
    {
        public override void InitializeEditingControl(int rowIndex, object initialFormattedValue,
            DataGridViewCellStyle dataGridViewCellStyle)
        {
            base.InitializeEditingControl(rowIndex, initialFormattedValue, dataGridViewCellStyle);
            if (DataGridView.EditingControl is ComboBox ctl)
            {
                ctl.DropDownStyle = ComboBoxStyle.DropDown;   // 允许手输
                ctl.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                ctl.AutoCompleteSource = AutoCompleteSource.ListItems;
            }
        }

        /// <summary>允许任意文本作为值（不强制属于下拉列表项），支持自定义波特率</summary>
        public override object ParseFormattedValue(object formattedValue, DataGridViewCellStyle cellStyle,
            TypeConverter formattedValueTypeConverter, TypeConverter valueTypeConverter)
        {
            return formattedValue?.ToString() ?? "";
        }
    }

    /// <summary>
    /// 只读下拉单元格：进入编辑时显示为 DropDownList（只能选择，不能手输），
    /// 用于 true/false、串口列表、数据位/停止位/校验位等"只能从给定选项里选"的配置项。
    /// </summary>
    public class DataGridViewStrictComboBoxCell : DataGridViewComboBoxCell
    {
        public override void InitializeEditingControl(int rowIndex, object initialFormattedValue,
            DataGridViewCellStyle dataGridViewCellStyle)
        {
            base.InitializeEditingControl(rowIndex, initialFormattedValue, dataGridViewCellStyle);
            if (DataGridView.EditingControl is ComboBox ctl)
            {
                ctl.DropDownStyle = ComboBoxStyle.DropDownList;
            }
        }
    }

    /// <summary>
    /// 数字型单元格：进入编辑状态时弹出 NumericUpDown 微调框，
    /// 支持按列设置上限 / 下限 / 步进 / 小数位，防止用户输入非法数值。
    /// 非编辑状态显示为普通文本（FormattedValueType 为 string，并重写 GetFormattedValue
    /// 输出格式化数字），避免"未编辑时空白、点击后才显示"的问题。
    /// 供系统设置窗口（SettingsForm）的"设置值"列使用。
    /// </summary>
    public class DataGridViewNumericUpDownCell : DataGridViewTextBoxCell
    {
        public decimal Minimum { get; set; } = 0;
        public decimal Maximum { get; set; } = 100;
        public decimal Increment { get; set; } = 1;
        public int DecimalPlaces { get; set; } = 0;

        public override Type EditType => typeof(DataGridViewNumericUpDownEditingControl);

        public override Type ValueType => typeof(decimal);

        public override Type FormattedValueType => typeof(string);

        /// <summary>非编辑状态下把数值按小数位格式化成文本显示（修复点击前不显示的问题）</summary>
        protected override object GetFormattedValue(object value, int rowIndex, ref DataGridViewCellStyle cellStyle,
            TypeConverter valueTypeConverter, TypeConverter formattedValueTypeConverter,
            DataGridViewDataErrorContexts context)
        {
            if (value is decimal d)
            {
                return d.ToString("F" + DecimalPlaces, CultureInfo.CurrentCulture);
            }
            return base.GetFormattedValue(value, rowIndex, ref cellStyle,
                valueTypeConverter, formattedValueTypeConverter, context);
        }

        /// <summary>编辑结束后把编辑控件的值（string 或 decimal）还原为 decimal 存入单元格</summary>
        public override object ParseFormattedValue(object formattedValue, DataGridViewCellStyle cellStyle,
            TypeConverter formattedValueTypeConverter, TypeConverter valueTypeConverter)
        {
            if (formattedValue is decimal dm) return dm;
            if (formattedValue is string s && decimal.TryParse(s, NumberStyles.Float,
                CultureInfo.CurrentCulture, out decimal parsed))
            {
                return Math.Max(Minimum, Math.Min(Maximum, parsed));
            }
            return base.ParseFormattedValue(formattedValue, cellStyle,
                formattedValueTypeConverter, valueTypeConverter);
        }

        /// <summary>进入编辑状态时把范围 / 步进 / 小数位同步给微调控件</summary>
        public override void InitializeEditingControl(int rowIndex, object initialFormattedValue,
            DataGridViewCellStyle dataGridViewCellStyle)
        {
            base.InitializeEditingControl(rowIndex, initialFormattedValue, dataGridViewCellStyle);
            if (DataGridView.EditingControl is DataGridViewNumericUpDownEditingControl ctl)
            {
                ctl.Minimum = Minimum;
                ctl.Maximum = Maximum;
                ctl.Increment = Increment;
                ctl.DecimalPlaces = DecimalPlaces;
                ctl.Value = (decimal)Value;
            }
        }
    }

    /// <summary>
    /// NumericUpDown 编辑控件，实现 IDataGridViewEditingControl 接口以嵌入 DataGridView
    /// </summary>
    public class DataGridViewNumericUpDownEditingControl : NumericUpDown, IDataGridViewEditingControl
    {
        private DataGridView _dataGridView;
        private int _rowIndex;
        private bool _valueChanged;

        public DataGridView EditingControlDataGridView
        {
            get => _dataGridView;
            set => _dataGridView = value;
        }

        public object EditingControlFormattedValue
        {
            get => Value;
            set
            {
                if (value is decimal d)
                {
                    Value = Math.Max(Minimum, Math.Min(Maximum, d));
                }
            }
        }

        public object GetEditingControlFormattedValue(DataGridViewDataErrorContexts context)
        {
            return Value;
        }

        public int EditingControlRowIndex
        {
            get => _rowIndex;
            set => _rowIndex = value;
        }

        public bool EditingControlValueChanged
        {
            get => _valueChanged;
            set => _valueChanged = value;
        }

        public Cursor EditingPanelCursor => Cursors.Default;

        public bool RepositionEditingControlOnValueChange => false;

        public void ApplyCellStyleToEditingControl(DataGridViewCellStyle dataGridViewCellStyle)
        {
            Font = dataGridViewCellStyle.Font;
            ForeColor = dataGridViewCellStyle.ForeColor;
            BackColor = dataGridViewCellStyle.BackColor;
        }

        public bool EditingControlWantsInputKey(Keys keyData, bool dataGridViewWantsInputKey)
        {
            return keyData == Keys.Left || keyData == Keys.Right ||
                   keyData == Keys.Up || keyData == Keys.Down ||
                   keyData == Keys.Home || keyData == Keys.End;
        }

        public void PrepareEditingControlForEdit(bool selectAll)
        {
        }

        protected override void OnValueChanged(EventArgs e)
        {
            _valueChanged = true;
            EditingControlDataGridView?.NotifyCurrentCellDirty(true);
            base.OnValueChanged(e);
        }
    }
}
