using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using DS4Windows;
using DS4WinWPF.DS4Forms.Controls;
using DS4WinWPF.DS4Forms.ViewModels;

namespace DS4WinWPF.DS4Forms
{
    /// <summary>
    /// Adds compact reset affordances beside the numeric controls in the
    /// profile editor's three dense rails without changing their XAML names or
    /// bindings.
    /// </summary>
    internal sealed class ProfileEditorResetController
    {
        private readonly IReadOnlyList<DependencyObject> sectionRoots;
        private readonly object settingsTarget;
        private readonly BackingStore defaultStore;
        private bool isAttached;

        public ProfileEditorResetController(object settingsTarget,
            params DependencyObject[] sectionRoots)
        {
            this.settingsTarget = settingsTarget ??
                throw new ArgumentNullException(nameof(settingsTarget));
            this.sectionRoots = sectionRoots ??
                throw new ArgumentNullException(nameof(sectionRoots));
            defaultStore = ProfileEditorDefaultProvider.CreateDefaultStore();
        }

        public int AttachedCount { get; private set; }

        public void EnsureAttached()
        {
            if (isAttached)
            {
                return;
            }

            List<NumericBindingTarget> targets = new();
            foreach (DependencyObject sectionRoot in sectionRoots)
            {
                CollectTargets(sectionRoot, targets);
            }

            foreach (NumericBindingTarget target in targets)
            {
                if (ProfileEditorResetCatalog.TryGet(target.SettingName,
                    out ProfileEditorResetEntry resetEntry) &&
                    TryWrapWithResetButton(target, resetEntry))
                {
                    AttachedCount++;
                }
            }

            isAttached = true;
        }

        private void CollectTargets(DependencyObject current,
            ICollection<NumericBindingTarget> targets)
        {
            if (current == null)
            {
                return;
            }

            if (current is FrameworkElement element &&
                IsNumericInput(element) &&
                TryGetValueBinding(element, out DependencyProperty property,
                    out Binding binding) &&
                !string.IsNullOrWhiteSpace(binding.Path?.Path))
            {
                targets.Add(new NumericBindingTarget(element, property,
                    binding.Path.Path));
            }

            foreach (object child in LogicalTreeHelper.GetChildren(current))
            {
                if (child is DependencyObject dependencyObject)
                {
                    CollectTargets(dependencyObject, targets);
                }
            }
        }

        private static bool IsNumericInput(FrameworkElement element)
        {
            return element is Slider ||
                element is NumericUpDownBase;
        }

        private static bool TryGetValueBinding(FrameworkElement element,
            out DependencyProperty property, out Binding binding)
        {
            LocalValueEnumerator values = element.GetLocalValueEnumerator();
            while (values.MoveNext())
            {
                LocalValueEntry value = values.Current;
                if (!string.Equals(value.Property.Name, "Value",
                    StringComparison.Ordinal))
                {
                    continue;
                }

                Binding candidate = BindingOperations.GetBinding(element,
                    value.Property);
                if (candidate != null)
                {
                    property = value.Property;
                    binding = candidate;
                    return true;
                }
            }

            property = null;
            binding = null;
            return false;
        }

        private bool TryWrapWithResetButton(NumericBindingTarget target,
            ProfileEditorResetEntry resetEntry)
        {
            if (target.Element.Parent is not Panel parent)
            {
                return false;
            }

            int childIndex = parent.Children.IndexOf(target.Element);
            if (childIndex < 0)
            {
                return false;
            }

            Thickness originalMargin = target.Element.Margin;
            HorizontalAlignment originalHorizontalAlignment =
                target.Element.HorizontalAlignment;
            VerticalAlignment originalVerticalAlignment =
                target.Element.VerticalAlignment;

            StackPanel wrapper = new()
            {
                Orientation = Orientation.Horizontal,
                Margin = originalMargin,
                HorizontalAlignment = originalHorizontalAlignment,
                VerticalAlignment = originalVerticalAlignment,
            };
            CopyPanelPosition(target.Element, wrapper);

            Button resetButton = new()
            {
                Content = "\u21B6",
                MinWidth = 24.0,
                MinHeight = 22.0,
                Padding = new Thickness(4.0, 0.0, 4.0, 0.0),
                Margin = new Thickness(4.0, 0.0, 0.0, 0.0),
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = $"Reset {target.SettingName} to profile default",
            };
            resetButton.SetResourceReference(FrameworkElement.StyleProperty,
                "BridgeSecondaryButtonStyle");
            AutomationProperties.SetName(resetButton,
                $"Reset {target.SettingName} to profile default");
            resetButton.Click += (_, _) =>
            {
                resetEntry.Reset(settingsTarget, defaultStore,
                    ProfileEditorDefaultProvider.DefaultDeviceIndex);
                BindingOperations.GetBindingExpression(target.Element,
                    target.ValueProperty)?.UpdateTarget();
                target.Element.Focus();
            };

            parent.Children.RemoveAt(childIndex);
            target.Element.Margin = new Thickness(0.0);
            wrapper.Children.Add(target.Element);
            wrapper.Children.Add(resetButton);
            parent.Children.Insert(childIndex, wrapper);
            return true;
        }

        private static void CopyPanelPosition(FrameworkElement source,
            FrameworkElement target)
        {
            Grid.SetRow(target, Grid.GetRow(source));
            Grid.SetColumn(target, Grid.GetColumn(source));
            Grid.SetRowSpan(target, Grid.GetRowSpan(source));
            Grid.SetColumnSpan(target, Grid.GetColumnSpan(source));
            DockPanel.SetDock(target, DockPanel.GetDock(source));
        }

        private sealed class NumericBindingTarget
        {
            public NumericBindingTarget(FrameworkElement element,
                DependencyProperty valueProperty, string settingName)
            {
                Element = element;
                ValueProperty = valueProperty;
                SettingName = settingName;
            }

            public FrameworkElement Element { get; }
            public DependencyProperty ValueProperty { get; }
            public string SettingName { get; }
        }
    }
}
