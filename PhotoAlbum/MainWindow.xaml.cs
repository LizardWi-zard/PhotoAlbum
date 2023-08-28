using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using OpenDialogResult = System.Windows.Forms.DialogResult;
using Path = System.IO.Path;

namespace PhotoAlbum
{
    public partial class MainWindow : Window
    {
        Dictionary<string, List<int>> albums = new Dictionary<string, List<int>>();
        Dictionary<string, List<int>> virtualDirectories = new Dictionary<string, List<int>>();

        List<DriveInfo> readyDrives = new List<DriveInfo>();
        List<BitmapImage> bitmapPhotosList = new List<BitmapImage>();

        List<string> directories = new List<string>();
        List<string> directoriesNames = new List<string>();

        string selectedItemPath;
        string folderPath;
        string selectedAlbum = "AllPhoto";

        int selectedItemIndex;

        double mediumPhotoSize = 158.5;
        double bigPhotoSize = 315;

        public MainWindow()
        {
            albums.Add("AllPhoto", new List<int>());

            InitializeComponent();
        }

        private void RefreshDrives_Click(object sender, RoutedEventArgs e)
        {
            readyDrives.Clear();

            DriveInfo[] allDrives = DriveInfo.GetDrives();
            foreach (DriveInfo d in allDrives)
            {
                if (d.IsReady == true)
                {
                    readyDrives.Add(d);
                }
            }

            DrivesListBox.ItemsSource = readyDrives;
            DrivesListBox.Items.Refresh();
        }

        private void SetDirecrtory_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog openFolderDialog = new FolderBrowserDialog();

            string driveLetter = ((System.Windows.Controls.Button)sender)?.Content?.ToString() ?? String.Empty;

            openFolderDialog.InitialDirectory = driveLetter;

            try
            {
                var dialogResult = openFolderDialog.ShowDialog();

                if (dialogResult == OpenDialogResult.OK)
                {
                    folderPath = openFolderDialog.SelectedPath;
                }

                FillListWithPhotos(folderPath);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.ToString());
            }

            AlbumListBox.ItemsSource = albums.Keys;
            AlbumListBox.SelectedItem = "AllPhoto";

