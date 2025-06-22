using LibVLCSharp.Shared;
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;


namespace MusicPlayerWPF
{
    public enum PlaybackState { Stopped, Playing, Paused }

    /// <summary>
    /// MainWindow.xaml 的互動邏輯
    /// </summary>
    public partial class MainWindow : Window
    {
        public string AppName = "MusicPlayerWPF.VLC (Beta)";

        private string audioFilePath;
        private DispatcherTimer progressTimer = new DispatcherTimer();
        private bool isUserDraggingSlider = false;
        private int? currentIndex;

        private PlaylistWindow playlistWindow;
        private SettingsWindow settingsWindow;

        private PlaybackState currentState = PlaybackState.Stopped;
        public event EventHandler<string> MediaChanged;

        private LibVLC libVLC;
        private LibVLCSharp.Shared.MediaPlayer mediaPlayer;

        private Storyboard TitleMarqueeStoryboard, ArtistsMarqueeStoryboard, AlbumMarqueeStoryboard;
        private DoubleAnimation TitleMarqueeAnimation, ArtistsMarqueeAnimation, AlbumMarqueeAnimation;
        private double marqueeVisibleWidth = 550.0;
        private const double pixelsPerSecond = 50.0;

        public PlaybackState GetCurrentState()
        {
            return currentState;
        }

        public int GetCurrentIndex()
        {
            return currentIndex != null ? (int)currentIndex : -1;
        }

        public MainWindow()
        {
            Core.Initialize();
            InitializeComponent();

            libVLC = new LibVLC();
            mediaPlayer = new LibVLCSharp.Shared.MediaPlayer(libVLC);

            mediaPlayer.LengthChanged += MediaPlayer_LengthChanged;
            mediaPlayer.EndReached += MediaPlayer_EndReached;
            mediaPlayer.EncounteredError += MediaPlayer_MediaFailed;
            mediaPlayer.PositionChanged += MediaPlayer_PositionChanged;
            mediaPlayer.Playing += MediaPlayer_Playing;

            Title = AppName;

            App.GlobalPlaylist.CollectionChanged += Playlist_CollectionChanged;

            progressTimer.Interval = TimeSpan.FromMilliseconds(500);
            progressTimer.Tick += ProgressTimer_Tick;

            // Read set volume value from config & apply
            VolumeSlider.ValueChanged += VolumeSlider_ValueChanged;
            VolumeSlider.Value = Properties.Settings.Default.Volume;
            if (mediaPlayer != null)
            {
                mediaPlayer.Volume = (int)VolumeSlider.Value;
            }
            VolumeDisplayText.Text = $"{Math.Round(VolumeSlider.Value)}%";

            // Initialize Marquee Animation
            TitleMarqueeStoryboard = new Storyboard();
            TitleMarqueeAnimation = new DoubleAnimation
            {
                RepeatBehavior = RepeatBehavior.Forever,
            };
            Storyboard.SetTargetName(TitleMarqueeAnimation, "TitleMarqueeTransform");
            Storyboard.SetTargetProperty(TitleMarqueeAnimation, new PropertyPath(TranslateTransform.XProperty));
            TitleMarqueeStoryboard.Children.Add(TitleMarqueeAnimation);

            ArtistsMarqueeStoryboard = new Storyboard();
            ArtistsMarqueeAnimation = new DoubleAnimation
            {
                From = 0,
                RepeatBehavior = RepeatBehavior.Forever
            };
            Storyboard.SetTargetName(ArtistsMarqueeAnimation, "ArtistsMarqueeTransform");
            Storyboard.SetTargetProperty(ArtistsMarqueeAnimation, new PropertyPath(TranslateTransform.XProperty));
            ArtistsMarqueeStoryboard.Children.Add(ArtistsMarqueeAnimation);

            AlbumMarqueeStoryboard = new Storyboard();
            AlbumMarqueeAnimation = new DoubleAnimation
            {
                From = 0,
                RepeatBehavior = RepeatBehavior.Forever
            };
            Storyboard.SetTargetName(AlbumMarqueeAnimation, "AlbumMarqueeTransform");
            Storyboard.SetTargetProperty(ArtistsMarqueeAnimation, new PropertyPath(TranslateTransform.XProperty));
            AlbumMarqueeStoryboard.Children.Add(AlbumMarqueeAnimation);

            UpdatePlaybackButtons();
            UpdateProgressUI();
            NowPlaying.Text = "None";

            Closing += MainWindow_Closing;
        }

