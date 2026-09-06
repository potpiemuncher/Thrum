/*
Thrum
Copyright (C) 2026  Thrum contributors

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using System;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;

namespace DS4WinWPF.DS4Forms.Controls
{
    public enum SpinnerLocation
    {
        Left,
        Right,
    }

    /// <summary>
    /// Shared input, spinning, formatting, and automation behavior for Thrum's
    /// clean-room numeric editors.
    /// </summary>
    [TemplatePart(Name = TextBoxPartName, Type = typeof(TextBox))]
    [TemplatePart(Name = IncreaseButtonPartName, Type = typeof(RepeatButton))]
    [TemplatePart(Name = DecreaseButtonPartName, Type = typeof(RepeatButton))]
    public abstract class NumericUpDownBase : Control
    {
        internal const string TextBoxPartName = "PART_TextBox";
        internal const string IncreaseButtonPartName = "PART_IncreaseButton";
        internal const string DecreaseButtonPartName = "PART_DecreaseButton";

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string),
                typeof(NumericUpDownBase),
                new FrameworkPropertyMetadata(string.Empty,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static readonly DependencyProperty FormatStringProperty =
            DependencyProperty.Register(nameof(FormatString), typeof(string),
                typeof(NumericUpDownBase),
                new FrameworkPropertyMetadata(null, OnDisplayPropertyChanged));

        public static readonly DependencyProperty ButtonSpinnerLocationProperty =
            DependencyProperty.Register(nameof(ButtonSpinnerLocation),
                typeof(SpinnerLocation), typeof(NumericUpDownBase),
                new FrameworkPropertyMetadata(SpinnerLocation.Right));

        public static readonly DependencyProperty ShowButtonSpinnerProperty =
            DependencyProperty.Register(nameof(ShowButtonSpinner), typeof(bool),
                typeof(NumericUpDownBase), new FrameworkPropertyMetadata(true));

        public static readonly DependencyProperty IsReadOnlyProperty =
            DependencyProperty.Register(nameof(IsReadOnly), typeof(bool),
                typeof(NumericUpDownBase),
                new FrameworkPropertyMetadata(false,
                    FrameworkPropertyMetadataOptions.Inherits));

        public static readonly RoutedEvent ValueChangedEvent =
            EventManager.RegisterRoutedEvent(nameof(ValueChanged),
                RoutingStrategy.Bubble,
                typeof(RoutedPropertyChangedEventHandler<object>),
                typeof(NumericUpDownBase));

        private TextBox textBox;
        private RepeatButton increaseButton;
        private RepeatButton decreaseButton;

        static NumericUpDownBase()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(NumericUpDownBase),
                new FrameworkPropertyMetadata(typeof(NumericUpDownBase)));
            HorizontalContentAlignmentProperty.OverrideMetadata(
                typeof(NumericUpDownBase),
                new FrameworkPropertyMetadata(HorizontalAlignment.Stretch));
            VerticalContentAlignmentProperty.OverrideMetadata(
                typeof(NumericUpDownBase),
                new FrameworkPropertyMetadata(VerticalAlignment.Center));
        }

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public string FormatString
        {
            get => (string)GetValue(FormatStringProperty);
            set => SetValue(FormatStringProperty, value);
        }

        public SpinnerLocation ButtonSpinnerLocation
        {
            get => (SpinnerLocation)GetValue(ButtonSpinnerLocationProperty);
            set => SetValue(ButtonSpinnerLocationProperty, value);
        }

        public bool ShowButtonSpinner
        {
            get => (bool)GetValue(ShowButtonSpinnerProperty);
            set => SetValue(ShowButtonSpinnerProperty, value);
        }

        public bool IsReadOnly
        {
            get => (bool)GetValue(IsReadOnlyProperty);
            set => SetValue(IsReadOnlyProperty, value);
        }

        public event RoutedPropertyChangedEventHandler<object> ValueChanged
        {
            add => AddHandler(ValueChangedEvent, value);
            remove => RemoveHandler(ValueChangedEvent, value);
        }

        public override void OnApplyTemplate()
        {
            if (textBox != null)
            {
                textBox.LostKeyboardFocus -= TextBox_LostKeyboardFocus;
                textBox.PreviewKeyDown -= TextBox_PreviewKeyDown;
            }

            if (increaseButton != null)
            {
                increaseButton.Click -= IncreaseButton_Click;
            }

            if (decreaseButton != null)
            {
                decreaseButton.Click -= DecreaseButton_Click;
            }

            base.OnApplyTemplate();

            textBox = GetTemplateChild(TextBoxPartName) as TextBox;
            increaseButton = GetTemplateChild(IncreaseButtonPartName) as RepeatButton;
            decreaseButton = GetTemplateChild(DecreaseButtonPartName) as RepeatButton;

            if (textBox != null)
            {
                textBox.LostKeyboardFocus += TextBox_LostKeyboardFocus;
                textBox.PreviewKeyDown += TextBox_PreviewKeyDown;
            }

            if (increaseButton != null)
            {
                increaseButton.Click += IncreaseButton_Click;
            }

            if (decreaseButton != null)
            {
                decreaseButton.Click += DecreaseButton_Click;
            }

            RefreshText();
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);
            if (IsReadOnly)
            {
                return;
            }

            if (e.Key == Key.Up)
            {
                ChangeValue(1);
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                ChangeValue(-1);
                e.Handled = true;
            }
        }

        protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
        {
            base.OnPreviewMouseWheel(e);
            if (!IsReadOnly && IsKeyboardFocusWithin && e.Delta != 0)
            {
                ChangeValue(e.Delta > 0 ? 1 : -1);
                e.Handled = true;
            }
        }

        protected override AutomationPeer OnCreateAutomationPeer() =>
            new NumericUpDownAutomationPeer(this);

        internal abstract DependencyProperty ValueDependencyProperty { get; }
        internal abstract object BoxedValue { get; }
        internal abstract string AutomationValue { get; }
        internal abstract double AutomationMinimum { get; }
        internal abstract double AutomationMaximum { get; }
        internal abstract double AutomationIncrement { get; }
        internal abstract double AutomationNumericValue { get; }
        internal abstract void SetAutomationValue(double value);
        internal abstract bool CommitText(string candidate);
        protected abstract void ChangeValue(int direction);
        protected abstract void RefreshText();

        internal void RaiseValueChanged(object oldValue, object newValue)
        {
            RaiseEvent(new RoutedPropertyChangedEventArgs<object>(oldValue,
                newValue, ValueChangedEvent));
        }

        internal void RaiseAutomationPropertyChanged(
            AutomationProperty property, object oldValue, object newValue)
        {
            if (UIElementAutomationPeer.FromElement(this) is
                NumericUpDownAutomationPeer peer)
            {
                peer.RaiseAutomationPropertyChanged(property, oldValue,
                    newValue);
            }
        }

        internal string ResolveAutomationName()
        {
            string explicitName = AutomationProperties.GetName(this);
            if (!string.IsNullOrWhiteSpace(explicitName))
            {
                return explicitName;
            }

            BindingExpression expression = BindingOperations.GetBindingExpression(
                this, ValueDependencyProperty);
            string path = expression?.ParentBinding?.Path?.Path;
            if (!string.IsNullOrWhiteSpace(path))
            {
                int separator = Math.Max(path.LastIndexOf('.'),
                    path.LastIndexOf(']'));
                string leaf = separator >= 0 && separator + 1 < path.Length
                    ? path.Substring(separator + 1)
                    : path;
                string readable = Humanize(leaf);
                if (!string.IsNullOrWhiteSpace(readable))
                {
                    return readable;
                }
            }

            if (!string.IsNullOrWhiteSpace(Name))
            {
                return Humanize(Name);
            }

            if (ToolTip is string tooltip && !string.IsNullOrWhiteSpace(tooltip))
            {
                return tooltip;
            }

            return "Numeric value";
        }

        private static string Humanize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(value.Length + 8);
            char previous = '\0';
            foreach (char character in value.Trim('_'))
            {
                if (character == '_' || character == '-')
                {
                    if (builder.Length > 0 && builder[builder.Length - 1] != ' ')
                    {
                        builder.Append(' ');
                    }

                    previous = character;
                    continue;
                }

                if (builder.Length > 0 && char.IsUpper(character) &&
                    (char.IsLower(previous) || char.IsDigit(previous)))
                {
                    builder.Append(' ');
                }

                builder.Append(character);
                previous = character;
            }

            return builder.ToString();
        }

        private static void OnDisplayPropertyChanged(DependencyObject sender,
            DependencyPropertyChangedEventArgs e) =>
            ((NumericUpDownBase)sender).RefreshText();

        private void TextBox_LostKeyboardFocus(object sender,
            KeyboardFocusChangedEventArgs e) => CommitCurrentText();

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommitCurrentText();
            }
            else if (e.Key == Key.Escape)
            {
                RefreshText();
                e.Handled = true;
            }
        }

        private void CommitCurrentText()
        {
            if (IsReadOnly)
            {
                RefreshText();
                return;
            }

            if (!CommitText(textBox?.Text ?? Text))
            {
                RefreshText();
            }
        }

        private void IncreaseButton_Click(object sender, RoutedEventArgs e) =>
            ChangeValue(1);

        private void DecreaseButton_Click(object sender, RoutedEventArgs e) =>
            ChangeValue(-1);
    }

    public abstract class NumericUpDown<T> : NumericUpDownBase
        where T : struct, IComparable<T>, IFormattable
    {
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(T?),
                typeof(NumericUpDown<T>),
                new FrameworkPropertyMetadata(null,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnValueChanged, CoerceValue));

        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register(nameof(Minimum), typeof(T?),
                typeof(NumericUpDown<T>),
                new FrameworkPropertyMetadata(null, OnRangeChanged));

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(nameof(Maximum), typeof(T?),
                typeof(NumericUpDown<T>),
                new FrameworkPropertyMetadata(null, OnRangeChanged));

        public static readonly DependencyProperty IncrementProperty =
            DependencyProperty.Register(nameof(Increment), typeof(T),
                typeof(NumericUpDown<T>),
                new FrameworkPropertyMetadata(default(T)));

        public static readonly DependencyProperty DefaultValueProperty =
            DependencyProperty.Register(nameof(DefaultValue), typeof(T?),
                typeof(NumericUpDown<T>), new FrameworkPropertyMetadata(null));

        public T? Value
        {
            get => (T?)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public T? Minimum
        {
            get => (T?)GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        public T? Maximum
        {
            get => (T?)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public T Increment
        {
            get => (T)GetValue(IncrementProperty);
            set => SetValue(IncrementProperty, value);
        }

        public T? DefaultValue
        {
            get => (T?)GetValue(DefaultValueProperty);
            set => SetValue(DefaultValueProperty, value);
        }

        internal override DependencyProperty ValueDependencyProperty => ValueProperty;
        internal override object BoxedValue => Value.HasValue ? Value.Value : null;
        internal override string AutomationValue => Format(Value);
        internal override double AutomationMinimum => Minimum.HasValue
            ? ToDouble(Minimum.Value)
            : ToDouble(RepresentableMinimum);
        internal override double AutomationMaximum => Maximum.HasValue
            ? ToDouble(Maximum.Value)
            : ToDouble(RepresentableMaximum);
        internal override double AutomationIncrement => Math.Abs(ToDouble(Increment));
        internal override double AutomationNumericValue => Value.HasValue
            ? ToDouble(Value.Value)
            : 0.0;

        protected abstract T RepresentableMinimum { get; }
        protected abstract T RepresentableMaximum { get; }
        protected abstract bool TryParse(string text, out T value);
        protected abstract T Step(T value, T increment, int direction);
        protected abstract double ToDouble(T value);
        protected abstract T FromDouble(double value);

        internal override void SetAutomationValue(double value)
        {
            if (IsReadOnly)
            {
                throw new ElementNotEnabledException();
            }

            if (double.IsNaN(value) || value < AutomationMinimum ||
                value > AutomationMaximum)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            SetCurrentValue(ValueProperty, (T?)FromDouble(value));
        }

        internal override bool CommitText(string candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                SetCurrentValue(ValueProperty, DefaultValue);
                return true;
            }

            if (!TryParse(candidate, out T parsed))
            {
                return false;
            }

            SetCurrentValue(ValueProperty, (T?)parsed);
            return true;
        }

        protected override void ChangeValue(int direction)
        {
            if (IsReadOnly)
            {
                return;
            }

            // Text is updated on every keystroke. Commit it before stepping so
            // Up/Down, the wheel, and spinner buttons use what the user sees
            // rather than the prior Value.
            if (!CommitText(Text))
            {
                RefreshText();
            }

            T start = Value ?? DefaultValue ?? Minimum ?? default(T);
            T increment = Increment;
            if (increment.CompareTo(default(T)) == 0)
            {
                increment = DefaultIncrement;
            }

            T next;
            try
            {
                next = Step(start, increment, direction);
            }
            catch (OverflowException)
            {
                next = direction > 0
                    ? Maximum ?? start
                    : Minimum ?? start;
            }

            SetCurrentValue(ValueProperty, (T?)next);
        }

        protected abstract T DefaultIncrement { get; }

        protected override void RefreshText() =>
            SetCurrentValue(TextProperty, Format(Value));

        private string Format(T? value)
        {
            if (!value.HasValue)
            {
                return string.Empty;
            }

            return value.Value.ToString(FormatString, CultureInfo.CurrentCulture);
        }

        private static object CoerceValue(DependencyObject sender,
            object candidate)
        {
            NumericUpDown<T> control = (NumericUpDown<T>)sender;
            if (candidate == null)
            {
                return null;
            }

            T value = (T)candidate;
            if (control.Minimum.HasValue &&
                value.CompareTo(control.Minimum.Value) < 0)
            {
                return control.Minimum.Value;
            }

            if (control.Maximum.HasValue &&
                value.CompareTo(control.Maximum.Value) > 0)
            {
                return control.Maximum.Value;
            }

            return value;
        }

        private static void OnValueChanged(DependencyObject sender,
            DependencyPropertyChangedEventArgs e)
        {
            NumericUpDown<T> control = (NumericUpDown<T>)sender;
            control.RefreshText();

            T? oldValue = e.OldValue == null
                ? (T?)null
                : (T)e.OldValue;
            T? newValue = e.NewValue == null
                ? (T?)null
                : (T)e.NewValue;
            control.RaiseAutomationPropertyChanged(
                RangeValuePatternIdentifiers.ValueProperty,
                oldValue.HasValue ? control.ToDouble(oldValue.Value) : 0.0,
                newValue.HasValue ? control.ToDouble(newValue.Value) : 0.0);
            control.RaiseAutomationPropertyChanged(
                ValuePatternIdentifiers.ValueProperty,
                control.Format(oldValue), control.Format(newValue));

            control.RaiseValueChanged(e.OldValue, e.NewValue);
        }

        private static void OnRangeChanged(DependencyObject sender,
            DependencyPropertyChangedEventArgs e)
        {
            NumericUpDown<T> control = (NumericUpDown<T>)sender;
            control.CoerceValue(ValueProperty);
            control.RefreshText();

            bool minimumChanged = e.Property == MinimumProperty;
            double unboundedValue = minimumChanged
                ? control.ToDouble(control.RepresentableMinimum)
                : control.ToDouble(control.RepresentableMaximum);
            double oldValue = e.OldValue is T oldTyped
                ? control.ToDouble(oldTyped)
                : unboundedValue;
            double newValue = e.NewValue is T newTyped
                ? control.ToDouble(newTyped)
                : unboundedValue;
            control.RaiseAutomationPropertyChanged(
                minimumChanged
                    ? RangeValuePatternIdentifiers.MinimumProperty
                    : RangeValuePatternIdentifiers.MaximumProperty,
                oldValue, newValue);
        }
    }

    public sealed class IntegerUpDown : NumericUpDown<int>
    {
        static IntegerUpDown() => IncrementProperty.OverrideMetadata(
            typeof(IntegerUpDown), new FrameworkPropertyMetadata(1));

        protected override int DefaultIncrement => 1;
        protected override int RepresentableMinimum => int.MinValue;
        protected override int RepresentableMaximum => int.MaxValue;
        protected override bool TryParse(string text, out int value) =>
            int.TryParse(text, NumberStyles.Integer,
                CultureInfo.CurrentCulture, out value);
        protected override int Step(int value, int increment, int direction) =>
            checked(value + (increment * direction));
        protected override double ToDouble(int value) => value;
        protected override int FromDouble(double value) =>
            checked((int)Math.Round(value, MidpointRounding.AwayFromZero));
    }

    public sealed class DoubleUpDown : NumericUpDown<double>
    {
        static DoubleUpDown() => IncrementProperty.OverrideMetadata(
            typeof(DoubleUpDown), new FrameworkPropertyMetadata(1.0));

        protected override double DefaultIncrement => 1.0;
        protected override double RepresentableMinimum => double.MinValue;
        protected override double RepresentableMaximum => double.MaxValue;
        protected override bool TryParse(string text, out double value) =>
            double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.CurrentCulture, out value);
        protected override double Step(double value, double increment,
            int direction) => value + (increment * direction);
        protected override double ToDouble(double value) => value;
        protected override double FromDouble(double value) => value;
    }

    public sealed class DecimalUpDown : NumericUpDown<decimal>
    {
        static DecimalUpDown() => IncrementProperty.OverrideMetadata(
            typeof(DecimalUpDown), new FrameworkPropertyMetadata(1m));

        protected override decimal DefaultIncrement => 1m;
        protected override decimal RepresentableMinimum => decimal.MinValue;
        protected override decimal RepresentableMaximum => decimal.MaxValue;
        protected override bool TryParse(string text, out decimal value) =>
            decimal.TryParse(text, NumberStyles.Number,
                CultureInfo.CurrentCulture, out value);
        protected override decimal Step(decimal value, decimal increment,
            int direction) => checked(value + (increment * direction));
        protected override double ToDouble(decimal value) => (double)value;
        protected override decimal FromDouble(double value) => (decimal)value;
    }

    public sealed class SByteUpDown : NumericUpDown<sbyte>
    {
        static SByteUpDown() => IncrementProperty.OverrideMetadata(
            typeof(SByteUpDown), new FrameworkPropertyMetadata((sbyte)1));

        protected override sbyte DefaultIncrement => 1;
        protected override sbyte RepresentableMinimum => sbyte.MinValue;
        protected override sbyte RepresentableMaximum => sbyte.MaxValue;
        protected override bool TryParse(string text, out sbyte value) =>
            sbyte.TryParse(text, NumberStyles.Integer,
                CultureInfo.CurrentCulture, out value);
        protected override sbyte Step(sbyte value, sbyte increment,
            int direction) => checked((sbyte)(value + (increment * direction)));
        protected override double ToDouble(sbyte value) => value;
        protected override sbyte FromDouble(double value) =>
            checked((sbyte)Math.Round(value, MidpointRounding.AwayFromZero));
    }

    public sealed class UIntegerUpDown : NumericUpDown<uint>
    {
        static UIntegerUpDown() => IncrementProperty.OverrideMetadata(
            typeof(UIntegerUpDown), new FrameworkPropertyMetadata(1u));

        protected override uint DefaultIncrement => 1u;
        protected override uint RepresentableMinimum => uint.MinValue;
        protected override uint RepresentableMaximum => uint.MaxValue;
        protected override bool TryParse(string text, out uint value) =>
            uint.TryParse(text, NumberStyles.Integer,
                CultureInfo.CurrentCulture, out value);
        protected override uint Step(uint value, uint increment, int direction)
        {
            if (direction < 0)
            {
                return increment > value ? 0u : value - increment;
            }

            return checked(value + increment);
        }
        protected override double ToDouble(uint value) => value;
        protected override uint FromDouble(double value) =>
            checked((uint)Math.Round(value, MidpointRounding.AwayFromZero));
    }

    internal class NumericUpDownAutomationPeer :
        FrameworkElementAutomationPeer, IRangeValueProvider, IValueProvider
    {
        internal NumericUpDownAutomationPeer(NumericUpDownBase owner)
            : base(owner)
        {
        }

        private NumericUpDownBase NumericOwner =>
            (NumericUpDownBase)Owner;

        protected override string GetClassNameCore() =>
            NumericOwner.GetType().Name;

        protected override AutomationControlType GetAutomationControlTypeCore() =>
            AutomationControlType.Spinner;

        protected override string GetNameCore()
        {
            string frameworkName = base.GetNameCore();
            return !string.IsNullOrWhiteSpace(frameworkName)
                ? frameworkName
                : NumericOwner.ResolveAutomationName();
        }

        public override object GetPattern(PatternInterface patternInterface)
        {
            if (patternInterface == PatternInterface.RangeValue ||
                patternInterface == PatternInterface.Value)
            {
                return this;
            }

            return base.GetPattern(patternInterface);
        }

        internal virtual void RaiseAutomationPropertyChanged(
            AutomationProperty property, object oldValue, object newValue) =>
            RaisePropertyChangedEvent(property, oldValue, newValue);

        bool IRangeValueProvider.IsReadOnly => NumericOwner.IsReadOnly;
        double IRangeValueProvider.LargeChange => NumericOwner.AutomationIncrement;
        double IRangeValueProvider.SmallChange => NumericOwner.AutomationIncrement;
        double IRangeValueProvider.Maximum => NumericOwner.AutomationMaximum;
        double IRangeValueProvider.Minimum => NumericOwner.AutomationMinimum;
        double IRangeValueProvider.Value => NumericOwner.AutomationNumericValue;
        bool IValueProvider.IsReadOnly => NumericOwner.IsReadOnly;
        string IValueProvider.Value => NumericOwner.AutomationValue;

        void IRangeValueProvider.SetValue(double value)
        {
            if (!IsEnabled())
            {
                throw new ElementNotEnabledException();
            }

            NumericOwner.SetAutomationValue(value);
        }

        void IValueProvider.SetValue(string value)
        {
            if (!IsEnabled() || NumericOwner.IsReadOnly)
            {
                throw new ElementNotEnabledException();
            }

            if (!NumericOwner.CommitText(value))
            {
                throw new ArgumentException("The value is not a valid number.",
                    nameof(value));
            }
        }
    }
}
