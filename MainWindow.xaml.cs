using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using HtmlAgilityPack;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using YoutubeExplode;
using YoutubeExplode.Converter;
using YoutubeExplode.Videos.Streams;

namespace YoutubeDownloader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static ObservableCollection<StreamInfo> VideoStreams { get; set; } = new();
        public static ObservableCollection<StreamInfo> AudioStreams { get; set; } = new();

        private static string savedURL = string.Empty;
        private static string videoTitle = String.Empty;
        private static HttpClient _httpClient = new HttpClient();
        private static YoutubeClient youtube = new YoutubeClient();




        public MainWindow()
        {
            InitializeComponent();

        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void Videos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void urldata_GotFocus(object sender, RoutedEventArgs e)
        {
            if (urldata.Text == "Enter URL Here")
            {

                urldata.Text = "";
                urldata.Foreground = new SolidColorBrush(Colors.Black);

            }
        }

        private void urldata_LostFocus(object sender, RoutedEventArgs e)
        {

            if (string.IsNullOrWhiteSpace(urldata.Text))
            {
                urldata.Text = "Enter URL Here";
                urldata.Foreground = new SolidColorBrush(Colors.Gray);
            }

        }

        private async void urldata_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {

                savedURL = urldata.Text;
                urldata.Text = "Loading Video Information...";

                DataContext = this;

                videoTitle = await GrabVideoTitle();
                await AudioVideoAvailable(savedURL);
                urldata.Text = "Video Loaded!";

            }
        }

        public static async Task<string> GrabVideoTitle()
        {



            using (HttpResponseMessage response = await _httpClient.GetAsync(savedURL, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();


                //grabbing title yt vid for file
                string responseBody = await response.Content.ReadAsStringAsync();
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(responseBody);

                var htmlBody = htmlDoc.DocumentNode.SelectSingleNode("//title");

                responseBody = htmlBody.InnerText;

                //shorten title so it's readable
                string videoTitle = ShortenTitle(responseBody);

                return videoTitle;


            }

        }


        public static string ShortenTitle(string x)
        {
            int iLength = x.Length;
            int newLength = iLength - 10;
            string newString = x.Remove(newLength);

            //gets all invalid file name characters and takes them out of string
            char[] invalidChars = System.IO.Path.GetInvalidFileNameChars();

            newString = new string(newString.Where(c => !invalidChars.Contains(c)).ToArray());

            return newString;

        }



        public static async Task AudioVideoAvailable(string url)
        {
            var streamManifest = await youtube.Videos.Streams.GetManifestAsync(url);
            foreach (var stream in streamManifest.GetVideoStreams())
            {
                VideoStreams.Add(new StreamInfo
                {
                    Quality = stream.VideoQuality.Label,
                    Resolution = $"{stream.VideoResolution.Width}x{stream.VideoResolution.Height}",
                    Bitrate = stream.Bitrate.BitsPerSecond,
                    Url = stream.Url


                });
            }

            foreach (var stream in streamManifest.GetAudioStreams())
            {

                AudioStreams.Add(new StreamInfo
                {

                    Quality = "Audio Only",
                    Resolution = "N/A",
                    Bitrate = stream.Bitrate.BitsPerSecond,
                    Url = stream.Url
                });
            }
        }
    }

    public class StreamInfo
    {
        public string Quality { get; set; }
        public string Resolution { get; set; }
        public long Bitrate { get; set; }
        public string Url { get; set; }
    }
}