using System;
using System.Windows;
using DS4Windows;
using DS4WinWPF.DS4Forms.ViewModels;

namespace DS4WinWPF.DS4Forms;

public partial class StickCalibrationWindow : Window
{
    private Stick _stick;
    private int _device;
    private readonly Action<Stick, sbyte, sbyte> _saveOffsets;

    public StickCalibrationWindow(Stick stick, int device, ProfileSettingsViewModel profileSettingsVm)
        : this(stick, device, (selectedStick, xOffset, yOffset) =>
        {
            if (selectedStick == Stick.Left)
            {
                profileSettingsVm.LeftStickDriftXAxis = xOffset;
                profileSettingsVm.LeftStickDriftYAxis = yOffset;
            }
            else
            {
                profileSettingsVm.RightStickDriftXAxis = xOffset;
                profileSettingsVm.RightStickDriftYAxis = yOffset;
            }
        })
    {
    }

    internal StickCalibrationWindow(Stick stick, int device,
        Action<Stick, sbyte, sbyte> saveOffsets)
    {
        _stick = stick;
        _device = device;
        _saveOffsets = saveOffsets ??
            throw new ArgumentNullException(nameof(saveOffsets));
        InitializeComponent();
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        var state = App.rootHub.getDS4State(_device);

        const int neutralState = 128;
        if (_stick == Stick.Left)
        {
            var xAxisDrift = state.LX - neutralState;
            var yAxisDrift = state.LY - neutralState;

            _saveOffsets(_stick, Convert.ToSByte(xAxisDrift),
                Convert.ToSByte(yAxisDrift));

            MessageBox.Show($"Detected drift:\nX axis: {xAxisDrift}, Y axis: {yAxisDrift}",
                ProductInfo.ProductName, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        if (_stick == Stick.Right)
        {
            var xAxisDrift = state.RX - neutralState;
            var yAxisDrift = state.RY - neutralState;

            _saveOffsets(_stick, Convert.ToSByte(xAxisDrift),
                Convert.ToSByte(yAxisDrift));

            MessageBox.Show($"Detected drift:\nX axis: {xAxisDrift}, Y axis: {yAxisDrift}",
                ProductInfo.ProductName, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        Close();
    }
}

public enum Stick
{
    Left,
    Right
}
