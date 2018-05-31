using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Mdf2IsoUWP
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public StorageFile MdfFile { get; set; } = null;
        public StorageFile IsoFile { get; set; } = null;

        private CancellationTokenSource TokenSource = new CancellationTokenSource();

        public MainPage()
        {
            this.InitializeComponent();

            ApplicationView.PreferredLaunchViewSize = new Size(700, 500);
            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;
        }

        private async void MdfSelectButton_ClickAsync(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker
            {
                FileTypeFilter = {".mdf"}
            };

            StorageFile file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                MdfFile = file;
                MdfPathBox.Text = file.Name;
            }
        }

        private async void IsoSelectButton_ClickAsync(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FileSavePicker
            {
                SuggestedFileName = MdfFile?.DisplayName ?? ""
            };
            picker.FileTypeChoices.Add("ISO image file", new List<String>(){ ".iso" });

            StorageFile file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                IsoFile = file;
                IsoPathBox.Text = file.Name;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            TokenSource.Cancel();
            TokenSource = new CancellationTokenSource();
        }

        private async void ConvertButton_ClickAsync(object sender, RoutedEventArgs e)
        {
            if(MdfFile != null && IsoFile != null)
            {
                ConversionProgressBar.Visibility = Visibility.Visible;
                var progress = new Progress<int>(
                    percent => ConversionProgressBar.Value = percent );

                CancelButton.IsEnabled = true;
                ConvertButton.IsEnabled = false;
                
                await Task.Run(() => Mdf2IsoConverter.ConvertAsync(
                    MdfFile,
                    IsoFile,
                    progress,
                    LogViewer.LogWriter,
                    token: TokenSource.Token
                    ).Wait());

                string message = (ConversionProgressBar.Value == ConversionProgressBar.Maximum) ? "Conversion completed!" : "Conversion terminated, see log for details.";
                var dialog = new MessageDialog(message);
                await dialog.ShowAsync();

                CancelButton.IsEnabled = false;
                ConvertButton.IsEnabled = true;
            }
        }

        private async void Info_OnClick(object sender, RoutedEventArgs e)
        {
            var infoDialog = new InfoDialog();
            await infoDialog.ShowAsync();
        }

        private void ShowLogCheck_Change(object sender, RoutedEventArgs e)
        {
            if (ShowLogCheck.IsChecked ??  false)
                LogViewer.Visibility = Visibility.Visible;
            else
                LogViewer.Visibility = Visibility.Collapsed;
        }
    }
}
