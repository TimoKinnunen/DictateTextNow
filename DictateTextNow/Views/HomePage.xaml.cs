using DictateTextNow.Helpers;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Globalization;
using Windows.Media.SpeechRecognition;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace DictateTextNow.Views
{
    public sealed partial class HomePage : Page
    {
        private MainPage mainPage;

        // Data to clipboard
        readonly DataPackage dataPackage;

        // The speech recognizer used throughout this sample.
        private SpeechRecognizer speechRecognizer;

        // Keep track of whether the continuous recognizer is currently running, so it can be cleaned up appropriately.
        private bool isListening;

        // Keep track of existing text that we've accepted in ContinuousRecognitionSession_ResultGenerated(), so
        // that we can combine it and Hypothesized results to show in-progress dictation mid-sentence.
        private readonly StringBuilder dictatedTextBuilder;

        /// <summary>
        /// This HResult represents the scenario where a user is prompted to allow in-app speech, but 
        /// declines. This should only happen on a Phone device, where speech is enabled for the entire device,
        /// not per-app.
        /// </summary>
        private static readonly uint HResultPrivacyStatementDeclined = 0x80045509;

        public HomePage()
        {
            InitializeComponent();

            SizeChanged += Page_SizeChanged;
            Loaded += Page_Loaded;

            mainPage = MainPage.CurrentMainPage;

            isListening = false;
            dictatedTextBuilder = new StringBuilder();

            dataPackage = new DataPackage();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            SetPageContentStackPanelWidth();
        }

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            SetPageContentStackPanelWidth();
        }

        /// <summary>
        /// Upon entering the scenario, ensure that we have permissions to use the Microphone. This may entail popping up
        /// a dialog to the user on Desktop systems. Only enable functionality once we've gained that permission in order to 
        /// prevent errors from occurring when using the SpeechRecognizer. If speech is not a primary input mechanism, developers
        /// should consider disabling appropriate parts of the UI if the user does not have a recording device, or does not allow 
        /// audio input.
        /// </summary>
        /// <param name="e">Unused navigation parameters</param>
        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // code here

            mainPage = MainPage.CurrentMainPage;

            // Prompt the user for permission to access the microphone. This request will only happen
            // once, it will not re-prompt if the user rejects the permission.
            bool permissionGained = await AudioCapturePermissions.RequestMicrophonePermission();
            if (permissionGained)
            {
                btnContinuousRecognize.IsEnabled = true;

                PopulateLanguageDropdown();
                await InitializeRecognizer(SpeechRecognizer.SystemSpeechLanguage);
            }
            else
            {
                dictationTextBox.Text = "Permission to access capture resources was not given by the user, reset the application setting in Settings->Privacy->Microphone.";
                btnContinuousRecognize.IsEnabled = false;
                cbLanguageSelection.IsEnabled = false;
            }

            // code here
        }

        /// <summary>
        /// When a user changes the speech recognition language, trigger re-initialization of the 
        /// speech engine with that language, and change any speech-specific UI assets.
        /// </summary>
        /// <param name="sender">Ignored</param>
        /// <param name="e">Ignored</param>
        private async void CbLanguageSelection_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (speechRecognizer != null)
            {
                ComboBoxItem item = (ComboBoxItem)cbLanguageSelection.SelectedItem;
                Language newLanguage = (Language)item.Tag;
                if (speechRecognizer.CurrentLanguage != newLanguage)
                {
                    // trigger cleanup and re-initialization of speech.
                    try
                    {
                        await InitializeRecognizer(newLanguage);
                    }
                    catch (Exception exception)
                    {
                        var messageDialog = new MessageDialog(exception.Message, "Exception");
                        await messageDialog.ShowAsync();
                    }
                }
            }
        }

        /// <summary>
        /// Initialize Speech Recognizer and compile constraints.
        /// </summary>
        /// <param name="recognizerLanguage">Language to use for the speech recognizer</param>
        /// <returns>Awaitable task.</returns>
        private async Task InitializeRecognizer(Language recognizerLanguage)
        {
            if (speechRecognizer != null)
            {
                // cleanup prior to re-initializing this scenario.
                speechRecognizer.StateChanged -= SpeechRecognizer_StateChanged;
                speechRecognizer.ContinuousRecognitionSession.Completed -= ContinuousRecognitionSession_Completed;
                speechRecognizer.ContinuousRecognitionSession.ResultGenerated -= ContinuousRecognitionSession_ResultGenerated;
                speechRecognizer.HypothesisGenerated -= SpeechRecognizer_HypothesisGenerated;

                speechRecognizer.Dispose();
                speechRecognizer = null;
            }

            speechRecognizer = new SpeechRecognizer(recognizerLanguage);

            // Provide feedback to the user about the state of the recognizer. This can be used to provide visual feedback in the form
            // of an audio indicator to help the user understand whether they're being heard.
            speechRecognizer.StateChanged += SpeechRecognizer_StateChanged;

            // Apply the dictation topic constraint to optimize for dictated freeform speech.
            var dictationConstraint = new SpeechRecognitionTopicConstraint(SpeechRecognitionScenario.Dictation, "dictation");
            speechRecognizer.Constraints.Add(dictationConstraint);
            SpeechRecognitionCompilationResult result = await speechRecognizer.CompileConstraintsAsync();
            if (result.Status != SpeechRecognitionResultStatus.Success)
            {
                mainPage.NotifyUser("Grammar compilation failed: " + result.Status.ToString(), NotifyType.ErrorMessage);
                btnContinuousRecognize.IsEnabled = false;
            }

            // Handle continuous recognition events. Completed fires when various error states occur. ResultGenerated fires when
            // some recognized phrases occur, or the garbage rule is hit. HypothesisGenerated fires during recognition, and
            // allows us to provide incremental feedback based on what the user's currently saying.
            speechRecognizer.ContinuousRecognitionSession.Completed += ContinuousRecognitionSession_Completed;
            speechRecognizer.ContinuousRecognitionSession.ResultGenerated += ContinuousRecognitionSession_ResultGenerated;
            speechRecognizer.HypothesisGenerated += SpeechRecognizer_HypothesisGenerated;
        }

        /// <summary>
        /// Upon leaving, clean up the speech recognizer. Ensure we aren't still listening, and disable the event 
        /// handlers to prevent leaks.
        /// </summary>
        /// <param name="e">Unused navigation parameters.</param>
        protected async override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            // code here

            if (speechRecognizer != null)
            {
                if (isListening)
                {
                    await speechRecognizer.ContinuousRecognitionSession.CancelAsync();
                    isListening = false;
                    DictationButtonText.Text = " Dictate";
                    cbLanguageSelection.IsEnabled = true;
                }

                dictationTextBox.Text = "";

                speechRecognizer.ContinuousRecognitionSession.Completed -= ContinuousRecognitionSession_Completed;
                speechRecognizer.ContinuousRecognitionSession.ResultGenerated -= ContinuousRecognitionSession_ResultGenerated;
                speechRecognizer.HypothesisGenerated -= SpeechRecognizer_HypothesisGenerated;
                speechRecognizer.StateChanged -= SpeechRecognizer_StateChanged;

                speechRecognizer.Dispose();
                speechRecognizer = null;
            }

            // code here
        }

        /// <summary>
        /// Look up the supported languages for this speech recognition scenario, 
        /// that are installed on this machine, and populate a dropdown with a list.
        /// </summary>
        private void PopulateLanguageDropdown()
        {
            Language defaultLanguage = SpeechRecognizer.SystemSpeechLanguage;
            IEnumerable<Language> supportedLanguages = SpeechRecognizer.SupportedTopicLanguages;
            foreach (Language lang in supportedLanguages)
            {
                ComboBoxItem item = new ComboBoxItem
                {
                    Tag = lang,
                    Content = lang.DisplayName
                };

                cbLanguageSelection.Items.Add(item);
                if (lang.LanguageTag == defaultLanguage.LanguageTag)
                {
                    item.IsSelected = true;
                    cbLanguageSelection.SelectedItem = item;
                }
            }
        }

        /// <summary>
        /// Handle events fired when error conditions occur, such as the microphone becoming unavailable, or if
        /// some transient issues occur.
        /// </summary>
        /// <param name="sender">The continuous recognition session</param>
        /// <param name="args">The state of the recognizer</param>
        private async void ContinuousRecognitionSession_Completed(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionCompletedEventArgs args)
        {
            if (args.Status != SpeechRecognitionResultStatus.Success)
            {
                // If TimeoutExceeded occurs, the user has been silent for too long. We can use this to 
                // cancel recognition if the user in dictation mode and walks away from their device, etc.
                // In a global-command type scenario, this timeout won't apply automatically.
                // With dictation (no grammar in place) modes, the default timeout is 20 seconds.
                if (args.Status == SpeechRecognitionResultStatus.TimeoutExceeded)
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        mainPage.NotifyUser("Automatic time out of dictation", NotifyType.StatusMessage);
                        DictationButtonText.Text = " Dictate";
                        cbLanguageSelection.IsEnabled = true;
                        dictationTextBox.Text = dictatedTextBuilder.ToString();
                        isListening = false;
                    });
                }
                else
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        mainPage.NotifyUser("Continuous recognition completed: " + args.Status.ToString(), NotifyType.StatusMessage);
                        DictationButtonText.Text = " Dictate";
                        cbLanguageSelection.IsEnabled = true;
                        isListening = false;
                    });
                }
            }
        }

        /// <summary>
        /// While the user is speaking, update the textbox with the partial sentence of what's being said for user feedback.
        /// </summary>
        /// <param name="sender">The recognizer that has generated the hypothesis</param>
        /// <param name="args">The hypothesis formed</param>
        private async void SpeechRecognizer_HypothesisGenerated(SpeechRecognizer sender, SpeechRecognitionHypothesisGeneratedEventArgs args)
        {
            string hypothesis = args.Hypothesis.Text;
            string textboxContent = await AppendTextboxContent(hypothesis);
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                dictationTextBox.Text = textboxContent;
                btnClearText.IsEnabled = true;
                btnCopyText.IsEnabled = true;
            });
        }

        private Task<string> AppendTextboxContent(string hypothesis)
        {
            // Update the textbox with the currently confirmed text, and the hypothesis combined.
            return Task.FromResult(dictatedTextBuilder.ToString() + " " + hypothesis + " ...");
        }

        /// <summary>
        /// Handle events fired when a result is generated. Check for high to medium confidence, and then append the
        /// string to the end of the stringbuffer, and replace the content of the textbox with the string buffer, to
        /// remove any hypothesis text that may be present.
        /// </summary>
        /// <param name="sender">The Recognition session that generated this result</param>
        /// <param name="args">Details about the recognized speech</param>
        private async void ContinuousRecognitionSession_ResultGenerated(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionResultGeneratedEventArgs args)
        {
            // We may choose to discard content that has low confidence, as that could indicate that we're picking up
            // noise via the microphone, or someone could be talking out of earshot.
            if (args.Result.Confidence == SpeechRecognitionConfidence.Medium ||
                args.Result.Confidence == SpeechRecognitionConfidence.High)
            {
                dictatedTextBuilder.Append(args.Result.Text + " ");

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    discardedTextBlock.Visibility = Visibility.Collapsed;

                    dictationTextBox.Text = dictatedTextBuilder.ToString();
                    btnClearText.IsEnabled = true;
                    btnCopyText.IsEnabled = true;
                });
            }
            else
            {
                // In some scenarios, a developer may choose to ignore giving the user feedback in this case, if speech
                // is not the primary input mechanism for the application.
                // Here, just remove any hypothesis text by resetting it to the last known good.
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    dictationTextBox.Text = dictatedTextBuilder.ToString();
                    string discardedText = args.Result.Text;
                    if (!string.IsNullOrEmpty(discardedText))
                    {
                        discardedText = discardedText.Length <= 25 ? discardedText : (discardedText.Substring(0, 25) + "...");

                        discardedTextBlock.Text = "Discarded due to low/rejected confidence: " + discardedText;
                        discardedTextBlock.Visibility = Visibility.Visible;
                    }
                });
            }
        }

        /// <summary>
        /// Provide feedback to the user based on whether the recognizer is receiving their voice input.
        /// </summary>
        /// <param name="sender">The recognizer that is currently running.</param>
        /// <param name="args">The current state of the recognizer.</param>
        private async void SpeechRecognizer_StateChanged(SpeechRecognizer sender, SpeechRecognizerStateChangedEventArgs args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                mainPage.NotifyUser(args.State.ToString(), NotifyType.StatusMessage);
            });
        }

        /// <summary>
        /// Begin recognition, or finish the recognition session. 
        /// </summary>
        /// <param name="sender">The button that generated this event</param>
        /// <param name="e">Unused event details</param>
        public async void ContinuousRecognize_Click(object sender, RoutedEventArgs e)
        {
            btnContinuousRecognize.IsEnabled = false;
            if (isListening == false)
            {
                // The recognizer can only start listening in a continuous fashion if the recognizer is currently idle.
                // This prevents an exception from occurring.
                if (speechRecognizer.State == SpeechRecognizerState.Idle)
                {
                    DictationButtonText.Text = " Stop dictation";
                    cbLanguageSelection.IsEnabled = false;
                    hlOpenPrivacySettings.Visibility = Visibility.Collapsed;
                    discardedTextBlock.Visibility = Visibility.Collapsed;

                    try
                    {
                        isListening = true;
                        await speechRecognizer.ContinuousRecognitionSession.StartAsync();
                    }
                    catch (Exception ex)
                    {
                        if ((uint)ex.HResult == HResultPrivacyStatementDeclined)
                        {
                            // Show a UI link to the privacy settings.
                            hlOpenPrivacySettings.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            var messageDialog = new MessageDialog(ex.Message, "Exception");
                            await messageDialog.ShowAsync();
                        }

                        isListening = false;
                        DictationButtonText.Text = " Dictate";
                        cbLanguageSelection.IsEnabled = true;

                    }
                }
            }
            else
            {
                isListening = false;
                DictationButtonText.Text = " Dictate";
                cbLanguageSelection.IsEnabled = true;

                if (speechRecognizer.State != SpeechRecognizerState.Idle)
                {
                    // Cancelling recognition prevents any currently recognized speech from
                    // generating a ResultGenerated event. StopAsync() will allow the final session to 
                    // complete.
                    try
                    {
                        await speechRecognizer.ContinuousRecognitionSession.StopAsync();

                        // Ensure we don't leave any hypothesis text behind
                        dictationTextBox.Text = dictatedTextBuilder.ToString();
                    }
                    catch (Exception exception)
                    {
                        var messageDialog = new MessageDialog(exception.Message, "Exception");
                        await messageDialog.ShowAsync();
                    }
                }
            }
            btnContinuousRecognize.IsEnabled = true;
        }

        /// <summary>
        /// Clear the dictation textbox.
        /// </summary>
        /// <param name="sender">Ignored</param>
        /// <param name="e">Ignored</param>
        private void BtnClearText_Click(object sender, RoutedEventArgs e)
        {
            btnClearText.IsEnabled = false;
            btnCopyText.IsEnabled = false;
            dictatedTextBuilder.Clear();
            dictationTextBox.Text = "";
            discardedTextBlock.Visibility = Visibility.Collapsed;


            // Avoid setting focus on the text box, since it's a non-editable control.
            btnContinuousRecognize.Focus(FocusState.Programmatic);
        }

        private void BtnCopyText_Click(object sender, RoutedEventArgs e)
        {
            dataPackage.SetText(dictationTextBox.Text);
            Clipboard.SetContent(dataPackage);
        }

        /// <summary>
        /// Automatically scroll the textbox down to the bottom whenever new dictated text arrives
        /// </summary>
        /// <param name="sender">The dictation textbox</param>
        /// <param name="e">Unused text changed arguments</param>
        private void DictationTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var grid = (Grid)VisualTreeHelper.GetChild(dictationTextBox, 0);
            for (var i = 0; i <= VisualTreeHelper.GetChildrenCount(grid) - 1; i++)
            {
                object obj = VisualTreeHelper.GetChild(grid, i);
                if (!(obj is ScrollViewer))
                {
                    continue;
                }

                ((ScrollViewer)obj).ChangeView(0.0f, ((ScrollViewer)obj).ExtentHeight, 1.0f);
                break;
            }
        }

        /// <summary>
        /// Open the Speech, Inking and Typing page under Settings -> Privacy, enabling a user to accept the 
        /// Microsoft Privacy Policy, and enable personalization.
        /// </summary>
        /// <param name="sender">Ignored</param>
        /// <param name="args">Ignored</param>
        private async void OpenPrivacySettings_Click(Hyperlink sender, HyperlinkClickEventArgs args)
        {
            await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:privacy-speechtyping"));
        }

        private void SetPageContentStackPanelWidth()
        {
            PageContentStackPanel.Width = ActualWidth -
                PageContentScrollViewer.Margin.Left -
                PageContentScrollViewer.Padding.Right;
        }

        #region MenuAppBarButton
        private void HomeAppBarButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            mainPage.GoToHomePage();
            mainPage.MenuNavigationListView.SelectedIndex = 0;
        }
        #endregion MenuAppBarButton

    }
}