        //public void HandleStartupArguments(string[] args)
        //{
        //    if (args != null && args.Length > 0)
        //    {
        //        Dispatcher?.Invoke(() =>
        //        {
        //            if (Playlist == null)
        //            {
        //                Playlist = new ObservableCollection<PlaylistItem>();
        //                Playlist.CollectionChanged += Playlist_CollectionChanged;
        //            }

        //            Import(args);

        //            if (Playlist.Count > 0 && currentState == PlaybackState.Stopped)
        //            {
        //                PlayFileFromPath(Playlist.First().FullPath);
        //            }
        //        });
        //    }
        //}

        public void Import(string[] files)
        {
            Debug.WriteLine("接收到命令包含參數" + string.Join(", ", files));
            foreach (string file in files)
            {
                Debug.WriteLine($"正在載入 {file}");
                if (File.Exists(file) && Constants.SupportFormat.Contains(Path.GetExtension(file)))
                {
                    App.GlobalPlaylist.Add(
                        new PlaylistItem()
                        {
                            FileName = Path.GetFileNameWithoutExtension(file),
                            FullPath = file
                        }
                    );
                    MessageBox.Show($"已將檔案 {file} 加入播放清單中", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else if (!File.Exists(file))
                    MessageBox.Show($"檔案 {file} 不存在", "發生錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                else if (!Constants.SupportFormat.Contains(Path.GetExtension(file)))
                    MessageBox.Show($"輸入的檔案類型 {Path.GetExtension(file).ToLower().Substring(1)} 尚未被支援", "發生錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                else
                    MessageBox.Show($"發生不明錯誤，匯入檔案 {file} 失敗", "發生錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MediaPlayer_Playing(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                currentState = PlaybackState.Playing;
                progressTimer?.Start();
                UpdatePlaybackButtons();
                UpdateProgressUI();
            });
        }

        private void MediaPlayer_LengthChanged(object sender, MediaPlayerLengthChangedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                long lengthMs = e.Length;
                if (lengthMs > 0)
                {
                    TimeSpan duration = TimeSpan.FromMilliseconds(lengthMs);
                    ProgressSlider.Maximum = duration.TotalSeconds;
                    TotalTimeText.Text = FormatTimeSpan(duration);
                    ProgressSlider.Value = 0;
                }
                else
                {
                    ProgressSlider.Maximum = 0;
                    TotalTimeText.Text = "00:00";
                    StopBtn_Click(this, new RoutedEventArgs());
                }
            });
        }

        private void MediaPlayer_PositionChanged(object sender, MediaPlayerPositionChangedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (!isUserDraggingSlider && mediaPlayer.IsPlaying)
                {
                    UpdateProgressUI();
                }
            });
        }

