using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
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
using System.Windows.Shapes;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

namespace YoutubeDownloader
{
    /// <summary>
    /// Interaction logic for Audio_Download_Details.xaml
    /// </summary>
    public partial class Audio_Download_Details : Window
    {
        private readonly StreamInfo _streamInfo;
        private readonly string _title;
        private readonly string _url;
        public Audio_Download_Details(StreamInfo stream, string title, string url)
        {
            InitializeComponent();
            _streamInfo = stream;
            _title = title;
            _url = url;

            DataContext = stream;
        }

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {

            try
            {
                //Choose a file path for saving
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = _title,
                    DefaultExt = ".mp3",
                    Title = "Save Youtube Video As",
                    Filter = "MP3 Files (*.mp3)|*.mp3|All Files (*.*)|*.*"
                };

                if (saveFileDialog.ShowDialog() == true)
                {

                    string filePath = saveFileDialog.FileName;


                    bool success = await DownloadAudio(_streamInfo, filePath, progressBar, progressText);
                    if (success)
                    {
                        MessageBox.Show("Download complete!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Download Failed", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Download Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }


        }

        public async Task<bool> DownloadAudio(StreamInfo streamInfo, string outputPath, ProgressBar progressBar, TextBlock progressText)
        {

            try
            {
                // Progress for downloading video and audio streams
                var audioDownloadProgress = new Progress<double>(p =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        progressBar.Value = p;
                        progressText.Text = $"Downloading video: {p:F2}%";
                    });
                });

                var youtubeClient = new YoutubeClient();
                var streamManifest = await youtubeClient.Videos.Streams.GetManifestAsync(_url);


                var streamInfoAudio = streamManifest.GetAudioOnlyStreams().Where(x => x.Bitrate == streamInfo.Bitrate).FirstOrDefault();
                var audioDownloadTask = youtubeClient.Videos.Streams.DownloadAsync(streamInfoAudio, outputPath, audioDownloadProgress).AsTask();

                await Task.WhenAll(audioDownloadTask);


                // Set progress bar to 100% for downloads
                Dispatcher.Invoke(() =>
                {
                    progressBar.Value = 100;
                    progressText.Text = "Download complete.";
                });
                return true;
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    progressBar.Value = 0;
                    progressText.Text = $"Error: {ex.Message}";
                });

                return false;

            }
        }
    }
}
