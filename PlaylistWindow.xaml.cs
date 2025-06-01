using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MusicPlayerWPF
{

    public class PlaylistItem
    {
        public string FileName { get; set; }
        public string FullPath { get; set; }
    }

    /// <summary>
    /// PlaylistWindow.xaml 的互動邏輯
    /// </summary>
    public partial class PlaylistWindow : Window
    {
        public ObservableCollection<PlaylistItem> PlaylistItems { get; set; }
        public event EventHandler<string> PlayFileRequested;
        public event EventHandler<ObservableCollection<PlaylistItem>> PlaylistUpdated;
        public event EventHandler<CancelEventArgs> WindowClosedEvent;
        public event EventHandler<int> CurrentIndexModified;

        private readonly MainWindow root;

        public PlaylistWindow(MainWindow root)
        {
            InitializeComponent();

            PlaylistItems = root.Playlist;
            PlaylistItems.CollectionChanged += PlaylistItems_CollectionChanged;
            PlaylistView.ItemsSource = PlaylistItems;

            PlaylistView.SelectionChanged += PlaylistView_SelectionUpdate;
            PlaylistView.MouseDoubleClick += PlaylistView_MouseDoubleClick;

            if (root.GetCurrentIndex() != -1)
            {
                PlaylistView.SelectedIndex = root.GetCurrentIndex();
            }
            this.root = root;

            root.MediaChanged += Root_MediaChanged;

            UpdateOperateButtons(PlaylistView);
            Closing += PlaylistWindow_Closing;
        }

        private void Root_MediaChanged(object sender, string newMediaPath)
        {
            if (root.GetCurrentState() == PlaybackState.Playing)
            {
                PlaylistView.SelectedIndex = root.GetCurrentIndex();
            }
        }

        private void PlaylistItems_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            PlaylistUpdated?.Invoke(this, PlaylistItems);
        }

        public ObservableCollection<PlaylistItem> GetPlaylist()
        {
            return PlaylistItems;
        }

        private void PlaylistView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            if (PlaylistView.SelectedItem is PlaylistItem selectedItem)
            {
                int newIndex = FindIndex(selectedItem);
                if (newIndex != -1)
                {
                    CurrentIndexModified?.Invoke(this, newIndex);
                    PlayFileRequested?.Invoke(this, selectedItem.FullPath);
                }
            }
        }

        private void InsertToNext_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = GetSelectedItems(PlaylistView);
            for (int i = selectedItems.Count - 1; i >= 0; --i)
            {
                var item = selectedItems.ElementAt(i);
                int oldIndex = FindIndex(item);
                if (oldIndex != -1 && oldIndex != root.GetCurrentIndex())
                {
                    //int targetIndex = (root.GetCurrentIndex() != -1 && root.GetCurrentIndex() < PlaylistItems.Count - 1) ? root.GetCurrentIndex() + 1 : 1;
                    int targetIndex = 1;
                    if (root.GetCurrentIndex() != -1 && root.GetCurrentIndex() < PlaylistItems.Count - 1)
                    {
                        targetIndex = (oldIndex > root.GetCurrentIndex()) ? root.GetCurrentIndex() + 1 : root.GetCurrentIndex();
                    }

                    if (oldIndex != targetIndex)
                    {
                        PlaylistItems.Move(oldIndex, targetIndex);

                        if (oldIndex > root.GetCurrentIndex() && targetIndex <= root.GetCurrentIndex())
                        {
                            CurrentIndexModified?.Invoke(this, root.GetCurrentIndex() + 1);
                        }
                        else if (oldIndex < root.GetCurrentIndex() && targetIndex >= root.GetCurrentIndex())
                        {
                            CurrentIndexModified?.Invoke(this, root.GetCurrentIndex() - 1);
                        }
                    }
                }
            }
            SelectItems(PlaylistView, selectedItems);
        }

        private void MoveUp_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = GetSelectedItems(PlaylistView);
            for (int i = selectedItems.Count - 1; i >= 0; --i)
            {
                var item = selectedItems.ElementAt(i);
                var oldIndex = FindIndex(item);

                if (oldIndex != -1 && oldIndex > 0)
                {
                    PlaylistItems.Move(oldIndex, oldIndex - 1);

                    if (oldIndex == root.GetCurrentIndex())
                    {
                        CurrentIndexModified?.Invoke(this, oldIndex - 1);
                    }
                    else if (oldIndex - 1 == root.GetCurrentIndex())
                    {
                        CurrentIndexModified?.Invoke(this, oldIndex);
                    }
                }
            }
            SelectItems(PlaylistView, selectedItems);
        }

        private void OpenInExplorer_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = PlaylistView.SelectedItem as PlaylistItem;
            if (!File.Exists(selectedItem.FullPath))
            {
                MessageBox.Show("檔案不存在", "錯誤");
                return;
            }

            try
            {
                Process.Start("explorer.exe", $"/select,{selectedItem.FullPath}");
            }
            catch (Exception ex)
            {
                MessageBox.Show("開啟檔案時發生錯誤", "錯誤");
                Debug.WriteLine(ex.StackTrace);
            }
        }

        private void MoveDown_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = GetSelectedItems(PlaylistView);
            for (int i = 0; i < selectedItems.Count; ++i)
            {
                var item = selectedItems.ElementAt(i);
                int oldIndex = FindIndex(item);

                if (oldIndex >= 0 && oldIndex < PlaylistItems.Count - 1)
                {
                    if (oldIndex != root.GetCurrentIndex() - 1)
                    {
                        PlaylistItems.Move(oldIndex, oldIndex + 1);

                        if (oldIndex == root.GetCurrentIndex())
                            CurrentIndexModified?.Invoke(this, oldIndex + 1);

                        else if (oldIndex + 1 == root.GetCurrentIndex())
                            CurrentIndexModified?.Invoke(this, oldIndex);
                    }
                }
            }
            SelectItems(PlaylistView, selectedItems);
        }

        private void MoveToBottom_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = GetSelectedItems(PlaylistView);
            for (int i = 0; i < selectedItems.Count; ++i)
            {
                var item = selectedItems.ElementAt(i);
                int oldIndex = FindIndex(item);

                if (oldIndex != -1 && oldIndex < PlaylistItems.Count - 1)
                {
                    int targetIndex = PlaylistItems.Count - 1;


                    if (oldIndex != targetIndex)
                    {
                        PlaylistItems.Move(oldIndex, targetIndex);

                        if (oldIndex == root.GetCurrentIndex())
                        {
                            CurrentIndexModified?.Invoke(this, targetIndex);
                        }
                        else if (oldIndex < root.GetCurrentIndex() && targetIndex >= root.GetCurrentIndex())
                        {
                            CurrentIndexModified?.Invoke(this, root.GetCurrentIndex() - 1);
                        }
                    }
                }
            }
            SelectItems(PlaylistView, selectedItems);
        }

        private void PlaylistView_SelectionUpdate(object sender, RoutedEventArgs e)
        {
            UpdateOperateButtons(PlaylistView);
        }

        private void SelectItems(ListBox listbox, List<PlaylistItem> items)
        {
            listbox.SelectedItems.Clear();
            foreach (var item in items)
            {
                listbox.SelectedItems.Add(item);
            }
        }

        private List<PlaylistItem> GetSelectedItems(ListBox listBox)
        {
            return listBox.SelectedItems.Cast<PlaylistItem>().ToList();
        }

        private int FindIndex(PlaylistItem targetItem)
        {
            return PlaylistItems.IndexOf(targetItem);
        }

        //private int FindIndex(string path)
        //{
        //    return PlaylistItems.ToList().FindIndex(item => item.FullPath == path);
        //}

        protected void UpdateOperateButtons(ListView listView)
        {
            var selected = listView.SelectedItem as PlaylistItem;

            InsertToNext.IsEnabled = selected != null && FindIndex(selected) != root.GetCurrentIndex() && FindIndex(selected) != root.GetCurrentIndex() + 1;
            MoveUp.IsEnabled = selected != null && FindIndex(selected) != 0;
            OpenInExplorer.IsEnabled = selected != null;
            MoveDown.IsEnabled = selected != null && FindIndex(selected) < PlaylistItems.Count - 1;
            MoveToBottom.IsEnabled = selected != null && FindIndex(selected) < PlaylistItems.Count - 1;
        }

        //public void Sync_Playlist(ObservableCollection<PlaylistItem> playlist)
        //{
        //    PlaylistItems = playlist;
        //}


        private void AddFileBtn_Click(object sender, RoutedEventArgs e)
        {
            Dictionary<string, string> filterDict = new Dictionary<string, string>
            {
                { "所有音訊檔案 (*.mp3;*.wav;*.m4a)", "*.mp3;*.wav;*.m4a" },
                { "MPEG-1 檔案 (*.mp3)", "*.mp3" },
                { "MPEG-4 檔案 (*.m4a)", "*.m4a" },
                { "Wave 檔案 (*.wav)", "*.wav" },
                /* { "Vorbis 檔案 (*.ogg)", "*.ogg"}, */
                { "所有檔案 (*.*)", "*.*" }
            };

            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter=string.Join("|", filterDict.Select(item => $"{item.Key}|{item.Value}")),
                Title = "選擇音訊檔案",
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                foreach (string fileName in openFileDialog.FileNames)
                {
                    bool exists = false;
                    foreach (var items in PlaylistItems)
                    {
                        if (items.FullPath == fileName)
                        {
                            exists = true;
                            break;
                        }
                    }

                    if (!exists)
                    {
                        PlaylistItems.Add(new PlaylistItem() { FileName = Path.GetFileNameWithoutExtension(fileName), FullPath = fileName });
                    }
                }
            }
        }

        private void RemoveSelectedBtn_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = PlaylistView.SelectedItems.Cast<PlaylistItem>().ToList();
            foreach (var item in selectedItems)
            {
                int targetIndex = PlaylistItems.IndexOf(item);
                if (targetIndex == root.GetCurrentIndex() && root.GetCurrentState() != PlaybackState.Stopped)
                {
                    MessageBox.Show("你不能刪除正在播放的檔案", "錯誤");
                    continue;
                }
                PlaylistItems.Remove(item);
            }
        }

        private void ClearAllBtn_Click(object sender, RoutedEventArgs e)
        {
            for (var i = 0; i < PlaylistItems.Count; ++i)
            {
                if (i == root.GetCurrentIndex() && root.GetCurrentState() != PlaybackState.Stopped)
                {
                    continue;
                }
                PlaylistItems.RemoveAt(i);
            }
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            //if (PlaylistView.Items.Count > 0 && root.GetCurrentState() == PlaybackState.Stopped)
            //{
            //    PlayFileRequested?.Invoke(this, PlaylistItems[0].FullPath);
            //}
            Close();
        }

        private void PlaylistWindow_Closing(object sender, CancelEventArgs e)
        {
            Closing -= PlaylistWindow_Closing;
            PlaylistItems.CollectionChanged -= PlaylistItems_CollectionChanged;
            PlaylistView.MouseDoubleClick -= PlaylistView_MouseDoubleClick;
            PlaylistView.SelectionChanged -= PlaylistView_SelectionUpdate;
            root.MediaChanged -= Root_MediaChanged;
            WindowClosedEvent?.Invoke(this, e);
        }
    }
}
