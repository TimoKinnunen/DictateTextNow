using System;
using Windows.Storage;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace DictateTextNow.Views
{
    public sealed partial class InstructionsPage : Page
    {
        private readonly MainPage mainPage;

        public InstructionsPage()
        {
            InitializeComponent();

            SizeChanged += Page_SizeChanged;
            Loaded += Page_Loaded;

            mainPage = MainPage.CurrentMainPage;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            SetPageContentStackPanelWidth();
        }

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            SetPageContentStackPanelWidth();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // code here
            // code here
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            // code here
            // code here
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

        private async void PdfButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var uri = new Uri("ms-appx:///Instruction/DictateTextNowInstructions.pdf");
            var file = await StorageFile.GetFileFromApplicationUriAsync(uri);
            bool success = await Launcher.LaunchFileAsync(file);
        }

    }
}