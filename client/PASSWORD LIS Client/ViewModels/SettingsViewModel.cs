﻿using PASSWORD_LIS_Client.Commands;
using PASSWORD_LIS_Client.Properties;
using PASSWORD_LIS_Client.Services;
using PASSWORD_LIS_Client.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Input;


namespace PASSWORD_LIS_Client.ViewModels
{
    public class SettingsViewModel : BaseViewModel
    {
        private readonly IWindowService windowService;
        private readonly IFriendsManagerService friendsManagerService;
        private readonly BackgroundMusicService backgroundMusicService;
        private bool isEnglishSelected;
        public bool IsEnglishSelected
        {
            get => isEnglishSelected;
            set
            {
                if (SetProperty(ref isEnglishSelected, value) && value)
                {
                    UpdateLanguage("en-US");
                }
            }
        }

        private bool isSpanishSelected;
        public bool IsSpanishSelected
        {
            get => isSpanishSelected;
            set
            {
                if (SetProperty(ref isSpanishSelected, value) && value)
                {
                    UpdateLanguage("es-MX");
                }
            }
        }
        private double musicVolume;
        private const double DoubleComparisonEpsilon = 0.0001;

        public double MusicVolume
        {
            get => musicVolume;
            set
            {
                if (Math.Abs(musicVolume - value) > DoubleComparisonEpsilon)
                {
                    musicVolume = value;
                    backgroundMusicService.Volume = value;
                    OnPropertyChanged();
                }
            }
        }

        public ICommand LogoutCommand { get; }

        public bool WasLogoutSuccessful { get; private set; } = false;
        public SettingsViewModel(IWindowService windowService, IFriendsManagerService friendsManagerService,
            BackgroundMusicService backgroundMusicService) {

            // Cargar el idioma guardado al abrir la ventana de configuración
            var currentLang = Properties.Settings.Default.languageCode;
            if (string.IsNullOrEmpty(currentLang) || currentLang == "en-US")
            {
                isEnglishSelected = true;
            }
            else
            {
                isSpanishSelected = true;
            }
            this.windowService = windowService;
            this.friendsManagerService = friendsManagerService;
            this.backgroundMusicService = backgroundMusicService;
            this.musicVolume = Settings.Default.MusicVolume;
            LogoutCommand = new RelayCommand(Logout);
        }

        private static void UpdateLanguage(string cultureName)
        {
            var culture = new CultureInfo(cultureName);
            TranslationProvider.Instance.SetLanguage(culture);
            Settings.Default.languageCode = cultureName;
            Settings.Default.Save();
        }
        private void Logout(object parameter)
        {
            if (SessionManager.IsUserLoggedIn() && SessionManager.CurrentUser.PlayerId >= 0)
            {
                backgroundMusicService.Stop();

            }
            if (SessionManager.IsUserLoggedIn() && SessionManager.CurrentUser.PlayerId > 0)  
            {
                _ = friendsManagerService.UnsubscribeFromFriendUpdatesAsync(SessionManager.CurrentUser.UserAccountId);
            }
            SessionManager.Logout();
            WasLogoutSuccessful = true;
            windowService.CloseWindow(this);
        }
    }
}
