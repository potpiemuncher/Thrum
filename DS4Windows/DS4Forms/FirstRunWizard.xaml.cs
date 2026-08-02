using DS4WinWPF.DS4Forms.ViewModels;
using System.ComponentModel;
using System.Windows;

namespace DS4WinWPF.DS4Forms
{
    public partial class FirstRunWizard : Window
    {
        private readonly FirstRunWizardViewModel viewModel;
        private bool closeApproved;

        internal FirstRunWizard(FirstRunWizardViewModel viewModel,
            FirstRunWizardEffects effects)
        {
            InitializeComponent();
            this.viewModel = viewModel;
            DataContext = viewModel;
            effects.Owner = this;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) =>
            nextBtn.Focus();

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            FirstRunAdvanceResult result = viewModel.Advance();
            if (result == FirstRunAdvanceResult.Finished ||
                result == FirstRunAdvanceResult.Fatal)
            {
                CloseFromViewModel(result == FirstRunAdvanceResult.Finished);
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e) =>
            viewModel.GoBack();

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            viewModel.Cancel();
            CloseFromViewModel(dialogResult: false);
        }

        private void ViiperInstall_Click(object sender, RoutedEventArgs e)
        {
            if (viewModel.CurrentStep is FirstRunBackendStepViewModel backend)
            {
                backend.InstallOrRepair();
            }
        }

        private void ViiperRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (viewModel.CurrentStep is FirstRunBackendStepViewModel backend)
            {
                backend.Refresh();
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (!closeApproved)
            {
                viewModel.Cancel();
                closeApproved = true;
            }
        }

        private void CloseFromViewModel(bool dialogResult)
        {
            closeApproved = true;
            DialogResult = dialogResult;
        }
    }
}