        private void MediaPlayer_MediaFailed(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show("媒體載入或播放失敗", "播放失敗");
                StopBtn_Click(this, new RoutedEventArgs());
            });
        }

        private void MediaPlayer_EndReached(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateProgressUI();
                UpdatePlaybackButtons();
                UpdateMetadataUI();

                if (currentIndex != null && currentIndex >= 0 && currentIndex < App.GlobalPlaylist.Count)
                {
                    bool isLastItem = (currentIndex == App.GlobalPlaylist.Count - 1);

                    App.GlobalPlaylist.RemoveAt((int)currentIndex);

                    if (isLastItem)
                    {
                        if (App.GlobalPlaylist.Count > 0)
                            currentIndex = 0;
                        else
                        {
                            currentIndex = null;
                        }
                    }
                }
                else currentIndex = null;

                if (currentIndex != null && currentIndex >= 0 && currentIndex < App.GlobalPlaylist.Count && currentState != PlaybackState.Stopped)
                    PlayFileFromPath(App.GlobalPlaylist[(int)currentIndex].FullPath);
                else
                {
                    currentState = PlaybackState.Stopped;
                    audioFilePath = null;
                    currentIndex = null;
                    UpdateMetadataUI();
                    UpdateProgressUI();
                }
                UpdatePlaybackButtons();
            });
        }

        private void Playlist_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (playlistWindow != null)
            {
                //playlistWindow.Sync_Playlist(Playlist);
                App.GlobalPlaylist = playlistWindow.GetPlaylist();
            }

            UpdatePlaybackButtons();
        }

        private void ProgressSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (mediaPlayer != null && currentState != PlaybackState.Stopped)
            {
                float newPos = (float)(ProgressSlider.Value / ProgressSlider.Maximum);
                mediaPlayer.Position = newPos;

                UpdateProgressUI();
                isUserDraggingSlider = false;
                if (mediaPlayer.IsPlaying)
                    progressTimer?.Start();
            }
        }

        private void ProgressSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            isUserDraggingSlider = true;
            if (mediaPlayer.IsPlaying && currentState != PlaybackState.Stopped)
                progressTimer?.Stop();
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Properties.Settings.Default.Volume = (int)e.NewValue;
            Properties.Settings.Default.Save();

            if (mediaPlayer != null)
            {
                mediaPlayer.Volume = Properties.Settings.Default.Volume;
                VolumeDisplayText.Text = $"{Properties.Settings.Default.Volume}%";
            }
        }

        private void VolumeSlider_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0 && VolumeSlider.Value != VolumeSlider.Maximum)
            {
                if (VolumeSlider.Value + VolumeSlider.SmallChange * 5 <= VolumeSlider.Maximum)
                    VolumeSlider.Value += VolumeSlider.SmallChange * 5;
                else
                    VolumeSlider.Value = VolumeSlider.Maximum;
            }
            else if (e.Delta < 0 && VolumeSlider.Value != VolumeSlider.Minimum)
            {
                if (VolumeSlider.Value - VolumeSlider.SmallChange * 5 >= VolumeSlider.Minimum)
                    VolumeSlider.Value -= VolumeSlider.SmallChange * 5;
                else
                    VolumeSlider.Value = VolumeSlider.Minimum;
            }
            e.Handled = true;
        }

        private void PlayPauseBtn_Click(object sender, RoutedEventArgs e)
        {
            if (audioFilePath == null && currentState == PlaybackState.Stopped && App.GlobalPlaylist.Count == 0)
            {
                MessageBox.Show("沒有檔案可供播放", "播放失敗");
                return;
            }

            if (audioFilePath == null && App.GlobalPlaylist.Count > 0 && currentState == PlaybackState.Stopped)
            {
                currentIndex = 0;
                PlayFileFromPath(App.GlobalPlaylist[(int)currentIndex].FullPath);
                return;
            }

            if (audioFilePath == null &&    App.GlobalPlaylist.Count > 0 && currentState == PlaybackState.Stopped)
            {
                currentIndex = 0;
                PlayFileFromPath(App.GlobalPlaylist[(int)currentIndex].FullPath);
            }

            if (currentState == PlaybackState.Paused)
            {
                mediaPlayer.Play();
                progressTimer?.Start();
                currentState = PlaybackState.Playing;
            }
            else if (currentState == PlaybackState.Playing)
            {
                mediaPlayer.Pause();
                progressTimer?.Stop();
                currentState = PlaybackState.Paused;
            }
            else if (currentState == PlaybackState.Stopped && audioFilePath != null)
            {
                PlayFileFromPath(audioFilePath);
            }
            UpdatePlaybackButtons();
        }

        private void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            Stop();
            currentState = PlaybackState.Stopped;
            audioFilePath = null;
            currentIndex = null;
            UpdateMetadataUI();
            UpdateProgressUI();
            UpdatePlaybackButtons();
        }

        private void Stop()
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                if (mediaPlayer != null)
                {
                    try
                    {
                        mediaPlayer.Stop();
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine(ex.Message);
                    }
                }
            });
            if (mediaPlayer != null)
            {
                progressTimer?.Stop();

                if (currentIndex != null && currentIndex >= 0 && currentIndex <     App.GlobalPlaylist.Count)
                {
                    App.GlobalPlaylist.RemoveAt((int)currentIndex);
                    if (currentIndex > App.GlobalPlaylist.Count) currentIndex = null;
                }
            }
            UpdateMetadataUI();
            UpdatePlaybackButtons();
            UpdateProgressUI();
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
            currentIndex = newIndex;
        }

        private void PlaylistWindow_PlaylistUpdated(object sender, ObservableCollection<PlaylistItem> newPlaylist)
        {
            App.GlobalPlaylist = newPlaylist;

            if (currentState == PlaybackState.Stopped && App.GlobalPlaylist.Count > 0)
            {
                PlayFileFromPath(App.GlobalPlaylist[0].FullPath);
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

        public void PlayFileFromPath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                MessageBox.Show("檔案路徑無效", "播放失敗");
                StopBtn_Click(this, new RoutedEventArgs());
                return;
            }

            if (audioFilePath == filePath && currentState == PlaybackState.Paused)
            {
                mediaPlayer.Play();
                progressTimer?.Start();
                currentState = PlaybackState.Playing;
            }
            else
            {
                audioFilePath = filePath;
                NowPlaying.Text = $"{Path.GetFileNameWithoutExtension(audioFilePath)}";
                UpdateProgressUI();
                try
                {
                    using (var media = new Media(libVLC, new Uri(audioFilePath)))
                    {
                        mediaPlayer.Play(media);
                    }

                    currentIndex = App.GlobalPlaylist.ToList().FindIndex(item => item.FullPath == audioFilePath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "播放失敗");
                    System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                    audioFilePath = null;
                    StopBtn_Click(this, new RoutedEventArgs());
                }

                try
                {
                    var metadataReader = new MetadataReader();
                    var metadata = metadataReader.GetMetadataByFile(audioFilePath);
                    UpdateMetadataUI(metadata);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex.Message);
                    UpdateMetadataUI();
                }
            }
            UpdatePlaybackButtons();
        }

        private void UpdateMetadataUI(Metadata metadata)
        {
            if (metadata.Thumbnail != null)
            {
                using (MemoryStream ms = new MemoryStream(metadata.Thumbnail))
                {
                    BitmapImage image = new BitmapImage();
                    image.BeginInit();
                    image.StreamSource = ms;
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.EndInit();
                    image.Freeze();
                    Thumbnail.Source = image;
                }
                Thumbnail.Visibility = Visibility.Visible;
                marqueeVisibleWidth = (Width * 0.8) - Thumbnail.DesiredSize.Width - 20.0;
            }
            else marqueeVisibleWidth = Width * 0.8;
            MasterContainer.MaxWidth = marqueeVisibleWidth;

            // Title
            NowPlaying.Text = metadata.Title;
            TitleMarquee.Visibility = Visibility.Visible;
            NowPlaying.Visibility = Visibility.Visible;

            NowPlaying.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            NowPlaying.Arrange(new Rect(0, 0, NowPlaying.DesiredSize.Width, NowPlaying.DesiredSize.Height));

            NowPlayingTextHolder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            NowPlayingTextHolder.Arrange(new Rect(0, 0, NowPlayingTextHolder.DesiredSize.Width, NowPlayingTextHolder.DesiredSize.Height));

            double singleTextWidth = NowPlaying.DesiredSize.Width;
            TitleMarqueeStoryboard?.Stop(TitleMarquee);
            TitleMarqueeTransform.X = 0;
            TitleMarquee.Width = marqueeVisibleWidth;
            TitleMarquee.MaxWidth = marqueeVisibleWidth;

            if (singleTextWidth > marqueeVisibleWidth)
            {
                NowPlaying.TextWrapping = TextWrapping.NoWrap;
                TitleMarqueeAnimation.From = marqueeVisibleWidth;
                TitleMarqueeAnimation.To = -(singleTextWidth);

                double durationSeconds = singleTextWidth / pixelsPerSecond;
                if (durationSeconds < 2) durationSeconds = 2;
                TitleMarqueeAnimation.Duration = new Duration(TimeSpan.FromSeconds(durationSeconds));
                TitleMarqueeStoryboard.Begin(TitleMarquee, true);
            }
            else
            {
                TitleMarqueeStoryboard?.Stop(TitleMarquee);
                TitleMarqueeTransform.X = 0;
                NowPlaying.TextWrapping = TextWrapping.NoWrap;
            }

            // Artists
            ArtistsName.Text = metadata.Artists;
            ArtistsName.Visibility = Visibility.Visible;
            ArtistsMarquee.Visibility = Visibility.Visible;

            ArtistsName.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            ArtistsName.Arrange(new Rect(0, 0, ArtistsName.DesiredSize.Width, ArtistsName.DesiredSize.Height));

            ArtistsTextHolder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            ArtistsTextHolder.Arrange(new Rect(0, 0, ArtistsTextHolder.DesiredSize.Width, ArtistsTextHolder.DesiredSize.Height));

            singleTextWidth = ArtistsName.DesiredSize.Width;
            ArtistsMarqueeStoryboard?.Stop(ArtistsMarquee);
            ArtistsMarqueeTransform.X = 0;
            ArtistsMarquee.Width = marqueeVisibleWidth;
            ArtistsMarquee.MaxWidth = marqueeVisibleWidth;

            if (singleTextWidth > marqueeVisibleWidth)
            {
                ArtistsName.TextWrapping = TextWrapping.NoWrap;
                ArtistsMarqueeAnimation.To = -(singleTextWidth);

                double durationSeconds = singleTextWidth / pixelsPerSecond;
                durationSeconds = (durationSeconds < 2) ? 2 : durationSeconds;
                ArtistsMarqueeAnimation.Duration = new Duration(TimeSpan.FromSeconds(durationSeconds));
                ArtistsMarqueeStoryboard.Begin(ArtistsMarquee, true);
            }
            else
            {
                ArtistsMarqueeStoryboard?.Stop(ArtistsMarquee);
                ArtistsMarqueeTransform.X = 0;
                ArtistsName.TextWrapping = TextWrapping.NoWrap;
            }

            // Album
            AlbumName.Text = metadata.Album;
            AlbumName.Visibility = Visibility.Visible;
            AlbumMarquee.Visibility = Visibility.Visible;

            AlbumName.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            AlbumName.Arrange(new Rect(0, 0, AlbumName.DesiredSize.Width, AlbumName.DesiredSize.Height));

            AlbumTextHolder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            AlbumTextHolder.Arrange(new Rect(0, 0, AlbumTextHolder.DesiredSize.Width, AlbumTextHolder.DesiredSize.Height));

            singleTextWidth = AlbumName.DesiredSize.Width;
            AlbumMarqueeStoryboard?.Stop(AlbumMarquee);
            AlbumMarqueeTransform.X = 0;
            AlbumMarquee.Width = marqueeVisibleWidth;
            AlbumMarquee.MaxWidth = marqueeVisibleWidth;

            if (singleTextWidth > marqueeVisibleWidth)
            {
                AlbumName.TextWrapping = TextWrapping.NoWrap;
                ArtistsMarqueeAnimation.To = -(singleTextWidth);

                double durationSeconds = singleTextWidth / pixelsPerSecond;
                AlbumMarqueeAnimation.Duration = new Duration(TimeSpan.FromSeconds(durationSeconds));
                AlbumMarqueeStoryboard.Begin(AlbumMarquee, true);
            }
            else
            {
                AlbumMarqueeStoryboard?.Stop(AlbumMarquee);
                AlbumMarqueeTransform.X = 0;
                AlbumName.TextWrapping = TextWrapping.NoWrap;
            }
        }

        private void UpdateMetadataUI()
        {
            NowPlaying.Text = "";
            TitleMarquee.Visibility = Visibility.Collapsed;
            NowPlaying.Visibility = Visibility.Collapsed;
            TitleMarqueeStoryboard?.Stop();

            ArtistsName.Text = "";
            ArtistsMarquee.Visibility = Visibility.Collapsed;
            ArtistsName.Visibility = Visibility.Collapsed;
            ArtistsMarqueeStoryboard?.Stop();

            AlbumName.Text = "";
            AlbumMarquee.Visibility = Visibility.Collapsed;
            AlbumName.Visibility = Visibility.Collapsed;
            AlbumMarqueeStoryboard?.Stop();

            Thumbnail.Source = null;
            Thumbnail.Visibility = Visibility.Collapsed;
        }

        private void ProgressTimer_Tick(object sender, EventArgs e)
        {
            if (!isUserDraggingSlider && mediaPlayer != null && mediaPlayer.IsPlaying)
            {
                UpdateProgressUI();
            }
            else
            {
                if (progressTimer?.IsEnabled == true && (mediaPlayer != null || !mediaPlayer.IsPlaying))
                {
                    progressTimer?.Stop();
                }
            }
        }

        private void UpdatePlaybackButtons()
        {
            bool isFileLoaded = (audioFilePath != null && mediaPlayer != null && mediaPlayer.Media != null && mediaPlayer.Media.Duration > 0);
            //bool isMediaLoaded = (MyMediaElement != null && MyMediaElement.Source != null && MyMediaElement.NaturalDuration.HasTimeSpan);
            //bool isPlaying = (_progressTimer.IsEnabled == true && isMediaLoaded && MyMediaElement.Position < MyMediaElement.NaturalDuration.TimeSpan);
            //bool isPaused = (isMediaLoaded && MyMediaElement.CanPause && !isPlaying && MyMediaElement.Position < MyMediaElement.NaturalDuration.TimeSpan);
            //bool isStopped = (MyMediaElement == null || MyMediaElement.Source == null || (isPlaying && !isPaused && (MyMediaElement.Position == TimeSpan.Zero || MyMediaElement.Position >= MyMediaElement.NaturalDuration.TimeSpan)));

            PlayPauseBtn.IsEnabled = isFileLoaded || currentState == PlaybackState.Paused || App.GlobalPlaylist.Count > 0;
            ForwardBtn.IsEnabled = (App.GlobalPlaylist.Count > 1) && currentState != PlaybackState.Stopped && currentIndex < App.GlobalPlaylist.Count - 1;
            RestartBtn.IsEnabled = isFileLoaded && currentState != PlaybackState.Stopped;
            StopBtn.IsEnabled = isFileLoaded && currentState != PlaybackState.Stopped && currentIndex < App.GlobalPlaylist.Count - 1;

            if (currentState == PlaybackState.Playing)
            {
                ((PackIcon)PlayPauseBtn.Content).Kind = PackIconKind.Pause;
                PlayPauseBtn.ToolTip = "暫停";
            }
            else if (currentState == PlaybackState.Paused)
            {
                ((PackIcon)PlayPauseBtn.Content).Kind = PackIconKind.Play;
                PlayPauseBtn.ToolTip = "繼續";
            }
            else
            {
                ((PackIcon)PlayPauseBtn.Content).Kind = PackIconKind.Play;
                PlayPauseBtn.ToolTip = "播放";
            }

            StopBtn.IsEnabled = isFileLoaded || currentState != PlaybackState.Stopped;
            PlaylistBtn.IsEnabled = true;

            bool showProgress = isFileLoaded && currentState != PlaybackState.Stopped; /* && MyMediaElement.NaturalDuration.TimeSpan.TotalSeconds > 0*/
            CurrentTimeText.Visibility = showProgress ? Visibility.Visible : Visibility.Collapsed;
            ProgressSlider.Visibility = showProgress ? Visibility.Visible : Visibility.Collapsed;
            TotalTimeText.Visibility = showProgress ? Visibility.Visible : Visibility.Collapsed;

            if (currentState == PlaybackState.Stopped)
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
            if (mediaPlayer != null && mediaPlayer.Media != null && mediaPlayer.Media.Duration > 0)
            {
                TimeSpan currentPosition = TimeSpan.FromMilliseconds(mediaPlayer.Time);
                TimeSpan totalDuration = TimeSpan.FromMilliseconds(mediaPlayer.Media.Duration);

                if (ProgressSlider.Maximum == 0 && totalDuration.TotalSeconds > 0)
                {
                    ProgressSlider.Maximum = totalDuration.TotalSeconds;
                    TotalTimeText.Text = FormatTimeSpan(totalDuration);
                }

                if (!isUserDraggingSlider)
                {
                    ProgressSlider.Value = currentPosition.TotalSeconds;
                }
                CurrentTimeText.Text = FormatTimeSpan(currentPosition);
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
            if (mediaPlayer != null)
            {
                mediaPlayer.LengthChanged -= MediaPlayer_LengthChanged;
                mediaPlayer.EndReached -= MediaPlayer_EndReached;
                mediaPlayer.EncounteredError -= MediaPlayer_MediaFailed;
                mediaPlayer.PositionChanged -= MediaPlayer_PositionChanged;
                mediaPlayer.Playing -= MediaPlayer_Playing;

                ThreadPool.QueueUserWorkItem(_ =>
                {
                    if (mediaPlayer != null)
                    {
                        try
                        {
                            mediaPlayer.Stop();
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine(ex.Message);
                        }
                    }
                });
                mediaPlayer.Dispose();
                mediaPlayer = null;

                if (libVLC != null)
                {
                    libVLC.Dispose();
                    libVLC = null;
                }

                if (progressTimer != null)
                {
                    progressTimer.Tick -= ProgressTimer_Tick;
                    progressTimer?.Stop();
                    progressTimer = null;
                }
            }
        }

        private string FormatTimeSpan(TimeSpan timeSpan)
        {
            return timeSpan.ToString(@"mm\:ss");
        }

        private void ForwardBtn_Click(object sender, RoutedEventArgs e)
        {
            if (App.GlobalPlaylist.Count > 0 && currentIndex != null && currentIndex < App.GlobalPlaylist.Count - 1)
            {
                Stop();
                currentIndex += 1;
                PlayFileFromPath(App.GlobalPlaylist[(int)currentIndex].FullPath);
            }
            else if (App.GlobalPlaylist.Count > 0 && currentIndex != null && currentIndex == App.GlobalPlaylist.Count - 1)
            {
                StopBtn_Click(this, new RoutedEventArgs());
            }
        }

        private void RestartBtn_Click(object sender, RoutedEventArgs e)
        {
            if (mediaPlayer != null && mediaPlayer.Media != null)
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    mediaPlayer?.Stop();
                    progressTimer?.Stop();
                    currentState = PlaybackState.Stopped;

                    mediaPlayer?.Play();
                    currentState = PlaybackState.Playing;
                    progressTimer?.Start();
                });
                UpdatePlaybackButtons();
                UpdateProgressUI();
            }
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

            settingsWindow = new SettingsWindow(this);
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
