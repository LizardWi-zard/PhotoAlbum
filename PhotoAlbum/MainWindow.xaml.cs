using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using OpenFileDialog = System.Windows.Forms.OpenFileDialog;
using Path = System.IO.Path;
using OpenDialogResult = System.Windows.Forms.DialogResult;
using System.Windows.Forms;

namespace PhotoAlbum
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Dictionary<string, List<int>> albums = new Dictionary<string, List<int>>();

        List<DriveInfo> readyDrives = new List<DriveInfo>();
        List<BitmapImage> bitmapPhotosList = new List<BitmapImage>();

        List<string> directories = new List<string>();
        List<string> directoriesNames = new List<string>();

        string selectedItemPath;
        string folderPath;
        string selectedAlbum = "AllPhoto";

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
    }
}
