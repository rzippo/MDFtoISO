﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

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

        public MainPage()
        {
            this.InitializeComponent();

            ApplicationView.PreferredLaunchViewSize = new Size(800, 200);
            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;
        }

        private async void MdfSelectButton_ClickAsync(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker
            {
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder,
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

        }

        private async void ConvertButton_ClickAsync(object sender, RoutedEventArgs e)
        {
            if(MdfFile != null && IsoFile != null)
            {
                await Mdf2IsoConverter.ConvertAsync(MdfFile, IsoFile);

                var dialog = new MessageDialog("Conversion completed!");
                await dialog.ShowAsync();
            }
        }


    }
}
