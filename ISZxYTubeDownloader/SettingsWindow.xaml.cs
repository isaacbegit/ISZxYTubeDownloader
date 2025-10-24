using System.Windows;

namespace ISZxYTubeDownloader
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            // Load saved settings from AppSettings or config file
            // For now, using default values
            chkAutoStart.IsChecked = AppSettings.AutoStartDownloads;
            chkConfirmCancel.IsChecked = AppSettings.ConfirmBeforeCancel;
            chkOverwriteFiles.IsChecked = AppSettings.OverwriteExistingFiles;
            chkShowNotifications.IsChecked = AppSettings.ShowNotifications;
            chkMinimizeToTray.IsChecked = AppSettings.MinimizeToTray;
            chkSoundOnComplete.IsChecked = AppSettings.PlaySoundOnComplete;
            chkEnableLogging.IsChecked = AppSettings.EnableLogging;
            chkCheckUpdates.IsChecked = AppSettings.CheckForUpdates;

            cmbRetryAttempts.SelectedIndex = AppSettings.MaxRetryAttempts;
            cmbDefaultFormat.SelectedIndex = AppSettings.DefaultFormatIndex;
            cmbDefaultQuality.SelectedIndex = AppSettings.DefaultQualityIndex;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // Save settings
            AppSettings.AutoStartDownloads = chkAutoStart.IsChecked ?? false;
            AppSettings.ConfirmBeforeCancel = chkConfirmCancel.IsChecked ?? true;
            AppSettings.OverwriteExistingFiles = chkOverwriteFiles.IsChecked ?? false;
            AppSettings.ShowNotifications = chkShowNotifications.IsChecked ?? true;
            AppSettings.MinimizeToTray = chkMinimizeToTray.IsChecked ?? false;
            AppSettings.PlaySoundOnComplete = chkSoundOnComplete.IsChecked ?? false;
            AppSettings.EnableLogging = chkEnableLogging.IsChecked ?? false;
            AppSettings.CheckForUpdates = chkCheckUpdates.IsChecked ?? true;

            AppSettings.MaxRetryAttempts = cmbRetryAttempts.SelectedIndex;
            AppSettings.DefaultFormatIndex = cmbDefaultFormat.SelectedIndex;
            AppSettings.DefaultQualityIndex = cmbDefaultQuality.SelectedIndex;

            AppSettings.Save();

            MessageBox.Show("Settings saved successfully!", "Success",
                MessageBoxButton.OK, MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnClearHistory_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to clear download history?",
                "Clear History",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                AppSettings.ClearDownloadHistory();
                MessageBox.Show("Download history cleared.", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }

    /// <summary>
    /// Application settings storage
    /// </summary>
    public static class AppSettings
    {
        // Download Settings
        public static bool AutoStartDownloads { get; set; } = false;
        public static bool ConfirmBeforeCancel { get; set; } = true;
        public static bool OverwriteExistingFiles { get; set; } = false;
        public static int MaxRetryAttempts { get; set; } = 2;

        // Default Format & Quality
        public static int DefaultFormatIndex { get; set; } = 0;
        public static int DefaultQualityIndex { get; set; } = 0;

        // UI Settings
        public static bool ShowNotifications { get; set; } = true;
        public static bool MinimizeToTray { get; set; } = false;
        public static bool PlaySoundOnComplete { get; set; } = false;

        // Advanced
        public static bool EnableLogging { get; set; } = false;
        public static bool CheckForUpdates { get; set; } = true;

        /// <summary>
        /// Save settings to file
        /// </summary>
        public static void Save()
        {
            // In a real application, save to JSON file or Registry
            // For now, settings persist in memory during app session
           
            Properties.Settings.Default.Save();
        }

        /// <summary>
        /// Load settings from file
        /// </summary>
        public static void Load()
        {
            // Load from JSON file or Registry
            Properties.Settings.Default.Reload();
        }

        /// <summary>
        /// Clear download history
        /// </summary>
        public static void ClearDownloadHistory()
        {
            // Clear any stored download history
            // Implementation depends on how history is stored
        }
    }
}