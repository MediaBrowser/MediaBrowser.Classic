using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using MediaBrowser;
using MediaBrowser.Library;
using MediaBrowser.Library.Factories;
using MediaBrowser.Library.Playables.ExternalPlayer;

namespace Configurator
{
    /// <summary>
    /// Interaction logic for ExternalPlayerForm.xaml
    /// </summary>
    public partial class ExternalPlayerForm : Window
    {
        public ExternalPlayerForm(bool isNew)
        {
            InitializeComponent();
            PopulateControls();

            // Set title
            if (isNew)
            {
                Title = "Add External Player";
            }
            else
            {
                Title = "Edit External Player";
            }

            lstPlayerType.SelectionChanged += new SelectionChangedEventHandler(lstPlayerType_SelectionChanged);
            btnCommand.Click += new RoutedEventHandler(btnCommand_Click);
            lnkSelectAllMediaTypes.Click += new RoutedEventHandler(lnkSelectAllMediaTypes_Click);
            lnkSelectAllVideoFormats.Click += new RoutedEventHandler(lnkSelectAllVideoFormats_Click);
            lnkSelectNoneMediaTypes.Click += new RoutedEventHandler(lnkSelectAllMediaTypes_Click);
            lnkSelectNoneVideoFormats.Click += new RoutedEventHandler(lnkSelectAllVideoFormats_Click);
            lnkConfigureMyPlayer.Click += new RoutedEventHandler(lnkConfigureMyPlayer_Click);
        }

        void lnkConfigureMyPlayer_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateUserInput())
            {
                return;
            }

            PlayableExternalConfigurator uiConfigurator = PlayableItemFactory.Instance.GetPlayableExternalConfiguratorByName(ExternalPlayerName);

            if (MessageBox.Show(uiConfigurator.ConfigureUserSettingsConfirmationMessage, "Configure Player", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                ConfigData.ExternalPlayer currentConfiguration = uiConfigurator.GetDefaultConfiguration();
                currentConfiguration.Command = txtCommand.Text;

                try
                {
                    uiConfigurator.ConfigureUserSettings(currentConfiguration);
                }
                catch
                {
                    MessageBox.Show("There was an error configuring some settings. Please open your player and verify them.");
                }
            }
        }

        void lnkSelectAllVideoFormats_Click(object sender, RoutedEventArgs e)
        {
            EnumWrapperList<VideoFormat> source = lstVideoFormats.ItemsSource as EnumWrapperList<VideoFormat>;

            bool selectAll = (sender as Hyperlink) == lnkSelectAllVideoFormats;

            source.SelectAll(selectAll);
            SetListDataSource(lstVideoFormats, source);
        }

        void lnkSelectAllMediaTypes_Click(object sender, RoutedEventArgs e)
        {
            EnumWrapperList<MediaType> source = lstMediaTypes.ItemsSource as EnumWrapperList<MediaType>;

            bool selectAll = (sender as Hyperlink) == lnkSelectAllMediaTypes;

            source.SelectAll(selectAll);
            SetListDataSource(lstMediaTypes, source);
        }

        private void SetListDataSource<T>(ListBox listbox, EnumWrapperList<T> source)
        {
            listbox.ItemsSource = null;
            listbox.ItemsSource = source;
        }

        private string ExternalPlayerName
        {
            get
            {
                return lstPlayerType.SelectedItem.ToString();
            }
        }

        void btnCommand_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.OpenFileDialog();

