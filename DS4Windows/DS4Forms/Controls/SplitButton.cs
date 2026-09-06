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

using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace DS4WinWPF.DS4Forms.Controls
{
    [TemplatePart(Name = MainButtonPartName, Type = typeof(Button))]
    [TemplatePart(Name = ToggleButtonPartName, Type = typeof(ToggleButton))]
    public class SplitButton : ContentControl, ICommandSource
    {
        internal const string MainButtonPartName = "PART_MainButton";
        internal const string ToggleButtonPartName = "PART_ToggleButton";

        public static readonly DependencyProperty DropDownContentProperty =
            DependencyProperty.Register(nameof(DropDownContent), typeof(object),
                typeof(SplitButton), new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty IsOpenProperty =
            DependencyProperty.Register(nameof(IsOpen), typeof(bool),
                typeof(SplitButton),
                new FrameworkPropertyMetadata(false,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnIsOpenChanged));

        public static readonly DependencyProperty CommandProperty =
            ButtonBase.CommandProperty.AddOwner(typeof(SplitButton));

        public static readonly DependencyProperty CommandParameterProperty =
            ButtonBase.CommandParameterProperty.AddOwner(typeof(SplitButton));

        public static readonly DependencyProperty CommandTargetProperty =
            ButtonBase.CommandTargetProperty.AddOwner(typeof(SplitButton));

        public static readonly RoutedEvent ClickEvent =
            EventManager.RegisterRoutedEvent(nameof(Click),
                RoutingStrategy.Bubble, typeof(RoutedEventHandler),
                typeof(SplitButton));

        private Button mainButton;
        private ToggleButton toggleButton;
        private ButtonBase dropDownButton;

        static SplitButton()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(SplitButton),
                new FrameworkPropertyMetadata(typeof(SplitButton)));
        }

        public object DropDownContent
        {
            get => GetValue(DropDownContentProperty);
            set => SetValue(DropDownContentProperty, value);
        }

        public bool IsOpen
        {
            get => (bool)GetValue(IsOpenProperty);
            set => SetValue(IsOpenProperty, value);
        }

        public ICommand Command
        {
            get => (ICommand)GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        public object CommandParameter
        {
            get => GetValue(CommandParameterProperty);
            set => SetValue(CommandParameterProperty, value);
        }

        public IInputElement CommandTarget
        {
            get => (IInputElement)GetValue(CommandTargetProperty);
            set => SetValue(CommandTargetProperty, value);
        }

        public event RoutedEventHandler Click
        {
            add => AddHandler(ClickEvent, value);
            remove => RemoveHandler(ClickEvent, value);
        }

        public override void OnApplyTemplate()
        {
            if (mainButton != null)
            {
                mainButton.Click -= MainButton_Click;
            }

            base.OnApplyTemplate();
            mainButton = GetTemplateChild(MainButtonPartName) as Button;
            if (mainButton != null)
            {
                mainButton.Click += MainButton_Click;
            }

            toggleButton = GetTemplateChild(ToggleButtonPartName) as ToggleButton;
            HookDropDownButton();
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);
            if (e.Key == Key.F4 ||
                (e.Key == Key.Down &&
                    (Keyboard.Modifiers & ModifierKeys.Alt) != 0))
            {
                SetCurrentValue(IsOpenProperty, true);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape && IsOpen)
            {
                SetCurrentValue(IsOpenProperty, false);
                e.Handled = true;
            }
            else if ((e.Key == Key.Enter || e.Key == Key.Space) &&
                !IsOpen &&
                !IsDropDownToggleSource(e))
            {
                Invoke();
                e.Handled = true;
            }
        }

        protected override AutomationPeer OnCreateAutomationPeer() =>
            new SplitButtonAutomationPeer(this);

        internal void Invoke()
        {
            if (!IsEnabled)
            {
                return;
            }

            RaiseEvent(new RoutedEventArgs(ClickEvent, this));
            if (Command == null)
            {
                return;
            }

            if (Command is RoutedCommand routedCommand)
            {
                if (routedCommand.CanExecute(CommandParameter, CommandTarget))
                {
                    routedCommand.Execute(CommandParameter, CommandTarget);
                }
            }
            else if (Command.CanExecute(CommandParameter))
            {
                Command.Execute(CommandParameter);
            }
        }

        private void MainButton_Click(object sender, RoutedEventArgs e) => Invoke();

        private void HookDropDownButton()
        {
            if (dropDownButton != null)
            {
                dropDownButton.Click -= DropDownButton_Click;
            }

            dropDownButton = DropDownContent as ButtonBase;
            if (dropDownButton != null)
            {
                dropDownButton.Click += DropDownButton_Click;
            }
        }

        private void DropDownButton_Click(object sender, RoutedEventArgs e) =>
            SetCurrentValue(IsOpenProperty, false);

        private bool IsDropDownToggleSource(KeyEventArgs e) =>
            ReferenceEquals(e.OriginalSource, toggleButton) ||
            ReferenceEquals(e.Source, toggleButton) ||
            (toggleButton?.IsKeyboardFocusWithin ?? false);

        private static void OnIsOpenChanged(DependencyObject sender,
            DependencyPropertyChangedEventArgs e)
        {
            if (UIElementAutomationPeer.FromElement((SplitButton)sender) is
                SplitButtonAutomationPeer peer)
            {
                peer.RaiseAutomationPropertyChanged(
                    ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                    (bool)e.OldValue
                        ? ExpandCollapseState.Expanded
                        : ExpandCollapseState.Collapsed,
                    (bool)e.NewValue
                        ? ExpandCollapseState.Expanded
                        : ExpandCollapseState.Collapsed);
            }
        }
    }

    internal class SplitButtonAutomationPeer :
        FrameworkElementAutomationPeer, IInvokeProvider, IExpandCollapseProvider
    {
        internal SplitButtonAutomationPeer(SplitButton owner) : base(owner)
        {
        }

        private SplitButton SplitOwner => (SplitButton)Owner;

        protected override string GetClassNameCore() => nameof(SplitButton);

        protected override AutomationControlType GetAutomationControlTypeCore() =>
            AutomationControlType.SplitButton;

        protected override string GetNameCore()
        {
            string frameworkName = base.GetNameCore();
            if (!string.IsNullOrWhiteSpace(frameworkName))
            {
                return frameworkName;
            }

            return SplitOwner.Content?.ToString() ?? "Split button";
        }

        public override object GetPattern(PatternInterface patternInterface)
        {
            if (patternInterface == PatternInterface.Invoke ||
                patternInterface == PatternInterface.ExpandCollapse)
            {
                return this;
            }

            return base.GetPattern(patternInterface);
        }

        internal virtual void RaiseAutomationPropertyChanged(
            AutomationProperty property, object oldValue, object newValue) =>
            RaisePropertyChangedEvent(property, oldValue, newValue);

        void IInvokeProvider.Invoke()
        {
            if (!IsEnabled())
            {
                throw new ElementNotEnabledException();
            }

            SplitOwner.Invoke();
        }

        ExpandCollapseState IExpandCollapseProvider.ExpandCollapseState =>
            SplitOwner.IsOpen
                ? ExpandCollapseState.Expanded
                : ExpandCollapseState.Collapsed;

        void IExpandCollapseProvider.Collapse()
        {
            if (!IsEnabled())
            {
                throw new ElementNotEnabledException();
            }

            SplitOwner.SetCurrentValue(SplitButton.IsOpenProperty, false);
        }

        void IExpandCollapseProvider.Expand()
        {
            if (!IsEnabled())
            {
                throw new ElementNotEnabledException();
            }

            SplitOwner.SetCurrentValue(SplitButton.IsOpenProperty, true);
        }
    }
}
