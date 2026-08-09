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

        /// <summary>
        /// DataGridView 由 CellTemplate 克隆出实际单元格时只复制基类属性，
        /// 这里手动把自定义属性带过去（否则 Maximum/HexDigits/ShowPrefix 等会被默认值覆盖）
        /// </summary>
        public override object Clone()
        {
            var c = base.Clone() as DataGridViewNumericUpDownCell;
            c.Minimum = Minimum;
            c.Maximum = Maximum;
            c.Increment = Increment;
            c.DecimalPlaces = DecimalPlaces;
            return c;
        }

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
    /// 十六进制数字型单元格：进入编辑状态时弹出 NumericUpDown（Hexadecimal=true）微调框，
    /// 用于 IO 备用通道映射弹窗中"通道号（0x00~0xFF）"的输入。
    /// 非编辑状态显示为两位大写十六进制文本。
    /// </summary>
    public class DataGridViewHexNumericUpDownCell : DataGridViewTextBoxCell
    {
        public decimal Minimum { get; set; } = 0;
        public decimal Maximum { get; set; } = 0xFF;
        /// <summary>显示位数（寄存器用 4 位，通道用 2 位）</summary>
        public int HexDigits { get; set; } = 2;
        /// <summary>是否显示 0x 前缀（寄存器地址显示 0x2000，通道号不显示）</summary>
        public bool ShowPrefix { get; set; } = false;

        public override Type EditType => typeof(DataGridViewHexNumericUpDownEditingControl);

        public override Type ValueType => typeof(decimal);

        public override Type FormattedValueType => typeof(string);

        /// <summary>
        /// 由 CellTemplate 克隆实际单元格时带上自定义属性（HexDigits/ShowPrefix 等），
        /// 否则会回落成默认值，导致寄存器只显示 2 位、无 0x 前缀。
        /// </summary>
        public override object Clone()
        {
            var c = base.Clone() as DataGridViewHexNumericUpDownCell;
            c.Minimum = Minimum;
            c.Maximum = Maximum;
            c.HexDigits = HexDigits;
            c.ShowPrefix = ShowPrefix;
            return c;
        }

        /// <summary>非编辑状态下把数值格式化为十六进制显示；空值显示为空</summary>
        protected override object GetFormattedValue(object value, int rowIndex, ref DataGridViewCellStyle cellStyle,
            TypeConverter valueTypeConverter, TypeConverter formattedValueTypeConverter,
            DataGridViewDataErrorContexts context)
        {
            if (value is decimal d)
            {
                string hex = ((int)d).ToString("X" + HexDigits, CultureInfo.CurrentCulture);
                return ShowPrefix ? "0x" + hex : hex;
            }
            return "";
        }

        /// <summary>编辑结束后把值还原为 decimal 存入单元格，并钳制到范围</summary>
        public override object ParseFormattedValue(object formattedValue, DataGridViewCellStyle cellStyle,
            TypeConverter formattedValueTypeConverter, TypeConverter valueTypeConverter)
        {
            if (formattedValue is decimal dm) return Math.Max(Minimum, Math.Min(Maximum, dm));
            if (formattedValue is string s && decimal.TryParse(s, NumberStyles.HexNumber,
                CultureInfo.CurrentCulture, out decimal parsed))
            {
                return Math.Max(Minimum, Math.Min(Maximum, parsed));
            }
            return base.ParseFormattedValue(formattedValue, cellStyle,
                formattedValueTypeConverter, valueTypeConverter);
        }

        /// <summary>进入编辑状态时把范围 / 十六进制同步给微调控件</summary>
        public override void InitializeEditingControl(int rowIndex, object initialFormattedValue,
            DataGridViewCellStyle dataGridViewCellStyle)
        {
            base.InitializeEditingControl(rowIndex, initialFormattedValue, dataGridViewCellStyle);
            if (DataGridView.EditingControl is DataGridViewHexNumericUpDownEditingControl ctl)
            {
                ctl.Minimum = Minimum;
                ctl.Maximum = Maximum;
                ctl.Value = Value is decimal d ? Math.Max(Minimum, Math.Min(Maximum, d)) : Minimum;
            }
        }
    }

    /// <summary>
    /// 十六进制 NumericUpDown 编辑控件：Hexadecimal=true，显示/输入均为十六进制
    /// </summary>
    public class DataGridViewHexNumericUpDownEditingControl : NumericUpDown, IDataGridViewEditingControl
    {
        private DataGridView _dataGridView;
        private int _rowIndex;
        private bool _valueChanged;

        public DataGridViewHexNumericUpDownEditingControl()
        {
            Hexadecimal = true;
            ThousandsSeparator = false;
        }

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
