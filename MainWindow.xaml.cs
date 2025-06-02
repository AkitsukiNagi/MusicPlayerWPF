using MaterialDesignThemes.Wpf;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace MusicPlayerWPF
{
    public enum PlaybackState { Stopped, Playing, Paused }

    /// <summary>
    /// MainWindow.xaml 的互動邏輯
    /// </summary>
    public partial class MainWindow : Window
    {
        private string _audioFilePath;
        private DispatcherTimer _progressTimer = new DispatcherTimer();
        private bool _isUserDraggingSlider = false;
        private int? CurrentIndex;

        private PlaylistWindow playlistWindow;
        private SettingsWindow settingsWindow;

        private PlaybackState CurrentState = PlaybackState.Stopped;
        public event EventHandler<string> MediaChanged;

        public ObservableCollection<PlaylistItem> Playlist = new ObservableCollection<PlaylistItem>();

        public PlaybackState GetCurrentState()
        {
            return CurrentState;
        }

        public int GetCurrentIndex()
        {
            return CurrentIndex != null ? (int)CurrentIndex : -1;
        }

        public MainWindow()
        {
            InitializeComponent();

            Playlist.CollectionChanged += Playlist_CollectionChanged;

            _progressTimer.Interval = TimeSpan.FromMilliseconds(500);
            _progressTimer.Tick += ProgressTimer_Tick;

            VolumeSlider.ValueChanged += VolumeSlider_ValueChanged;
            VolumeSlider.Value = Properties.Settings.Default.Volume;
            MyMediaElement.Volume = VolumeSlider.Value / 100.0;
            VolumeDisplayText.Text = $"{Math.Round(VolumeSlider.Value)}%";

            UpdatePlaybackButtons();
            UpdateProgressUI();

            Closing += MainWindow_Closing;
        }


        private void Playlist_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (playlistWindow != null)
            {
                //playlistWindow.Sync_Playlist(Playlist);
                Playlist = playlistWindow.PlaylistItems;
            }

            //if (e.Action == NotifyCollectionChangedAction.Move)
            //{
            //    if (CurrentIndex != null)
            //    {
            //        int oldIndex = e.OldStartingIndex;
            //        int newIndex = e.NewStartingIndex;

            //        if (oldIndex != CurrentIndex)
            //        {
            //            CurrentIndex = oldIndex;
            //        }
            //        else if (oldIndex < CurrentIndex && newIndex >= CurrentIndex)
            //        {
            //            CurrentIndex--;
            //        }
            //        else if (oldIndex > CurrentIndex && newIndex <= CurrentIndex)
            //        {
            //            CurrentIndex++;
            //        }
            //    }
            //}
            //else if (e.Action == NotifyCollectionChangedAction.Remove)
            //{
            //    if (CurrentIndex != null)
            //    {
            //        if (e.OldStartingIndex <= CurrentIndex)
            //        {
            //            CurrentIndex--;
            //        }
            //    }
            //}
            //else if (e.Action == NotifyCollectionChangedAction.Add)
            //{
            //    // Do nothing.
            //}
            UpdatePlaybackButtons();
        }

        private void ProgressSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (MyMediaElement != null && MyMediaElement.Source != null && MyMediaElement.NaturalDuration.HasTimeSpan && MyMediaElement.NaturalDuration.TimeSpan.TotalSeconds > 0)
            {
                MyMediaElement.Position = TimeSpan.FromSeconds(ProgressSlider.Value);
                UpdateProgressUI();
                _isUserDraggingSlider = false;
                _progressTimer?.Start();
            }
        }

        private void ProgressSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _isUserDraggingSlider = true;
            _progressTimer?.Stop();
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Properties.Settings.Default.Volume = (int)e.NewValue;
            Properties.Settings.Default.Save();

            if (MyMediaElement != null)
            {
                MyMediaElement.Volume = Properties.Settings.Default.Volume / 100.0;
                VolumeDisplayText.Text = $"{Properties.Settings.Default.Volume}%";
            }
        }

        private void PlayPauseBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_audioFilePath == null && CurrentState == PlaybackState.Stopped && Playlist.Count == 0)
            {
                MessageBox.Show("沒有檔案可供播放", "播放失敗");
                return;
            }

            if (_audioFilePath == null && Playlist.Count > 0 && CurrentState == PlaybackState.Stopped)
            {
                CurrentIndex = 0;
                PlayFileFromPath(Playlist[(int)CurrentIndex].FullPath);
            }

            if (CurrentState == PlaybackState.Stopped || (CurrentState == PlaybackState.Paused && (MyMediaElement == null || MyMediaElement.Source == null || (_audioFilePath != null && MyMediaElement.Source.LocalPath != _audioFilePath))))
            {
                if (_audioFilePath != null)
                {
                    PlayFileFromPath(_audioFilePath);
                }
            }
            else if (CurrentState == PlaybackState.Paused && MyMediaElement != null && MyMediaElement.Source != null)
            {
                MyMediaElement.Play();
                _progressTimer?.Start();
                CurrentState = PlaybackState.Playing;
            }
            else if (CurrentState == PlaybackState.Playing && MyMediaElement != null && MyMediaElement.CanPause)
            {
                MyMediaElement.Pause();
                _progressTimer?.Stop();
                CurrentState = PlaybackState.Paused;
            }
            UpdatePlaybackButtons();
        }

        private void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            Stop();
            CurrentState = PlaybackState.Stopped;
            UpdateProgressUI();
            UpdatePlaybackButtons();
            _audioFilePath = null;
            CurrentIndex = null;
        }

        private void Stop()
        {
            if (MyMediaElement != null)
            {
                MyMediaElement.Stop();
                MyMediaElement.Source = null;
                NowPlaying.Text = "正在播放：None";
                _progressTimer?.Stop();

                if (CurrentIndex != null && CurrentIndex >= 0 && CurrentIndex < Playlist.Count)
                {
                    Playlist.RemoveAt((int)CurrentIndex);
                }
            }
        }

        private void PlaylistBtn_Click(object sender, RoutedEventArgs e)
        {
            if (playlistWindow != null)
            {
                if (playlistWindow.WindowState == WindowState.Minimized) playlistWindow.WindowState = WindowState.Normal;
                playlistWindow.Activate();
                return;
            }
            playlistWindow = new PlaylistWindow(this);
            playlistWindow.PlayFileRequested += PlaylistWindow_PlayFileRequested;
            playlistWindow.PlaylistUpdated += PlaylistWindow_PlaylistUpdated;
            playlistWindow.WindowClosedEvent += PlaylistWindow_WindowClosedHandler;
            playlistWindow.CurrentIndexModified += PlaylistWindow_CurrentIndexModified;

            playlistWindow.Show();
        }

        private void PlaylistWindow_CurrentIndexModified(object sender, int newIndex)
        {
            CurrentIndex = newIndex;
        }

        private void PlaylistWindow_PlaylistUpdated(object sender, ObservableCollection<PlaylistItem> newPlaylist)
        {
            Playlist = newPlaylist;

            if (CurrentState == PlaybackState.Stopped && Playlist.Count > 0)
            {
                PlayFileFromPath(Playlist[0].FullPath);
            }
            UpdatePlaybackButtons();
        }

        private void PlaylistWindow_PlayFileRequested(object sender, string filePath)
        {
            PlayFileFromPath(filePath);
        }

        private void PlaylistWindow_WindowClosedHandler(object sender, CancelEventArgs e)
        {
            playlistWindow.CurrentIndexModified -= PlaylistWindow_CurrentIndexModified;
            playlistWindow.WindowClosedEvent -= PlaylistWindow_WindowClosedHandler;
            playlistWindow.PlaylistUpdated -= PlaylistWindow_PlaylistUpdated;
            playlistWindow.PlayFileRequested -= PlaylistWindow_PlayFileRequested;
            playlistWindow = null;
        }

        private void PlayFileFromPath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                MessageBox.Show("檔案路徑無效", "播放失敗");
                StopBtn_Click(this, new RoutedEventArgs());
                return;
            }

            if (_audioFilePath == filePath && CurrentState == PlaybackState.Paused)
            {
                MyMediaElement.Play();
                _progressTimer?.Start();
                CurrentState = PlaybackState.Playing;
            }
            else
            {
                _audioFilePath = filePath;
                NowPlaying.Text = $"正在播放：{Path.GetFileNameWithoutExtension(_audioFilePath)}";
                UpdateProgressUI();
                try
                {
                    MyMediaElement.Source = new Uri(_audioFilePath);
                    CurrentState = PlaybackState.Playing;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "播放失敗");
                    System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                    _audioFilePath = null;
                    StopBtn_Click(this, new RoutedEventArgs());
                }
            }
            CurrentIndex = Playlist.ToList().FindIndex(item => item.FullPath == _audioFilePath);
            MyMediaElement_MediaOpened(this, new RoutedEventArgs());
            UpdatePlaybackButtons();
        }

        private void MyMediaElement_MediaOpened(object sender, RoutedEventArgs e)
        {
            if (MyMediaElement.NaturalDuration.HasTimeSpan && MyMediaElement.NaturalDuration.TimeSpan.TotalSeconds > 0)
            {
                ProgressSlider.Maximum = MyMediaElement.NaturalDuration.TimeSpan.TotalSeconds;
                TotalTimeText.Text = FormatTimeSpan(MyMediaElement.NaturalDuration.TimeSpan);
            }
            else
            {
                ProgressSlider.Maximum = 0;
                TotalTimeText.Text = "00:00";
                RestartBtn_Click(this, new RoutedEventArgs());
            }

            if (CurrentState == PlaybackState.Playing)
            {
                MyMediaElement.Play();
                _progressTimer?.Start();
            }

            UpdatePlaybackButtons();
        }

        private void MyMediaElement_MediaEnded(object sender, RoutedEventArgs e)
        {
            if (CurrentIndex >= 0 && Playlist.Count > 1 && CurrentIndex < Playlist.Count - 1)
            {
                Playlist.RemoveAt((int)CurrentIndex);
            }
            else if (Playlist.Count == 0 || CurrentIndex == Playlist.Count - 1)
            {
                StopBtn_Click(this, new RoutedEventArgs());
            }

            if (_audioFilePath != null && CurrentIndex != null && Playlist.Count >= 1 && CurrentState != PlaybackState.Stopped)
            {
                if (CurrentIndex >= 0 && CurrentIndex < Playlist.Count)
                {
                    PlayFileFromPath(Playlist[(int)CurrentIndex].FullPath);
                }
            }
        }

        private void MyMediaElement_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            MessageBox.Show(e.ErrorException.Message, "媒體載入失敗");
            StopBtn_Click(this, new RoutedEventArgs());
        }

        private void ProgressTimer_Tick(object sender, EventArgs e)
        {
            if (!_isUserDraggingSlider && MyMediaElement != null && MyMediaElement.Source != null && MyMediaElement.NaturalDuration.HasTimeSpan && MyMediaElement.NaturalDuration.TimeSpan.TotalSeconds > 0 && MyMediaElement.Position < MyMediaElement.NaturalDuration.TimeSpan)
            {
                UpdateProgressUI();
            }
            else
            {
                if (MyMediaElement != null && MyMediaElement.NaturalDuration.HasTimeSpan && MyMediaElement.NaturalDuration.TimeSpan.TotalSeconds > 0 && MyMediaElement.Position >= MyMediaElement.NaturalDuration.TimeSpan)
                {
                    _progressTimer?.Stop();
                }
                else if (_progressTimer?.IsEnabled == true)
                {
                    _progressTimer.Stop();
                }
            }
        }

        private void UpdatePlaybackButtons()
        {
            bool isFileSelected = (_audioFilePath != null);
            bool isMediaLoaded = (MyMediaElement != null && MyMediaElement.Source != null && MyMediaElement.NaturalDuration.HasTimeSpan);
            //bool isPlaying = (_progressTimer.IsEnabled == true && isMediaLoaded && MyMediaElement.Position < MyMediaElement.NaturalDuration.TimeSpan);
            //bool isPaused = (isMediaLoaded && MyMediaElement.CanPause && !isPlaying && MyMediaElement.Position < MyMediaElement.NaturalDuration.TimeSpan);
            //bool isStopped = (MyMediaElement == null || MyMediaElement.Source == null || (isPlaying && !isPaused && (MyMediaElement.Position == TimeSpan.Zero || MyMediaElement.Position >= MyMediaElement.NaturalDuration.TimeSpan)));

            PlayPauseBtn.IsEnabled = isFileSelected || CurrentState == PlaybackState.Paused || Playlist.Count > 0;
            ForwardBtn.IsEnabled = (Playlist.Count > 1) && CurrentState != PlaybackState.Stopped && CurrentIndex < Playlist.Count - 1;
            RestartBtn.IsEnabled = isFileSelected && CurrentState != PlaybackState.Stopped;

            if (CurrentState == PlaybackState.Playing)
            {
                ((PackIcon)PlayPauseBtn.Content).Kind = PackIconKind.Pause;
                PlayPauseBtn.ToolTip = "暫停";
            }
            else if (CurrentState == PlaybackState.Paused)
            {
                ((PackIcon)PlayPauseBtn.Content).Kind = PackIconKind.Play;
                PlayPauseBtn.ToolTip = "繼續";
            }
            else
            {
                ((PackIcon)PlayPauseBtn.Content).Kind = PackIconKind.Play;
                PlayPauseBtn.ToolTip = "播放";
            }

            StopBtn.IsEnabled = isMediaLoaded || CurrentState != PlaybackState.Stopped;
            PlaylistBtn.IsEnabled = true;

            bool showProgress = isFileSelected && isMediaLoaded; /* && MyMediaElement.NaturalDuration.TimeSpan.TotalSeconds > 0*/
            CurrentTimeText.Visibility = showProgress ? Visibility.Visible : Visibility.Collapsed;
            ProgressSlider.Visibility = showProgress ? Visibility.Visible : Visibility.Collapsed;
            TotalTimeText.Visibility = showProgress ? Visibility.Visible : Visibility.Collapsed;

            if (CurrentState == PlaybackState.Stopped)
            {
                ProgressSlider.Value = 0;
                CurrentTimeText.Text = "00:00";
                TotalTimeText.Text = "00:00";
                CurrentTimeText.Visibility = Visibility.Collapsed;
                ProgressSlider.Visibility = Visibility.Collapsed;
                TotalTimeText.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateProgressUI()
        {
            if (MyMediaElement != null && MyMediaElement.NaturalDuration.HasTimeSpan && MyMediaElement.NaturalDuration.TimeSpan.TotalSeconds > 0)
            {
                ProgressSlider.Value = MyMediaElement.Position.TotalSeconds;
                CurrentTimeText.Text = FormatTimeSpan(MyMediaElement.Position);
            }
            else
            {
                ProgressSlider.Value = 0;
                ProgressSlider.Maximum = 0;
                CurrentTimeText.Text = "00:00";
                TotalTimeText.Text = "00:00";
            }
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            DisposeMediaPlayer();
            Application.Current.Shutdown();
        }

        private void DisposeMediaPlayer()
        {
            if (MyMediaElement != null)
            {
                MyMediaElement.MediaOpened -= MyMediaElement_MediaOpened;
                MyMediaElement.MediaEnded -= MyMediaElement_MediaEnded;
                MyMediaElement.MediaFailed -= MyMediaElement_MediaFailed;

                if (_progressTimer != null)
                {
                    _progressTimer.Tick -= ProgressTimer_Tick;
                    _progressTimer.Stop();
                    _progressTimer = null;
                }

                MyMediaElement.Stop();
                MyMediaElement.Source = null;
            }
        }

        private string FormatTimeSpan(TimeSpan timeSpan)
        {
            return timeSpan.ToString(@"mm\:ss");
        }

        private void ForwardBtn_Click(object sender, RoutedEventArgs e)
        {
            Stop();

            if (Playlist.Count > 0 && CurrentIndex < Playlist.Count - 1)
            {
                PlayFileFromPath(Playlist[(int)CurrentIndex].FullPath);
            }
        }

        private void RestartBtn_Click(object sender, RoutedEventArgs e)
        {
            MyMediaElement.Stop();
            _progressTimer?.Stop();

            MyMediaElement.Play();
            _progressTimer?.Start();
            UpdatePlaybackButtons();
            UpdateProgressUI();
        }

        private void SettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            if (settingsWindow != null)
            {
                //settingsWindow.Close_Window();
                if (settingsWindow.WindowState == WindowState.Minimized)
                {
                    settingsWindow.WindowState = WindowState.Normal;
                }
                settingsWindow.Activate();
                return;
            }

            settingsWindow = new SettingsWindow();
            settingsWindow.Show();
            settingsWindow.WindowClosedEvent += SettingsWindow_WindowClosedHandler;
        }

        private void SettingsWindow_WindowClosedHandler(object sender, CancelEventArgs e)
        {
            settingsWindow.WindowClosedEvent -= SettingsWindow_WindowClosedHandler;
            settingsWindow = null;
        }
    }
}
