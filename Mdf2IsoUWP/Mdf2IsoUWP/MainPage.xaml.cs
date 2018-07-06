using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Services.Store;
using Microsoft.Services.Store.Engagement;
using Windows.Storage;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Mdf2IsoUWP
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public StorageFile MdfFile { get; set; }
        public StorageFile IsoFile { get; set; }

        private readonly Progress<int> progress;

        private CancellationTokenSource tokenSource = new CancellationTokenSource();

        private readonly StoreServicesCustomEventLogger logger = StoreServicesCustomEventLogger.GetDefault();

        public MainPage()
        {
            InitializeComponent();

            #if DEBUG
            ApplicationData.Current.ClearAsync(ApplicationDataLocality.Roaming).AsTask().Wait();
            #endif

            progress = new Progress<int>(
                percent => ConversionProgressBar.Value = percent);

            ApplicationView.PreferredLaunchViewSize = new Size(700, 500);
            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if(e.Parameter is IActivatedEventArgs args)
            {
                if(args.Kind == ActivationKind.File)
                {
                    if (args is FileActivatedEventArgs fileArgs)
                    {
                        MdfFile = (StorageFile) fileArgs.Files[0];
                        MdfPathBox.Text = MdfFile.Name;
                    }
                }
            }
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
            picker.FileTypeChoices.Add("ISO image file", new List<string>(){ ".iso" });

            StorageFile file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                IsoFile = file;
                IsoPathBox.Text = file.Name;
            }
        }

        private void ClearMdfFile()
        {
            MdfFile = null;
            MdfPathBox.Text = "";
        }

        private void ClearIsoFile()
        {
            IsoFile = null;
            IsoPathBox.Text = "";
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            tokenSource.Cancel();
            tokenSource.Dispose();
            tokenSource = new CancellationTokenSource();
        }

        private async void ConvertButton_ClickAsync(object sender, RoutedEventArgs e)
        {
            if(MdfFile != null && IsoFile != null)
            {
                ConversionProgressBar.Visibility = Visibility.Visible;
                
                CancelButton.IsEnabled = true;
                ConvertButton.IsEnabled = false;
                
                ConversionResult conversionResult = await Task.Run(() => Mdf2IsoConverter.ConvertAsync(
                    MdfFile,
                    IsoFile,
                    progress,
                    LogViewer.LogWriter,
                    token: tokenSource.Token
                ));

                await ProcessConversionResult(conversionResult);
            }
        }

        private async Task ShowSimpleContentDialog(string message, string closeButtonText = "Ok", string title = null)
        {
            ContentDialog contentDialog = new ContentDialog
            {
                Content = message
            };

            if (title != null)
                contentDialog.Title = title;

            if (ApiInformation.IsPropertyPresent("Windows.UI.Xaml.Controls.ContentDialog", nameof(ContentDialog.CloseButtonText)))
            {
                contentDialog.CloseButtonText = closeButtonText;
            }
            else
            {
                contentDialog.SecondaryButtonText = closeButtonText;
            }

            await contentDialog.ShowAsync();
        }

        private async Task ProcessConversionResult(ConversionResult conversionResult)
        {
            switch (conversionResult)
            {
                case ConversionResult.Success:
                    logger.Log("Conversion.Success");
                    await ShowSimpleContentDialog("Conversion completed!");
                    break;

                case ConversionResult.AlreadyIso:
                    await ProcessAlreadyIso();
                    break;

                case ConversionResult.FormatNotSupported:
                    logger.Log("Conversion.InputFormatNotSupported");
                    await ShowSimpleContentDialog(
                        title: "Input file format is not supported",
                        message:
                        "Note that.mdf extension is also used for other file types, like SQL databases, which cannot be converted to iso. " +
                        "Still, some mdf disk image formats are not yet supported. Future updates will extend support.",
                        closeButtonText: "Ok"
                    );
                    break;

                case ConversionResult.ConversionCanceled:
                    logger.Log("Conversion.Canceled");
                    await ShowSimpleContentDialog("Conversion canceled by user.");
                    break;

                case ConversionResult.IoException:
                    logger.Log("Conversion.IOException");
                    await ShowSimpleContentDialog("Exception while accessing files. Conversion aborted.");
                    break;

                default:
                    logger.Log("Conversion.FailedUnknown");
                    await ShowSimpleContentDialog("Conversion failed.");
                    break;
            }

            if (conversionResult == ConversionResult.Success)
            {
                await AskForReview();
                ClearMdfFile();
                ClearIsoFile();
            }

            CancelButton.IsEnabled = false;
            ConvertButton.IsEnabled = true;
        }

        private async Task ProcessAlreadyIso()
        {
            logger.Log("Conversion.InputAlreadyISO");
            ContentDialog alreadyIsoChoiceDialog = new ContentDialog
            {
                Title = "Input file is already in ISO format",
                Content = "You can change the extension directly to .iso, or copy its content to the new file",
                PrimaryButtonText = "Copy it as a new .iso file",
            };

            //Move option is available only on 1703 and later, beacause of number of buttons

            bool isCloseButtonTextSupported = ApiInformation.IsPropertyPresent("Windows.UI.Xaml.Controls.ContentDialog",
                nameof(ContentDialog.CloseButtonText));

            if (isCloseButtonTextSupported)
            {
                alreadyIsoChoiceDialog.SecondaryButtonText = "Just rename the .mdf file to .iso";
                alreadyIsoChoiceDialog.CloseButtonText = "Cancel";
            }
            else
            {
                alreadyIsoChoiceDialog.SecondaryButtonText = "Cancel";
            }

            ContentDialogResult choiceDialogResult = await alreadyIsoChoiceDialog.ShowAsync();
            switch (choiceDialogResult)
            {
                case ContentDialogResult.Primary:
                    CopyResult copyResult = await Task.Run(() => Mdf2IsoConverter.CopyAsync(
                        MdfFile,
                        IsoFile,
                        progress,
                        LogViewer.LogWriter,
                        token: tokenSource.Token
                    ));
                    await ProcessCopyResult(copyResult);
                    break;
                
                case ContentDialogResult.Secondary:
                    if (isCloseButtonTextSupported)
                    {
                        bool renameResult = await ChangeExtension();
                        await ProcessRenameResult(renameResult);
                    }
                    else
                    {
                        logger.Log("AlreadyIso.DoNothing");
                    }
                    break;

                case ContentDialogResult.None:
                    logger.Log("AlreadyIso.DoNothing");
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private async Task ProcessRenameResult(bool renameResult)
        {
            if (renameResult)
            {
                logger.Log("AlreadyIso.ChangeExtension.Success");
                await ShowSimpleContentDialog("Rename completed!");
            }
            else
            {
                logger.Log("AlreadyIso.ChangeExtension.Exception");
                await ShowSimpleContentDialog("Exception while accessing files. Rename aborted.");
            }

            if (renameResult == true)
            {
                await AskForReview();
                ClearIsoFile();
                ClearMdfFile();
            }
        }

        private async Task<bool> ChangeExtension()
        {
            try
            {
                await IsoFile.DeleteAsync();
                ClearIsoFile();

                string nextName = MdfFile.DisplayName + ".iso";
                await MdfFile.RenameAsync(nextName, NameCollisionOption.GenerateUniqueName);
                await ShowSimpleContentDialog($"Input file renamed to {nextName}");
                ClearMdfFile();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task ProcessCopyResult(CopyResult copyResult)
        {
            switch (copyResult)
            {
                case CopyResult.Success:
                    logger.Log("AlreadyIso.CopySuccess");
                    await ShowSimpleContentDialog("Copy completed!");
                    break;

                case CopyResult.IoException:
                    logger.Log("AlreadyIso.CopyIOException");
                    await ShowSimpleContentDialog("Exception while accessing files. Copy aborted.");
                    break;

                case CopyResult.CopyCanceled:
                    logger.Log("AlreadyIso.CopyCanceled");
                    await ShowSimpleContentDialog("Copy canceled by user.");
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(copyResult), copyResult, null);
            }

            if (copyResult == CopyResult.Success)
            {
                await AskForReview();
                ClearIsoFile();
                ClearMdfFile();
            }
        }

        private async Task AskForReview()
        {
            object hasGivenReviewObject = ApplicationData.Current.RoamingSettings.Values["HasGivenReview"];
            bool hasGivenReview = hasGivenReviewObject is bool b && b;

            if (!hasGivenReview)
            {
                object popupRefusedCountObject = ApplicationData.Current.RoamingSettings.Values["popupRefusedCount"];
                int popupRefusedCount = popupRefusedCountObject is int i ? i : 1;
                if(popupRefusedCount > 1)
                {
                    Random rng = new Random();
                    if (rng.NextDouble() > 1.0 / popupRefusedCount)
                    {
                        return;
                    }
                }

                ContentDialog wantToReviewDialog = new ContentDialog
                {
                    Title = "Are you liking MDF to ISO?",
                    Content = "Please consider to leave a feedback on the Store",
                    PrimaryButtonText = "Rate now",
                };

                if (ApiInformation.IsPropertyPresent("Windows.UI.Xaml.Controls.ContentDialog",
                    nameof(ContentDialog.CloseButtonText)))
                {
                    wantToReviewDialog.CloseButtonText = "Later";
                }
                else
                {
                    wantToReviewDialog.SecondaryButtonText = "Later";
                } 

                ContentDialogResult result = await wantToReviewDialog.ShowAsync();
                if(result == ContentDialogResult.Primary)
                {
                    bool reviewResult = await ShowRatingReviewDialog();
                    ApplicationData.Current.RoamingSettings.Values["HasGivenReview"] = reviewResult;
                }
                else
                {
                    logger.Log("Review.Refused");
                    ApplicationData.Current.RoamingSettings.Values["popupRefusedCount"] = popupRefusedCount + 1;
                }
            }
        }

        public async Task<bool> ShowRatingReviewDialog()
        {
            if(ApiInformation.IsMethodPresent(typeof(StoreRequestHelper).FullName, nameof(StoreRequestHelper.SendRequestAsync), 3))
            {
                logger.Log("Review.SendRequest");
                StoreSendRequestResult result = await StoreRequestHelper.SendRequestAsync(
                    StoreContext.GetDefault(), 16, string.Empty);

                if (result.ExtendedError == null)
                {
                    JObject jsonObject = JObject.Parse(result.Response);
                    if (jsonObject.SelectToken("status").ToString() == "success")
                    {
                        // The customer rated or reviewed the app.
                        return true;
                    }
                }

                // There was an error with the request, or the customer chose not to
                // rate or review the app.
                return false;
            }
            else
            {
                logger.Log("Review.LaunchUri");
                bool reviewResult = await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-windows-store://review/?ProductId=9PCZBVLLDSX4"));
                return reviewResult;
            }
        }

        private async void Info_OnClick(object sender, RoutedEventArgs e)
        {
            logger.Log("InfoPanel.Opened");
            var infoDialog = new InfoDialog();
            await infoDialog.ShowAsync();
        }

        private void ShowLogCheck_Change(object sender, RoutedEventArgs e)
        {
            if (ShowLogCheck.IsChecked ?? false)
            {
                logger.Log("Log.Shown");
                LogViewer.Visibility = Visibility.Visible;
            }
            else
            {
                logger.Log("Log.Hidden");
                LogViewer.Visibility = Visibility.Collapsed;
            }
        }
    }
}
