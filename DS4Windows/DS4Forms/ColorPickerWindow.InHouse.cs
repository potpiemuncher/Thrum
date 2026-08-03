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
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace DS4WinWPF.DS4Forms
{
    public partial class ColorPickerWindow
    {
        private readonly Color[] presetColors =
        {
            Colors.Black, Colors.White, Colors.Gray, Colors.Red,
            Colors.Orange, Colors.Yellow, Colors.LimeGreen, Colors.Green,
            Colors.Cyan, Colors.DeepSkyBlue, Colors.Blue, Colors.Purple,
            Colors.Magenta, Colors.DeepPink, Colors.Brown, Colors.Gold,
        };

        private Slider redSlider;
        private Slider greenSlider;
        private Slider blueSlider;
        private Border preview;
        private TextBlock hexValue;
        private Color selectedColor;
        private bool synchronizing;

        public Color SelectedColor
        {
            get => selectedColor;
            set => ApplySelectedColor(value, false);
        }

        private void BuildColorPicker()
        {
            root.Margin = new Thickness(18);
            root.RowDefinitions.Add(new RowDefinition
            {
                Height = GridLength.Auto,
            });
            root.RowDefinitions.Add(new RowDefinition
            {
                Height = GridLength.Auto,
            });
            root.RowDefinitions.Add(new RowDefinition
            {
                Height = GridLength.Auto,
            });
            root.RowDefinitions.Add(new RowDefinition
            {
                Height = GridLength.Auto,
            });
            root.RowDefinitions.Add(new RowDefinition
            {
                Height = GridLength.Auto,
            });
            root.RowDefinitions.Add(new RowDefinition());

            preview = new Border
            {
                Height = 76,
                CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 14),
            };
            preview.SetResourceReference(Border.BorderBrushProperty,
                "BorderColor");
            AutomationProperties.SetName(preview, "Selected color preview");
            root.Children.Add(preview);

            UniformGrid palette = new UniformGrid
            {
                Columns = 8,
                Margin = new Thickness(0, 0, 0, 18),
            };
            Grid.SetRow(palette, 1);
            foreach (Color color in presetColors)
            {
                Button swatch = new Button
                {
                    Background = new SolidColorBrush(color),
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(2),
                    MinHeight = 30,
                    Tag = color,
                    ToolTip = ColorName(color),
                };
                swatch.SetResourceReference(Border.BorderBrushProperty,
                    "BorderColor");
                AutomationProperties.SetName(swatch,
                    $"Select {ColorName(color)}");
                swatch.Click += Preset_Click;
                palette.Children.Add(swatch);
            }
            root.Children.Add(palette);

            redSlider = AddChannel("R", "Red channel", 2);
            greenSlider = AddChannel("G", "Green channel", 3);
            blueSlider = AddChannel("B", "Blue channel", 4);

            hexValue = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 16, 0, 0),
            };
            AutomationProperties.SetName(hexValue, "Selected RGB color");
            Grid.SetRow(hexValue, 5);
            root.Children.Add(hexValue);
        }

        private Slider AddChannel(string shortName, string automationName,
            int row)
        {
            Grid channel = new Grid
            {
                Margin = new Thickness(0, 3, 0, 3),
            };
            channel.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(24),
            });
            channel.ColumnDefinitions.Add(new ColumnDefinition());
            channel.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(48),
            });

            TextBlock label = new TextBlock
            {
                Text = shortName,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
            };
            channel.Children.Add(label);

            Slider slider = new Slider
            {
                Minimum = 0,
                Maximum = 255,
                TickFrequency = 1,
                IsSnapToTickEnabled = true,
                VerticalAlignment = VerticalAlignment.Center,
            };
            AutomationProperties.SetName(slider, automationName);
            slider.ValueChanged += Channel_ValueChanged;
            Grid.SetColumn(slider, 1);
            channel.Children.Add(slider);

            TextBlock value = new TextBlock
            {
                Width = 40,
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
            };
            value.SetBinding(TextBlock.TextProperty,
                new System.Windows.Data.Binding(nameof(Slider.Value))
                {
                    Source = slider,
                    StringFormat = "F0",
                });
            Grid.SetColumn(value, 2);
            channel.Children.Add(value);

            Grid.SetRow(channel, row);
            root.Children.Add(channel);
            return slider;
        }

        private void ApplySelectedColor(Color color, bool notify)
        {
            selectedColor = Color.FromRgb(color.R, color.G, color.B);
            if (redSlider == null)
            {
                return;
            }

            synchronizing = true;
            try
            {
                redSlider.Value = selectedColor.R;
                greenSlider.Value = selectedColor.G;
                blueSlider.Value = selectedColor.B;
                UpdatePresentation();
            }
            finally
            {
                synchronizing = false;
            }

            if (notify)
            {
                ColorChanged?.Invoke(this, selectedColor);
            }
        }

        private void Channel_ValueChanged(object sender,
            RoutedPropertyChangedEventArgs<double> e)
        {
            if (synchronizing || redSlider == null || greenSlider == null ||
                blueSlider == null)
            {
                return;
            }

            selectedColor = Color.FromRgb(
                (byte)Math.Round(redSlider.Value),
                (byte)Math.Round(greenSlider.Value),
                (byte)Math.Round(blueSlider.Value));
            UpdatePresentation();
            ColorChanged?.Invoke(this, selectedColor);
        }

        private void Preset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Color color)
            {
                ApplySelectedColor(color, true);
            }
        }

        private void UpdatePresentation()
        {
            preview.Background = new SolidColorBrush(selectedColor);
            hexValue.Text = $"#{selectedColor.R:X2}{selectedColor.G:X2}{selectedColor.B:X2}";
        }

        private static string ColorName(Color color)
        {
            foreach (var property in typeof(Colors).GetProperties())
            {
                if (property.PropertyType == typeof(Color) &&
                    property.GetValue(null) is Color named && named == color)
                {
                    return property.Name;
                }
            }

            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }
    }
}
