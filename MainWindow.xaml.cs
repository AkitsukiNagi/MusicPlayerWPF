using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using System;
using System.IO;
using System.Runtime.InteropServices.ComTypes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace MusicPlayerWPF
{
    /// <summary>
    /// MainWindow.xaml 的互動邏輯
    /// </summary>
    public partial class MainWindow : Window
    {
        private string _audioFilePath;
        private DispatcherTimer _progressTimer;
        private bool _isUserDraggingSlider = false;
        private enum PlaybackState { Stopped, Playing, Paused }
        private PlaybackState _currentState = PlaybackState.Stopped;

        public MainWindow()
        {
            InitializeComponent();

            _progressTimer = new DispatcherTimer();
            _progressTimer.Interval = TimeSpan.FromMilliseconds(500);
            _progressTimer.Tick += ProgressTimer_Tick;

            MyMediaElement.Volume = VolumeSlider.Value / 100.0;
            VolumeDisplayText.Text = $"{Math.Round(VolumeSlider.Value)}%";

            UpdatePlaybackButtons();
            UpdateProgressUI();

            Closing += MainWindow_Closing;
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
            if (MyMediaElement != null)
            {
                MyMediaElement.Volume = e.NewValue / 100.0;
                VolumeDisplayText.Text = $"{Math.Round(e.NewValue)}%";
            }
        }

        private void PlayPauseBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_audioFilePath == null && _currentState == PlaybackState.Stopped)
            {
                MessageBox.Show("沒有檔案可供播放", "播放失敗");
                return;
            }

            if (_currentState == PlaybackState.Stopped || (_currentState == PlaybackState.Paused && (MyMediaElement == null || MyMediaElement.Source == null || MyMediaElement.Source.LocalPath != _audioFilePath)))
            {
                try
                {
                    MyMediaElement.Source = new Uri(_audioFilePath);
                    _currentState = PlaybackState.Playing;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "播放失敗");
                    System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                    _audioFilePath = null;
                    UpdatePlaybackButtons();
                    UpdateProgressUI();
                }
            }
            else if (_currentState == PlaybackState.Paused && MyMediaElement != null && MyMediaElement.Source != null)
            {
                MyMediaElement.Play();
                _progressTimer?.Start();
                _currentState = PlaybackState.Playing;
            }
            else if (_currentState == PlaybackState.Playing && MyMediaElement != null && MyMediaElement.CanPause)
            {
                MyMediaElement.Pause();
                _progressTimer?.Stop();
                _currentState = PlaybackState.Paused;
            }
            UpdatePlaybackButtons();
        }

        //private void PauseBtn_Click(object sender, RoutedEventArgs e)
        //{
        //    if (MyMediaElement == null || MyMediaElement.Source == null)
        //    {
        //        return;
        //    }
        //    if (_currentState == PlaybackState.Playing && MyMediaElement.CanPause)
        //    {
        //        PauseBtn.ToolTip = "繼續";
        //        MyMediaElement.Pause();
        //        _progressTimer?.Stop();
        //        _currentState = PlaybackState.Paused;
        //    }
        //    else if (_currentState == PlaybackState.Paused)
        //    {
        //        PauseBtn.ToolTip = "暫停";
        //        MyMediaElement.Play();
        //        _progressTimer?.Start();
        //        _currentState = PlaybackState.Playing;
        //    }

        //    UpdatePlaybackButtons();
        //}

        private void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            if (MyMediaElement != null)
            {
                MyMediaElement.Stop();
                MyMediaElement.Source = null;
                NowPlaying.Text = "正在播放：None";
                _progressTimer?.Stop();
                _currentState = PlaybackState.Stopped;
            }
            UpdateProgressUI();
            UpdatePlaybackButtons();
            _audioFilePath = null;
        }

        private void BrowseBtn_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "音訊檔案 (*.mp3;*.wav;*.ogg;*.m4a)|*.mp3;*.wav;*.ogg;*.m4a|所有檔案(*.*)|*.*";
            openFileDialog.Title = "選擇音訊檔案";

            if (openFileDialog.ShowDialog() == true)
            {
                StopBtn_Click(this, new RoutedEventArgs());
                _audioFilePath = openFileDialog.FileName;
                NowPlaying.Text = $"正在播放：{Path.GetFileNameWithoutExtension(_audioFilePath)}";
                UpdateProgressUI();
                PlayPauseBtn_Click(this, new RoutedEventArgs());
            }
            else
            {
                StopBtn_Click(this, new RoutedEventArgs());
                _audioFilePath = null;
                NowPlaying.Text = "正在播放：None";
                UpdateProgressUI();
            }
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
            }

            MyMediaElement.Play();
            _progressTimer?.Start();

            UpdatePlaybackButtons();
        }

        private void MyMediaElement_MediaEnded(object sender, RoutedEventArgs e)
        {
            StopBtn_Click(this, new RoutedEventArgs());
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
                if (_progressTimer?.IsEnabled == true)
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

            PlayPauseBtn.IsEnabled = isFileSelected || _currentState == PlaybackState.Paused;

            if (_currentState == PlaybackState.Paused)
            {
                ((PackIcon)PlayPauseBtn.Content).Kind = PackIconKind.Play;
                PlayPauseBtn.ToolTip = "繼續";
            }
            else
            {
                ((PackIcon)PlayPauseBtn.Content).Kind = PackIconKind.Pause;
                PlayPauseBtn.ToolTip = "暫停";
            }

            StopBtn.IsEnabled = isMediaLoaded;
            BrowseBtn.IsEnabled = (_currentState == PlaybackState.Stopped);

            bool showProgress = isFileSelected && isMediaLoaded && MyMediaElement.NaturalDuration.TimeSpan.TotalSeconds > 0;
            CurrentTimeText.Visibility = showProgress ? Visibility.Visible : Visibility.Collapsed;
            ProgressSlider.Visibility = showProgress ? Visibility.Visible : Visibility.Collapsed;
            TotalTimeText.Visibility = showProgress ? Visibility.Visible: Visibility.Collapsed;

            if (_currentState == PlaybackState.Stopped)
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

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            DisposeMediaPlayer();
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
    }
}
