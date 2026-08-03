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

using DS4WinWPF.DS4Forms;
using DS4WinWPF.DS4Forms.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace DS4WindowsTests;

[TestClass]
public class InHouseToolkitControlTests
{
    [TestMethod]
    public void TypedNumericControlsParseFormatClampAndBindTwoWayByDefault()
    {
        RunOnStaThread(() =>
        {
            CultureInfo previous = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            try
            {
                var integer = new IntegerUpDown
                {
                    Minimum = 0,
                    Maximum = 10,
                    Value = 4,
                };
                int changes = 0;
                integer.ValueChanged += (_, _) => changes++;
                ValueProvider(integer).SetValue("12");
                Assert.AreEqual(10, integer.Value);
                Assert.IsTrue(changes > 0);

                var real = new DoubleUpDown
                {
                    Minimum = 0,
                    Maximum = 10,
                    FormatString = "F2",
                    Value = 1.25,
                };
                Assert.AreEqual("1.25", real.Text);
                ValueProvider(real).SetValue("2.5");
                Assert.AreEqual(2.5, real.Value);
                Assert.AreEqual("2.50", real.Text);

                var precise = new DecimalUpDown
                {
                    Minimum = 0m,
                    Maximum = 5m,
                };
                ValueProvider(precise).SetValue("2.75");
                Assert.AreEqual(2.75m, precise.Value);

                var signed = new SByteUpDown
                {
                    Minimum = -10,
                    Maximum = 10,
                };
                ValueProvider(signed).SetValue("-7");
                Assert.AreEqual((sbyte)-7, signed.Value);

                var unsigned = new UIntegerUpDown
                {
                    Minimum = 0,
                    Maximum = 100,
                };
                ValueProvider(unsigned).SetValue("40");
                Assert.AreEqual(40u, unsigned.Value);

                Assert.IsTrue(((System.Windows.FrameworkPropertyMetadata)
                    IntegerUpDown.ValueProperty.GetMetadata(
                        typeof(IntegerUpDown))).BindsTwoWayByDefault);
                Assert.IsTrue(((System.Windows.FrameworkPropertyMetadata)
                    DoubleUpDown.ValueProperty.GetMetadata(
                        typeof(DoubleUpDown))).BindsTwoWayByDefault);
                Assert.IsTrue(((System.Windows.FrameworkPropertyMetadata)
                    DecimalUpDown.ValueProperty.GetMetadata(
                        typeof(DecimalUpDown))).BindsTwoWayByDefault);
                Assert.IsTrue(((System.Windows.FrameworkPropertyMetadata)
                    SByteUpDown.ValueProperty.GetMetadata(
                        typeof(SByteUpDown))).BindsTwoWayByDefault);
                Assert.IsTrue(((System.Windows.FrameworkPropertyMetadata)
                    UIntegerUpDown.ValueProperty.GetMetadata(
                        typeof(UIntegerUpDown))).BindsTwoWayByDefault);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        });
    }

    [TestMethod]
    public void EnterCommitsTextWithoutSuppressingRecordBoxKeyDown()
    {
        RunOnStaThread(() =>
        {
            var control = new IntegerUpDown
            {
                Value = 2,
                Text = "7",
            };
            var keyEvent = new KeyEventArgs(Keyboard.PrimaryDevice, new TestPresentationSource(),
                Environment.TickCount, Key.Enter)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent,
            };

            MethodInfo handler = typeof(NumericUpDownBase).GetMethod(
                "TextBox_PreviewKeyDown",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(handler);
            handler.Invoke(control, new object[] { control, keyEvent });

            Assert.AreEqual(7, control.Value,
                "Enter must commit the text before the parent sees KeyDown.");
            Assert.IsFalse(keyEvent.Handled,
                "RecordBox.WaitIUD_KeyDown must still receive Enter to update " +
                "the source and leave edit mode.");
        });
    }

    [TestMethod]
    public void SpinCommitsVisibleTextBeforeApplyingIncrement()
    {
        RunOnStaThread(() =>
        {
            var control = new IntegerUpDown
            {
                Minimum = 0,
                Maximum = 100,
                Increment = 1,
                Value = 2,
                Text = "8",
            };

            MethodInfo changeValue = typeof(NumericUpDown<int>).GetMethod(
                "ChangeValue",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(changeValue);
            changeValue.Invoke(control, new object[] { 1 });

            Assert.AreEqual(9, control.Value,
                "The spinner must step from the freshly typed value.");
            Assert.AreEqual("9", control.Text);
        });
    }

    [TestMethod]
    public void SplitButtonLeavesArrowKeysForDropDownToggle()
    {
        RunOnStaThread(() =>
        {
            var control = new SplitButton { Content = "Edit" };
            var arrow = new ToggleButton();
            int clicks = 0;
            control.Click += (_, _) => clicks++;

            FieldInfo toggleField = typeof(SplitButton).GetField(
                "toggleButton", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(toggleField);
            toggleField.SetValue(control, arrow);

            MethodInfo previewKeyDown = typeof(SplitButton).GetMethod(
                "OnPreviewKeyDown",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(previewKeyDown);

            foreach (Key key in new[] { Key.Enter, Key.Space })
            {
                KeyEventArgs arrowKey = PreviewKey(key, arrow);
                previewKeyDown.Invoke(control, new object[] { arrowKey });
                Assert.IsFalse(arrowKey.Handled,
                    "The arrow must receive its activation key so ToggleButton can open it.");
            }
            Assert.AreEqual(0, clicks,
                "Arrow keyboard input must not invoke the primary action.");

            KeyEventArgs primaryEnter = PreviewKey(Key.Enter, control);
            previewKeyDown.Invoke(control, new object[] { primaryEnter });
            Assert.IsTrue(primaryEnter.Handled);
            Assert.AreEqual(1, clicks,
                "Enter elsewhere on the composite remains the primary action.");
        });
    }

    [TestMethod]
    public void AutomationPeersRaiseValueRangeAndExpansionNotifications()
    {
        RunOnStaThread(() =>
        {
            var numeric = new TrackingIntegerUpDown
            {
                Minimum = 0,
                Maximum = 10,
                Value = 2,
            };
            var numericPeer = (TrackingNumericPeer)
                UIElementAutomationPeer.CreatePeerForElement(numeric);
            numeric.Value = 4;
            numeric.Minimum = 1;
            numeric.Maximum = 9;

            Assert.AreEqual(4, numericPeer.Properties.Count);
            Assert.AreSame(RangeValuePatternIdentifiers.ValueProperty,
                numericPeer.Properties[0]);
            Assert.AreSame(ValuePatternIdentifiers.ValueProperty,
                numericPeer.Properties[1]);
            Assert.AreSame(RangeValuePatternIdentifiers.MinimumProperty,
                numericPeer.Properties[2]);
            Assert.AreSame(RangeValuePatternIdentifiers.MaximumProperty,
                numericPeer.Properties[3]);
            Assert.AreEqual(2.0, numericPeer.OldValues[0]);
            Assert.AreEqual(4.0, numericPeer.NewValues[0]);
            Assert.AreEqual("2", numericPeer.OldValues[1]);
            Assert.AreEqual("4", numericPeer.NewValues[1]);

            var unsigned = new TrackingUIntegerUpDown();
            var unsignedPeer = (TrackingNumericPeer)
                UIElementAutomationPeer.CreatePeerForElement(unsigned);
            unsigned.Minimum = 5;
            Assert.AreEqual(1, unsignedPeer.Properties.Count);
            Assert.AreSame(RangeValuePatternIdentifiers.MinimumProperty,
                unsignedPeer.Properties[0]);
            Assert.AreEqual(0.0, unsignedPeer.OldValues[0],
                "The null minimum notification must match uint.MinValue.");
            Assert.AreEqual(5.0, unsignedPeer.NewValues[0]);

            var split = new TrackingSplitButton();
            var splitPeer = (TrackingSplitButtonPeer)
                UIElementAutomationPeer.CreatePeerForElement(split);
            split.IsOpen = true;

            Assert.AreEqual(1, splitPeer.Properties.Count);
            Assert.AreSame(
                ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                splitPeer.Properties[0]);
            Assert.AreEqual(ExpandCollapseState.Collapsed,
                splitPeer.OldValues[0]);
            Assert.AreEqual(ExpandCollapseState.Expanded,
                splitPeer.NewValues[0]);
        });
    }

    [TestMethod]
    public void NumericAutomationUsesRepresentableBoundsWhenUnbounded()
    {
        RunOnStaThread(() =>
        {
            AssertAutomationBounds(new IntegerUpDown(), int.MinValue,
                int.MaxValue);
            AssertAutomationBounds(new DoubleUpDown(), double.MinValue,
                double.MaxValue);
            AssertAutomationBounds(new DecimalUpDown(),
                (double)decimal.MinValue, (double)decimal.MaxValue);
            AssertAutomationBounds(new SByteUpDown(), sbyte.MinValue,
                sbyte.MaxValue);

            var unsigned = new UIntegerUpDown();
            IRangeValueProvider range = RangeProvider(unsigned);
            Assert.AreEqual(0.0, range.Minimum,
                "An unbounded unsigned editor must not advertise negatives.");
            Assert.AreEqual((double)uint.MaxValue, range.Maximum);
            range.SetValue(uint.MaxValue);
            Assert.AreEqual(uint.MaxValue, unsigned.Value);
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                range.SetValue(-1));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                range.SetValue(double.NaN));
        });
    }

    [TestMethod]
    public void NumericAutomationExposesNamedRangeAndTextValuePatterns()
    {
        RunOnStaThread(() =>
        {
            var control = new IntegerUpDown
            {
                Minimum = 0,
                Maximum = 100,
                Increment = 5,
                Value = 20,
            };
            AutomationProperties.SetName(control, "Hip-fire delay");

            AutomationPeer peer =
                UIElementAutomationPeer.CreatePeerForElement(control);
            Assert.IsNotNull(peer);
            Assert.AreEqual(AutomationControlType.Spinner,
                peer.GetAutomationControlType());
            Assert.AreEqual("Hip-fire delay", peer.GetName());

            var range = (IRangeValueProvider)peer.GetPattern(
                PatternInterface.RangeValue);
            var value = (IValueProvider)peer.GetPattern(
                PatternInterface.Value);
            Assert.IsNotNull(range);
            Assert.IsNotNull(value);
            Assert.AreEqual(5.0, range.SmallChange);
            range.SetValue(35);
            Assert.AreEqual(35, control.Value);
            value.SetValue("45");
            Assert.AreEqual(45, control.Value);
        });
    }

    [TestMethod]
    public void SplitButtonAutomationInvokesAndExpandsTheSamePublicState()
    {
        RunOnStaThread(() =>
        {
            var control = new SplitButton
            {
                Content = "Edit",
            };
            int clicks = 0;
            control.Click += (_, _) => clicks++;

            AutomationPeer peer =
                UIElementAutomationPeer.CreatePeerForElement(control);
            Assert.IsNotNull(peer);
            Assert.AreEqual(AutomationControlType.SplitButton,
                peer.GetAutomationControlType());
            Assert.AreEqual("Edit", peer.GetName(),
                "The visible action content must be the accessible name.");

            ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();
            Assert.AreEqual(1, clicks);

            var expansion = (IExpandCollapseProvider)peer.GetPattern(
                PatternInterface.ExpandCollapse);
            expansion.Expand();
            Assert.IsTrue(control.IsOpen);
            Assert.AreEqual(ExpandCollapseState.Expanded,
                expansion.ExpandCollapseState);
            expansion.Collapse();
            Assert.IsFalse(control.IsOpen);

            control.IsEnabled = false;
            Assert.ThrowsException<ElementNotEnabledException>(() =>
                expansion.Expand());
            control.IsOpen = true;
            Assert.ThrowsException<ElementNotEnabledException>(() =>
                expansion.Collapse());
            Assert.IsTrue(control.IsOpen);
        });
    }

    [TestMethod]
    public void ColorPickerOwnsItsSelectionAndRaisesLiveUserChanges()
    {
        RunOnStaThread(() =>
        {
            var picker = new ColorPickerWindow
            {
                SelectedColor = Color.FromRgb(10, 20, 30),
            };
            Assert.AreEqual(Color.FromRgb(10, 20, 30),
                picker.SelectedColor);

            Color observed = default;
            int changes = 0;
            picker.ColorChanged += (_, color) =>
            {
                observed = color;
                changes++;
            };

            FieldInfo field = typeof(ColorPickerWindow).GetField("redSlider",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            ((Slider)field.GetValue(picker)).Value = 42;

            Assert.AreEqual(1, changes);
            Assert.AreEqual((byte)42, observed.R);
            Assert.AreEqual(observed, picker.SelectedColor);
        });
    }

    private static IValueProvider ValueProvider(NumericUpDownBase control)
    {
        AutomationPeer peer =
            UIElementAutomationPeer.CreatePeerForElement(control);
        Assert.IsNotNull(peer);
        return (IValueProvider)peer.GetPattern(PatternInterface.Value);
    }

    private static IRangeValueProvider RangeProvider(
        NumericUpDownBase control)
    {
        AutomationPeer peer =
            UIElementAutomationPeer.CreatePeerForElement(control);
        Assert.IsNotNull(peer);
        return (IRangeValueProvider)peer.GetPattern(
            PatternInterface.RangeValue);
    }

    private static void AssertAutomationBounds(NumericUpDownBase control,
        double minimum, double maximum)
    {
        IRangeValueProvider range = RangeProvider(control);
        Assert.AreEqual(minimum, range.Minimum);
        Assert.AreEqual(maximum, range.Maximum);
    }

    private static KeyEventArgs PreviewKey(Key key, object source)
    {
        return new KeyEventArgs(Keyboard.PrimaryDevice,
            new TestPresentationSource(), Environment.TickCount, key)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent,
            Source = source,
        };
    }

    private sealed class TestPresentationSource : PresentationSource
    {
        private Visual rootVisual = new DrawingVisual();

        public override Visual RootVisual
        {
            get => rootVisual;
            set => rootVisual = value;
        }

        public override bool IsDisposed => false;

        protected override CompositionTarget GetCompositionTargetCore() => null;
    }

    private sealed class TrackingIntegerUpDown : NumericUpDown<int>
    {
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

        protected override AutomationPeer OnCreateAutomationPeer() =>
            new TrackingNumericPeer(this);
    }

    private sealed class TrackingUIntegerUpDown : NumericUpDown<uint>
    {
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

        protected override AutomationPeer OnCreateAutomationPeer() =>
            new TrackingNumericPeer(this);
    }

    private sealed class TrackingNumericPeer : NumericUpDownAutomationPeer
    {
        internal TrackingNumericPeer(NumericUpDownBase owner) : base(owner)
        {
        }

        internal List<AutomationProperty> Properties { get; } = new();
        internal List<object> OldValues { get; } = new();
        internal List<object> NewValues { get; } = new();

        internal override void RaiseAutomationPropertyChanged(
            AutomationProperty property, object oldValue, object newValue)
        {
            Properties.Add(property);
            OldValues.Add(oldValue);
            NewValues.Add(newValue);
        }
    }

    private sealed class TrackingSplitButton : SplitButton
    {
        protected override AutomationPeer OnCreateAutomationPeer() =>
            new TrackingSplitButtonPeer(this);
    }

    private sealed class TrackingSplitButtonPeer : SplitButtonAutomationPeer
    {
        internal TrackingSplitButtonPeer(SplitButton owner) : base(owner)
        {
        }

        internal List<AutomationProperty> Properties { get; } = new();
        internal List<object> OldValues { get; } = new();
        internal List<object> NewValues { get; } = new();

        internal override void RaiseAutomationPropertyChanged(
            AutomationProperty property, object oldValue, object newValue)
        {
            Properties.Add(property);
            OldValues.Add(oldValue);
            NewValues.Add(newValue);
        }
    }

    private static void RunOnStaThread(Action body)
    {
        Exception failure = null;
        Thread thread = new Thread(() =>
        {
            try
            {
                body();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(15)),
            "In-house WPF control test did not finish.");
        if (failure != null)
        {
            Assert.Fail(failure.ToString());
        }
    }
}