            if (!string.IsNullOrEmpty(txtCommand.Text))
            {
                dialog.FileName = txtCommand.Text;
            }

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                txtCommand.Text = dialog.FileName;
            }
        }

        void lstPlayerType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PlayableExternalConfigurator uiConfigurator = PlayableItemFactory.Instance.GetPlayableExternalConfiguratorByName(ExternalPlayerName);
            ConfigData.ExternalPlayer externalPlayer = uiConfigurator.GetDefaultConfiguration();

            FillControlsFromObject(externalPlayer, uiConfigurator, false, false);
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateUserInput())
            {
                return;
            }

            this.DialogResult = true;
            this.Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void PopulateControls()
        {
            lstPlayerType.ItemsSource = PlayableItemFactory.Instance.GetAllPlayableExternalConfigurators().Select(t => t.ExternalPlayerName);

            SetListDataSource(lstMediaTypes, EnumWrapperList<MediaType>.Create());
            SetListDataSource(lstVideoFormats, EnumWrapperList<VideoFormat>.Create());
        }

        public void FillControlsFromObject(ConfigData.ExternalPlayer externalPlayer)
        {
            FillControlsFromObject(externalPlayer, PlayableItemFactory.Instance.GetPlayableExternalConfiguratorByName(externalPlayer.ExternalPlayerName), true, true);
        }

        public void FillControlsFromObject(ConfigData.ExternalPlayer externalPlayer, PlayableExternalConfigurator uiConfigurator, bool refreshMediaTypes, bool refreshVideoFormats)
        {
            lstPlayerType.SelectedItem = externalPlayer.ExternalPlayerName;

            txtArguments.Text = externalPlayer.Args;

            chkMinimizeMce.IsChecked = externalPlayer.MinimizeMCE;
            chkShowSplashScreen.IsChecked = externalPlayer.ShowSplashScreen;
            //chkHideTaskbar.IsChecked = externalPlayer.HideTaskbar;
            chkSupportsMultiFileCommand.IsChecked = externalPlayer.SupportsMultiFileCommandArguments;
            chkSupportsPLS.IsChecked = externalPlayer.SupportsPlaylists;

            if (refreshMediaTypes)
            {
                EnumWrapperList<MediaType> mediaTypes = lstMediaTypes.ItemsSource as EnumWrapperList<MediaType>;
                mediaTypes.SetValues(externalPlayer.MediaTypes);
                SetListDataSource(lstMediaTypes, mediaTypes);
            }

            if (refreshVideoFormats)
            {
                EnumWrapperList<VideoFormat> videoFormats = lstVideoFormats.ItemsSource as EnumWrapperList<VideoFormat>;
                videoFormats.SetValues(externalPlayer.VideoFormats);
                SetListDataSource(lstVideoFormats, videoFormats);
            }

            SetControlVisibility(uiConfigurator);
            SetTips(uiConfigurator);

            if (string.IsNullOrEmpty(externalPlayer.Command))
            {
                AutoFillPaths(uiConfigurator);
            }
            else
            {
                txtCommand.Text = externalPlayer.Command;
            }
        }

        public void UpdateObjectFromControls(ConfigData.ExternalPlayer externalPlayer)
        {
            PlayableExternalConfigurator uiConfigurator = PlayableItemFactory.Instance.GetPlayableExternalConfiguratorByName(ExternalPlayerName);
            ConfigData.ExternalPlayer externalPlayerDefault = uiConfigurator.GetDefaultConfiguration();

            externalPlayer.LaunchType = externalPlayerDefault.LaunchType;
            externalPlayer.ExternalPlayerName = lstPlayerType.SelectedItem.ToString();

            externalPlayer.Args = txtArguments.Text;
            externalPlayer.Command = txtCommand.Text;

            externalPlayer.MinimizeMCE = chkMinimizeMce.IsChecked.Value;
            externalPlayer.ShowSplashScreen = chkShowSplashScreen.IsChecked.Value;
            //externalPlayer.HideTaskbar = chkHideTaskbar.IsChecked.Value;
            externalPlayer.SupportsMultiFileCommandArguments = chkSupportsMultiFileCommand.IsChecked.Value;
            externalPlayer.SupportsPlaylists = chkSupportsPLS.IsChecked.Value;

            externalPlayer.MediaTypes = (lstMediaTypes.ItemsSource as EnumWrapperList<MediaType>).GetCheckedValues();
            externalPlayer.VideoFormats = (lstVideoFormats.ItemsSource as EnumWrapperList<VideoFormat>).GetCheckedValues();
        }

        private void SetControlVisibility(PlayableExternalConfigurator configurator)
        {
            // Expose all fields only for the base class
            // We can make this more flexible down the road if needed
            if (configurator.GetType() == typeof(PlayableExternalConfigurator))
            {
                chkSupportsMultiFileCommand.Visibility = System.Windows.Visibility.Visible;
                chkSupportsPLS.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                chkSupportsMultiFileCommand.Visibility = System.Windows.Visibility.Hidden;
                chkSupportsPLS.Visibility = System.Windows.Visibility.Hidden;
            }

            lblArguments.Visibility = configurator.AllowArgumentsEditing ? System.Windows.Visibility.Visible : System.Windows.Visibility.Hidden;
            txtArguments.Visibility = configurator.AllowArgumentsEditing ? System.Windows.Visibility.Visible : System.Windows.Visibility.Hidden;
            lbConfigureMyPlayer.Visibility = configurator.SupportsConfiguringUserSettings ? System.Windows.Visibility.Visible : System.Windows.Visibility.Hidden;
        }

        private void SetTips(PlayableExternalConfigurator configurator)
        {
            txtCommand.ToolTip = configurator.CommandFieldTooltip;
            lblTipsHeader.Content = configurator.ExternalPlayerName + " Player Tips:";
            txtTips.Text = configurator.PlayerTips;
        }

        private bool ValidateUserInput()
        {
            // Validate Player Path
            if (!IsPathValid(txtCommand.Text))
            {
                MessageBox.Show("Please enter a valid player path.");
                return false;
            }

            if ((lstMediaTypes.ItemsSource as EnumWrapperList<MediaType>).GetCheckedValues().Count == 0)
            {
                MessageBox.Show("Please select at least one media type.");
                return false;
            }

            if ((lstVideoFormats.ItemsSource as EnumWrapperList<VideoFormat>).GetCheckedValues().Count == 0)
            {
                MessageBox.Show("Please select at least one video format.");
                return false;
            }

            PlayableExternalConfigurator uiConfigurator = PlayableItemFactory.Instance.GetPlayableExternalConfiguratorByName(ExternalPlayerName);

            if ((lstMediaTypes.ItemsSource as EnumWrapperList<MediaType>).GetCheckedValues().Contains(MediaType.ISO) && uiConfigurator.ShowIsoDirectLaunchWarning)
            {
                if (MessageBox.Show(uiConfigurator.IsoDirectLaunchWarning, "Confirm ISO Media Type", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                {
                    return false;
                }
            }

            /*if (chkHideTaskbar.IsChecked == true)
            {
                string warning = "Hiding the windows taskbar can improve the external player experience but comes with a warning. If MediaBrowser crashes or becomes unstable during playback, you may have to reboot your computer to get your taskbar back. You can also get the taskbar back by starting and stopping another video that uses the same player. Are you sure you want to continue?";

                if (MessageBox.Show(warning, "Confirm Hide Taskbar", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                {
                    return false;
                }
            }*/

            return true;
        }

        private bool IsPathValid(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            if (Path.IsPathRooted(path) && !File.Exists(path))
            {
                return false;
            }

            return true;
        }

        private void AutoFillPaths(PlayableExternalConfigurator configurator)
        {
            foreach (string path in configurator.GetKnownPlayerPaths())
            {
                if (File.Exists(path))
                {
                    txtCommand.Text = path;
                    return;
                }
            }

            txtCommand.Text = string.Empty;
        }

        private class EnumWrapper<TEnumType>
        {
            public TEnumType Value { get; set; }
            public bool IsChecked { get; set; }
        }

        private class EnumWrapperList<TEnumType> : List<EnumWrapper<TEnumType>>
        {
            public static EnumWrapperList<TEnumType> Create()
            {
                EnumWrapperList<TEnumType> list = new EnumWrapperList<TEnumType>();

                foreach (TEnumType val in Enum.GetValues(typeof(TEnumType)))
                {
                    list.Add(new EnumWrapper<TEnumType>() { Value = val, IsChecked = false });
                }

                return list;
            }

            public List<TEnumType> GetCheckedValues()
            {
                List<TEnumType> values = new List<TEnumType>();

                foreach (EnumWrapper<TEnumType> wrapper in this)
                {
                    if (wrapper.IsChecked)
                    {
                        values.Add(wrapper.Value);
                    }
                }

                return values;
            }

            public void SetValues(List<TEnumType> values)
            {
                foreach (EnumWrapper<TEnumType> wrapper in this)
                {
                    wrapper.IsChecked = values.Count == 0 || values.Contains(wrapper.Value);
                }
            }

            public void SelectAll(bool selected)
            {
                foreach (EnumWrapper<TEnumType> wrapper in this)
                {
                    wrapper.IsChecked = selected;
                }
            }
        }

    }
}
