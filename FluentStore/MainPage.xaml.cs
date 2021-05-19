﻿using FluentStore.Helpers;
using FluentStore.ViewModels;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using System;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using NavigationViewItem = Microsoft.UI.Xaml.Controls.NavigationViewItem;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace FluentStore
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private Services.NavigationService NavService { get; } = Ioc.Default.GetService<Services.INavigationService>() as Services.NavigationService;
        private Services.UserService UserService { get; } = Ioc.Default.GetService<Services.UserService>();
        
        public ShellViewModel ViewModel
        {
            get => (ShellViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }
        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register(nameof(ViewModel), typeof(ShellViewModel), typeof(MainPage), new PropertyMetadata(new ShellViewModel()));

        public MainPage()
        {
            this.InitializeComponent();

            MainFrame.Navigated += MainFrame_Navigated;
            NavigationHelper.PageFrame = MainFrame;
            NavService.CurrentFrame = MainFrame;

            foreach(PageInfo page in NavigationHelper.Pages)
            {
                MainNav.MenuItems.Add(page.NavViewItem);
            }
            MainNav.SelectedItem = MainNav.MenuItems[0];

            Services.UserService.OnLoginStateChanged += UserService_OnLoginStateChanged;
            UserService.TrySignIn(false);
        }

        private void UserService_OnLoginStateChanged(bool isLoggedIn)
        {
            if (isLoggedIn)
            {
                UserButton.Visibility = Visibility.Visible;
                SignInButton.Visibility = Visibility.Collapsed;

                // Show pages that require signin
                foreach (NavigationViewItem navItem in MainNav.MenuItems)
                    if (((PageInfo)navItem.Tag).RequiresSignIn)
                        navItem.Visibility = Visibility.Visible;
            }
            else
            {
                UserButton.Visibility = Visibility.Collapsed;
                SignInButton.Visibility = Visibility.Visible;

                // Hide pages that require signin
                foreach (NavigationViewItem navItem in MainNav.MenuItems)
                    if (((PageInfo)navItem.Tag).RequiresSignIn)
                        navItem.Visibility = Visibility.Collapsed;

                // If the current page requires sign in, navigate away
                if (RequiresSignInAttribute.IsPresent(MainFrame.Content))
                    NavService.Navigate(typeof(Views.HomeView));

                // Remove all pages from the back stack
                // that require authentication
                var backStack = MainFrame.BackStack.ToArray();
                foreach (PageStackEntry entry in backStack)
                {
                    if (RequiresSignInAttribute.IsPresent(entry.SourcePageType))
                        MainFrame.BackStack.Remove(entry);
                }
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            OnBackRequested();
        }

        // Handles system-level BackRequested events and page-level back button Click events
        private bool OnBackRequested()
        {
            if (MainFrame.CanGoBack)
            {
                MainFrame.GoBack();
                return true;
            }
            return false;
        }

        private void BackInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            OnBackRequested();
            args.Handled = true;
        }

        private void MainNav_BackRequested(Microsoft.UI.Xaml.Controls.NavigationView sender, Microsoft.UI.Xaml.Controls.NavigationViewBackRequestedEventArgs args)
        {
            OnBackRequested();
        }

        private async void controlsSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.CheckCurrent() && args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
                await ViewModel.GetSearchSuggestionsAsync();
        }

        private async void controlsSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (args.ChosenSuggestion != null && args.ChosenSuggestion is MicrosoftStore.Models.Product product)
            {
                await ViewModel.SubmitQueryAsync(product);
            }
            else if (!string.IsNullOrEmpty(args.QueryText))
            {
                NavService.Navigate(typeof(Views.SearchResultsView), args.QueryText);
            }
        }
        
        private void CtrlF_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            controlsSearchBox.Focus(FocusState.Programmatic);
        }

        private void MainFrame_Navigated(object sender, NavigationEventArgs e)
        {
            MainNav.IsBackEnabled = MainFrame.CanGoBack;
            try
            {
                // Update the NavView when the frame navigates on its own.
                // This is in a try-catch block so that I don't have to do a dozen
                // null checks.

                // If the user is not signed in but current page requires it, navigate away
                bool requiresSignIn = RequiresSignInAttribute.IsPresent(e.SourcePageType);
                if (requiresSignIn && !UserService.IsLoggedIn)
                {
                    MainNav.SelectedItem = null;
                    return;
                }

                var page = NavigationHelper.Pages.Find((info) => info.PageType == e.SourcePageType);
                if (page == null)
                {
                    MainNav.SelectedItem = null;
                    return;
                }

                MainNav.SelectedItem = MainNav.MenuItems.First(obj => (obj as NavigationViewItem)?.Tag == page);
            }
            catch
            {
                MainNav.SelectedItem = null;
            }
        }

        private void NavigationView_SelectionChanged(Microsoft.UI.Xaml.Controls.NavigationView sender, Microsoft.UI.Xaml.Controls.NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                NavService.Navigate(typeof(Views.SettingsView));
                return;
            }

            if (!(args.SelectedItem is NavigationViewItem navItem))
            {
                NavService.Navigate(typeof(Views.HomeView));
                return;
            }

            PageInfo pageInfo = NavigationHelper.Pages.Find((info) => info == navItem.Tag);
            if (pageInfo == null)
            {
                NavService.Navigate(typeof(Views.HomeView));
                return;
            }

            if (pageInfo.PageType.BaseType == typeof(Page))
                NavService.Navigate(pageInfo.PageType);
        }

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Set titlebar to page header when in compact
            double CompactModeMinWidth = (double)App.Current.Resources["CompactModeMinWidth"];
            if (e.NewSize.Width <= CompactModeMinWidth && e.PreviousSize.Width > CompactModeMinWidth)
            {
                var coreTitleBar = Windows.ApplicationModel.Core.CoreApplication.GetCurrentView().TitleBar;
                coreTitleBar.LayoutMetricsChanged += CoreTitleBar_LayoutMetricsChanged;
                // Set XAML element as a draggable region.
                Window.Current.SetTitleBar(TitlebarGrid);
            }
            else if (e.NewSize.Width > CompactModeMinWidth && e.PreviousSize.Width <= CompactModeMinWidth)
            {
                var coreTitleBar = Windows.ApplicationModel.Core.CoreApplication.GetCurrentView().TitleBar;
                coreTitleBar.LayoutMetricsChanged -= CoreTitleBar_LayoutMetricsChanged;
                // Set XAML element as a draggable region.
                Window.Current.SetTitleBar(null);
            }
        }

        private void CoreTitleBar_LayoutMetricsChanged(Windows.ApplicationModel.Core.CoreApplicationViewTitleBar sender, object args)
        {
            //TitlebarRow.Height = new GridLength(sender.Height);
        }
    }
}