            AlbumListBox.Items.Refresh();
            DrivesListBox.Items.Refresh();
        }

        private void FillListWithPhotos(string directoryPath)
        {
            List<string> imageList = new List<string>();
            List<Byte[]> photosInBytes = new List<Byte[]>();

            directories = Directory.GetDirectories(directoryPath, "*", SearchOption.AllDirectories).ToList();

            albums.Add(Path.GetFileName(directoryPath), new List<int>());
            directoriesNames.Add(Path.GetFileName(directoryPath));

            foreach (string directory in directories)
            {
                albums.Add(Path.GetFileName(directory), new List<int>()); //bug!
                directoriesNames.Add(Path.GetFileName(directory));
            }

            imageList = Directory.GetFiles(directoryPath, "*.*", SearchOption.AllDirectories).Where(s => s.EndsWith(".png") || s.EndsWith(".jpg") || s.EndsWith(".jpeg")).ToList();

            foreach (var imagePath in imageList)
            {
                bitmapPhotosList.Add(ConvertFileToBitmapImage(imagePath));
            }

            foreach (var item in bitmapPhotosList)
            {
                albums["AllPhoto"].Add(bitmapPhotosList.IndexOf(item));
            }

            foreach (var album in directoriesNames)
            {
                foreach (var item in imageList)
                {
                    var nm = Path.GetFileName(Path.GetDirectoryName(item));
                    if (album == nm)
                    {
                        albums[album].Add(imageList.IndexOf(item));
                    }
                }
            }

            FilesCounter.Text = "Total images: " + bitmapPhotosList.Count().ToString();

            PhotoListBox.ItemsSource = bitmapPhotosList;
            PhotoListBox.Items.Refresh();
        }

        private BitmapImage ConvertFileToBitmapImage(String FilePath)
        {
            byte[] file = File.ReadAllBytes(FilePath);

            try
            {
                if (file == null || file.Length == 0) return null;
                var image = new BitmapImage();
                using (var mem = new MemoryStream(file))
                {
                    mem.Position = 0;
                    image.BeginInit();
                    image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.UriSource = null;
                    image.DecodePixelHeight = 200;
                    image.DecodePixelWidth = 200;
                    image.StreamSource = mem;
                    image.EndInit();
                }
                image.Freeze();
                return image;
            }
            catch
            {
                return null;
            }
        }

        private void AddPictures_Click(object sender, RoutedEventArgs e)
        {
            List<BitmapImage> imageList = new List<BitmapImage>();

            imageList = (List<BitmapImage>)PhotoListBox.ItemsSource;

            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Multiselect = true;

            try
            {
                if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    foreach (string file in openFileDialog.FileNames)
                    {
                        var fileName = Path.GetFileName(file);
                        var path = Path.Combine(folderPath, fileName);
                        File.Copy(file, path);
                        imageList.Add(ConvertFileToBitmapImage(file));
                    }
                }
            }
            catch (IOException)
            {
                System.Windows.MessageBox.Show("File already exist");
            }
            catch (ArgumentNullException)
            {
                System.Windows.MessageBox.Show("Open the folder where you want to add the image first");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.ToString());
            }

            FilesCounter.Text = "Total images: " + bitmapPhotosList.Count().ToString();

            PhotoListBox.ItemsSource = imageList;
            PhotoListBox.Items.Refresh();
        }

        private void HideEmptyFolders(object sender, RoutedEventArgs e)
        {
            List<string> activeAlbums = new List<string>();

            if (FolderApearence_RadioButton.IsChecked == true)
            {

                activeAlbums = albums.Keys.Where(x => albums[x].Count() != 0 || x == "AllPhoto").ToList();

                AlbumListBox.SelectedItem = "AllPhoto";
                AlbumListBox.ItemsSource = activeAlbums;
            }
            else
            {
                AlbumListBox.ItemsSource = albums.Keys;
            }
        }

        private void SetSmallSize_Click(object sender, RoutedEventArgs e)
        {
            PhotoSize_Slider.Value = PhotoSize_Slider.Minimum;
        }

        private void SetMediumSize_Click(object sender, RoutedEventArgs e)
        {
            PhotoSize_Slider.Value = mediumPhotoSize;
        }

        private void SetBigSize_Click(object sender, RoutedEventArgs e)
        {
            PhotoSize_Slider.Value = bigPhotoSize;
        }

        private void CalculateImageSize(object sender, SizeChangedEventArgs e)
        {
            PhotoSize_Slider.Maximum = (PhotoListBox.ActualWidth / 2) - 10;

            bigPhotoSize = (PhotoListBox.ActualWidth / 5) - 15;
            mediumPhotoSize = (PhotoListBox.ActualWidth / 9) - 10;
        }

        private void AddAlbum_Click(object sender, RoutedEventArgs e)
        {
            AlbumNameInput.Visibility = Visibility.Visible;
            AlbumNameInput_TextBox.Text = String.Empty;
            AlbumNameInput_TextBox.Focus();
        }

        private void ApplyName_Click(object sender, RoutedEventArgs e)
        {
            AlbumNameInput.Visibility = Visibility.Collapsed;

            String input = AlbumNameInput_TextBox.Text;
            AddAlbum(input);

            AlbumListBox.ItemsSource = albums.Keys;
            AlbumListBox.Items.Refresh();

            AddToAlbumSelector_ListBox.ItemsSource = albums.Keys.Where(x => x != "AllPhoto");
            AddToAlbumSelector_ListBox.Items.Refresh();

            RemoveFromAlbumSelector_ListBox.ItemsSource = albums.Keys.Where(x => x != "AllPhoto");
            RemoveFromAlbumSelector_ListBox.Items.Refresh();

            AddToAlbumFinder_TextBox.Text = String.Empty;
        }

        private void AddAlbum(string albumName)
        {
            try
            {
                albums.Add(albumName, new List<int>());
                virtualDirectories.Add(albumName, new List<int>());
            }
            catch (ArgumentException)
            {
                System.Windows.MessageBox.Show("Album with this name already exist");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.ToString());
            }
        }

        private void GoBack_Click(object sender, RoutedEventArgs e)
        {
            //TODO: switch case selection of element to off
            AlbumNameInput.Visibility = Visibility.Collapsed;
            AddToAlbumListMenu.Visibility = Visibility.Collapsed;
            RemoveFromAlbumListMenu.Visibility = Visibility.Collapsed;

            AddToAlbumFinder_TextBox.Text = String.Empty;
        }

        private void SelectedAlbumChanged(object sender, SelectionChangedEventArgs e)
        {
            List<BitmapImage> imageList = new List<BitmapImage>();

            selectedAlbum = ((System.Windows.Controls.ListBox)sender).SelectedItem?.ToString() ?? String.Empty;

            foreach (var item in albums[selectedAlbum])
            {
                imageList.Add(bitmapPhotosList[item]);
            }

            var place = directories.FirstOrDefault(str => str.EndsWith(selectedAlbum, StringComparison.OrdinalIgnoreCase)) ?? "Virtual album";

            ChosenAlbum_TextBlock.Text = "Chosen album: \"" + selectedAlbum + "\"";
            ChosenAlbumPath_TextBlock.Text = "Folder place: " + place;
            FilesInAlbumCounter.Text = "In this album: " + albums[selectedAlbum].Count();

            PhotoListBox.ItemsSource = imageList;
            PhotoListBox.Items.Refresh();
        }

        private void DeletePhoto_Ckick(object sender, RoutedEventArgs e)
        {
            string path = selectedItemPath;

            List<BitmapImage> imageList = new List<BitmapImage>();

            imageList = (List<BitmapImage>)PhotoListBox.ItemsSource;

            foreach (var item in PhotoListBox.SelectedItems)
            {
                var id = PhotoListBox.Items.IndexOf(item);
                imageList.RemoveAt(id);
            }

            PhotoListBox.ItemsSource = imageList;

            //PhotoList.Items.Remove(PhotoList.Items[deleteItemIndex]);

            PhotoListBox.Items.Refresh();

            selectedItemPath = String.Empty; // нет настоящего пути файла, надо сохранять в список
            PhotoListBox.SelectedItem = null;

            //File.Delete(path); // TODO: Изза битмапа нет пути для удаления файла

            FilesCounter.Text = "Total images: " + bitmapPhotosList.Count().ToString();
        }

        private void ShowAddAlbumMenu_Click(object sender, RoutedEventArgs e)
        {
            AddToAlbumListMenu.Visibility = Visibility.Visible;
            AddToAlbumSelector_ListBox.ItemsSource = albums.Keys.Where(x => x != "AllPhoto" && x != AlbumListBox.SelectedItem.ToString());
            AddToAlbumFinder_TextBox.Focus();
        }

        private void ShowRemoveAlbumMenu_Click(object sender, RoutedEventArgs e)
        {
            RemoveFromAlbumListMenu.Visibility = Visibility.Visible;
            RemoveFromAlbumSelector_ListBox.ItemsSource = albums.Keys.Where(x => x != "AllPhoto");
            RemoveFromAlbumFinder_TextBox.Focus();
        }

        private void AlbumNameSearch(object sender, TextChangedEventArgs e)
        {
            string addInput = AddToAlbumFinder_TextBox.Text;
            string removeInput = RemoveFromAlbumFinder_TextBox.Text;

            if (String.IsNullOrWhiteSpace(addInput))
            {
                AddToAlbumSelector_ListBox.ItemsSource = albums.Keys.Where(x => x != "AllPhoto");
                AddToAlbumSelector_ListBox.Items.Refresh();
            }
            else
            {
                foreach (var item in albums.Keys)
                {
                    AddToAlbumSelector_ListBox.ItemsSource = albums.Keys.Where(x => x.Contains(addInput, StringComparison.OrdinalIgnoreCase) && x != "AllPhoto");
                }
            }

            if (String.IsNullOrWhiteSpace(removeInput))
            {
                RemoveFromAlbumSelector_ListBox.ItemsSource = albums.Keys.Where(x => x != "AllPhoto");
                RemoveFromAlbumSelector_ListBox.Items.Refresh();
            }
            else
            {
                foreach (var item in albums.Keys)
                {
                    RemoveFromAlbumSelector_ListBox.ItemsSource = albums.Keys.Where(x => x.Contains(removeInput, StringComparison.OrdinalIgnoreCase) && x != "AllPhoto");
                }
            }
        }

        private void AddToSelectedAlbum_Click(object sender, RoutedEventArgs e)
        {
            AddToAlbumListMenu.Visibility = Visibility.Collapsed;

            foreach (string albumName in AddToAlbumSelector_ListBox.SelectedItems)
            {
                foreach (BitmapImage item in PhotoListBox.SelectedItems)
                {
                    int photoID = bitmapPhotosList.IndexOf(item);
                    albums[albumName].Add(photoID);
                }

                if (virtualDirectories.Keys.Contains(albumName))
                {
                    SaveAsFile();
                }
            }
        }

        private void RemoveFromSelectedAlbum_Click(object sender, RoutedEventArgs e)
        {
            RemoveFromAlbumListMenu.Visibility = Visibility.Collapsed;

            List<BitmapImage> imageList = new List<BitmapImage>();

            foreach (string albumName in RemoveFromAlbumSelector_ListBox.SelectedItems)
            {
                foreach (BitmapImage item in PhotoListBox.SelectedItems)
                {
                    int photoID = bitmapPhotosList.IndexOf(item);
                    albums[albumName].Remove(photoID);
                }

                if (virtualDirectories.Keys.Contains(albumName))
                {
                    SaveAsFile();
                }
            }

            foreach (string albumName in RemoveFromAlbumSelector_ListBox.SelectedItems)
            {
                if (selectedAlbum == albumName)
                {
                    imageList.Clear();


                    for (int i = 0; i < albums[selectedAlbum].Count(); i++)
                    {
                        int index = albums[selectedAlbum][i];

                        imageList.Add(bitmapPhotosList[index]);
                    }

                    PhotoListBox.ItemsSource = imageList;
                    PhotoListBox.Items.Refresh();
                }
            }
        }

        private void PhotoListBox_SetSelectedItem(object sender, SelectionChangedEventArgs e)
        {
            selectedItemPath = ((System.Windows.Controls.ListBox)sender).SelectedItem?.ToString() ?? String.Empty;
            selectedItemIndex = ((System.Windows.Controls.ListBox)sender).SelectedIndex;
        }

        private void OnListViewItemPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            Trace.WriteLine("Preview MouseRightButtonDown");

            e.Handled = true;
        }

        private void SaveAsFile()
        {

            Dictionary<string, List<int>> albumsToSave = new Dictionary<string, List<int>>();

            foreach (var album in albums.Keys)
            {
                foreach (var al in virtualDirectories.Keys)
                {
                    if (album == al)
                    {
                        virtualDirectories[al] = albums[album];
                        albumsToSave.Add(al, albums[al]);
                    }
                }
            }

            ItemsToSave itemsToSave = new ItemsToSave();

            itemsToSave.FolderPath = folderPath;
            itemsToSave.VirtualDirectories = virtualDirectories;

            var json = JsonSerializer.Serialize(itemsToSave);
            var path = "\\bin\\Debug\\net6.0-windows\\SaveData.json";

            //File.WriteAllText(path, json);
        }

        private void ClearAlbum_Click(object sender, RoutedEventArgs e)
        {
            albums[selectedAlbum].Clear();
        }

        private void DeleteAlbum_Click(object sender, RoutedEventArgs e)
        {
            List<string> activeAlbums = new List<string>();

            activeAlbums = albums.Keys.Where(x => x != selectedAlbum).ToList();

            selectedAlbum = "AllPhoto";

            AlbumListBox.SelectedItem = "AllPhoto";
            AlbumListBox.ItemsSource = activeAlbums;
            AlbumListBox.Items.Refresh();
        }
    }
}
