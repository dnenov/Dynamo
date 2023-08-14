using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Navigation;

namespace Dynamo.PackageDetails
{
    public partial class PackageDetailsView : UserControl
    {
        public event EventHandler Closed;

        public PackageDetailsView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Allows for mousewheel scrolling in the DataGrid.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void VersionsDataGrid_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Handled) return;
            
            e.Handled = true;
            var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = MouseWheelEvent,
                Source = sender
            };
            var parent = ((Control)sender).Parent as UIElement;
            parent?.RaiseEvent(eventArg);
        }

        private void FrameworkElement_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            MainScrollViewer.ScrollToTop();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                if (!e.Uri.IsAbsoluteUri)
                {
                    var attemptRequest = string.Concat("www.", e.Uri.ToString());
                    System.Diagnostics.Process.Start(new ProcessStartInfo(attemptRequest) { UseShellExecute = true });
                }
                else
                {
                    System.Diagnostics.Process.Start(new ProcessStartInfo(e.Uri.ToString()) { UseShellExecute = true });
                }
            }
            catch (Exception) { }
            e.Handled = true;
        }

        private void CloseButton_OnClick(object sender, RoutedEventArgs e)
        {
            Closed?.Invoke(this, EventArgs.Empty);
        }

        private void PackageDetailsView_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var packageDetailsViewModel = this.DataContext as PackageDetailsViewModel;
            var descriptionText = packageDetailsViewModel.PackageDescription;
            if (!string.IsNullOrEmpty(descriptionText))
            {
                try
                {
                    return; // currently not implemented (waiting for approval)
                    AddHyperlinksToTextBlock(this.DescriptionBody, descriptionText);
                }
                catch(Exception ex)
                {
                    Console.WriteLine("Package Detail Extension Log");
                    Console.WriteLine(ex.Message);
                }
            }
        }

        /// <summary>
        /// Turns a plain text to series of runs and hyperlinks
        /// and assigns it onto a TextBlock element
        /// </summary>
        /// <param name="textBlock">the TextBlock element to assign to</param>
        /// <param name="text">the plain text to convert</param>
        private void AddHyperlinksToTextBlock(TextBlock textBlock, string text)
        {
            // Clear previous results   
            if (textBlock == null) return;
            textBlock.Inlines.Clear();

            MatchCollection matches = GetURLMatchesFromString(text);

            int carryOver = 0;

            foreach (Match match in matches)
            {
                string url = match.Groups["url"].Value;
                int startIndex = match.Index - carryOver;
                int length = match.Length;

                // Add the non-hyperlink text before the URL
                if (startIndex > 0)
                {
                    textBlock.Inlines.Add(new Run(text.Substring(0, startIndex)));
                }

                if (!string.IsNullOrEmpty(url))
                {
                    // Add the hyperlink
                    Hyperlink hyperlink = new Hyperlink(new Run(url));
                    Uri result;
                    if (Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out result))
                    {
                        // the url is valid
                        hyperlink.NavigateUri = result;
                    }
                    hyperlink.RequestNavigate += Hyperlink_RequestNavigate;
                    textBlock.Inlines.Add(hyperlink);
                }

                // Remove the processed part of the text
                text = text.Substring(startIndex + length);
                carryOver += startIndex + length;
            }

            // Add any remaining text (after the last hyperlink)
            if (!string.IsNullOrEmpty(text))
            {
                textBlock.Inlines.Add(new Run(text));
            }
        }

        /// <summary>
        /// Detect URL regex
        /// </summary>
        /// <param name="text">The string to find URL matches from</param>
        /// <returns></returns>
        internal static MatchCollection GetURLMatchesFromString(string text)
        {
            // Regular expression pattern to detect URLs
            // only with prefix
            string pattern = @"(?<url>(ftp:\/\/|www\.|https?:\/\/){1}[a-zA-Z0-9u00a1ffff0-]{2,}\.[a-zA-Z0-9u00a1ffff0-]{2,}(\S*))";
            // without prefix
            //string pattern = @"(?<url>((http|ftp|https):\/\/)?([\w_-]+(?:(?:\.[\w_-]+)+))([\w.,@?^=%&:\/~+#-]*[\w@?^=%&\/~+#-])?)";

            MatchCollection matches = Regex.Matches(text, pattern);

            return matches;
        }
    }
}
