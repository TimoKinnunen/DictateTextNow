using DictateTextNow.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace DictateTextNow.Views
{
    public sealed partial class MainPage : Page
    {
        private readonly DispatcherTimer hideTextTimer;

        private int counterIntervalSeconds;

        public static MainPage CurrentMainPage;

        public ListView MenuNavigationListView
        {
            get { return NavigationListView; }
        }
        public MainPage()
        {
            InitializeComponent();

            CurrentMainPage = this;
            GoToHomePage();

            hideTextTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };

            hideTextTimer.Tick += HideTextTimer_Tick;
        }

        private void HideTextTimer_Tick(object sender, object e)
        {
            counterIntervalSeconds++;
            if (counterIntervalSeconds >= 5)
            {
                counterIntervalSeconds = 0;
                //hide text by setting it to empty
                NotifyUser(string.Empty, NotifyType.StatusMessage);
                //after 5 seconds stop the timer
                hideTextTimer.Stop();
            }
        }

        public void GoToHomePage()
        {
            MainFrame.Navigate(typeof(HomePage));
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // code here
            ObservableCollection<NavigationMenuItem> allNavigationMenuItems = GetNavigationMenuItems();

            NavigationMenuCollectionViewSource.Source = allNavigationMenuItems;

            if (allNavigationMenuItems.Count > 0)
            {
                NavigationListView.SelectedItem = allNavigationMenuItems.FirstOrDefault();
            }

            NotifyUser(string.Empty, NotifyType.StatusMessage);
            // code here
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            // code here
            // code here
        }

        private static ObservableCollection<NavigationMenuItem> GetNavigationMenuItems()
        {
            return new ObservableCollection<NavigationMenuItem>(){
                new NavigationMenuItem(){
                    MenuSymbolIcon = Symbol.Home,
                    MenuToolTip = "Home",
                    MenuItemName = "Home"
                },
                new NavigationMenuItem(){
                    MenuSymbolIcon = Symbol.Setting,
                    MenuToolTip = "Instructions",
                    MenuItemName = "Instructions"
                },
                new NavigationMenuItem(){
                    MenuSymbolIcon = Symbol.Setting,
                    MenuToolTip = "About",
                    MenuItemName = "About"
                },
                new NavigationMenuItem(){
                    MenuSymbolIcon = Symbol.Setting,
                    MenuToolTip = "Privacy statement",
                    MenuItemName = "Privacy statement"
                },
            };
        }

        private void HamburgerButton_Click(object sender, RoutedEventArgs e)
        {
            MenuSplitView.IsPaneOpen = MenuSplitView.IsPaneOpen ? false : true;
        }

        private void NavigationListView_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (sender is ListView listView)
            {
                if (listView.SelectedItem is NavigationMenuItem navigationMenuItem)
                {
                    switch (navigationMenuItem.MenuItemName)
                    {
                        case "Home":
                            MainFrame.Navigate(typeof(HomePage));
                            break;
                        case "Instructions":
                            MainFrame.Navigate(typeof(InstructionsPage));
                            break;
                        case "About":
                            MainFrame.Navigate(typeof(AboutPage));
                            break;
                        case "Privacy statement":
                            MainFrame.Navigate(typeof(PrivacyStatementPage));
                            break;
                        default:
                            MainFrame.Navigate(typeof(HomePage));
                            break;
                    }
                    MenuSplitView.IsPaneOpen = false;
                }
            }
        }

        /// <summary>
        /// Used to display messages to the user
        /// </summary>
        /// <param name="strMessage"></param>
        /// <param name="type"></param>
        public void NotifyUser(string strMessage, NotifyType type)
        {
            switch (type)
            {
                case NotifyType.StatusMessage:
                    StatusBorder.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                    break;
                case NotifyType.ErrorMessage:
                    StatusBorder.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                    break;
            }
            StatusTextBlock.Text = strMessage;

            // Collapse the StatusBlock if it has no text to conserve real estate.
            StatusBorder.Visibility = (StatusTextBlock.Text != String.Empty) ? Visibility.Visible : Visibility.Collapsed;
            if (StatusTextBlock.Text != String.Empty)
            {
                StatusBorder.Visibility = Visibility.Visible;
                StatusPanel.Visibility = Visibility.Visible;
            }
            else
            {
                StatusBorder.Visibility = Visibility.Collapsed;
                StatusPanel.Visibility = Visibility.Collapsed;
            }

            // Collapse the StatusTextBlock if it has no text to conserve real estate.
            StatusBorder.Visibility = (StatusTextBlock.Text != string.Empty) ? Visibility.Visible : Visibility.Collapsed;
            if (StatusTextBlock.Text != string.Empty)
            {
                StatusBorder.Visibility = Visibility.Visible;
                StatusBorder.Visibility = Visibility.Visible;

                counterIntervalSeconds = 0;
                if (hideTextTimer != null && !hideTextTimer.IsEnabled)
                {
                    hideTextTimer.Start();
                }
            }
            else
            {
                StatusBorder.Visibility = Visibility.Collapsed;
                StatusBorder.Visibility = Visibility.Collapsed;
            }
        }

        private void StatusBorder_Tapped(object sender, TappedRoutedEventArgs e)
        {
            //hide
            NotifyUser(string.Empty, NotifyType.StatusMessage);
        }
    }

    public enum NotifyType
    {
        StatusMessage,
        ErrorMessage
    };
}
