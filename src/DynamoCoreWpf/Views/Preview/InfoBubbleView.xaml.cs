using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Dynamo.Configuration;
using Dynamo.Logging;
using Dynamo.UI;
using Dynamo.Utilities;
using Dynamo.ViewModels;
using Dynamo.Wpf.UI;
using Dynamo.Wpf.Utilities;
using InfoBubbleViewModel = Dynamo.ViewModels.InfoBubbleViewModel;

namespace Dynamo.Controls
{
    /// <summary>
    /// Interaction logic for PreviewInfoBubble.xaml
    /// </summary>
    public partial class InfoBubbleView : UserControl
    {
        #region Properties

        private bool isResizing = false;
        private bool isResizeHeight = false;
        private bool isResizeWidth = false;

        private InfoBubbleViewModel viewModel = null;

        public InfoBubbleViewModel ViewModel
        {
            get { return viewModel; }
            set
            {
                if (viewModel == null)
                {
                    viewModel = value;
                    viewModel.PropertyChanged += ViewModel_PropertyChanged;
                    viewModel.RequestAction += InfoBubbleRequestAction;
                }
            }
        }

        private double contentMaxWidth;
        public double ContentMaxWidth
        {
            get { return contentMaxWidth; }
            set { contentMaxWidth = value; }
        }

        private double contentMaxHeight;
        public double ContentMaxHeight
        {
            get { return contentMaxHeight; }
            set { contentMaxHeight = value; }
        }

        private System.Windows.Thickness contentMargin;
        public System.Windows.Thickness ContentMargin
        {
            get { return contentMargin; }
            set { contentMargin = value; }
        }

        private double contentFontSize;
        public double ContentFontSize
        {
            get { return contentFontSize; }
            set { contentFontSize = value; }
        }

        private FontWeight contentFontWeight;
        public FontWeight ContentFontWeight
        {
            get { return contentFontWeight; }
            set { contentFontWeight = value; }
        }

        private SolidColorBrush contentForeground;
        public SolidColorBrush ContentForeground
        {
            get { return contentForeground; }
            set { contentForeground = value; }
        }

        private double preview_LastMaxWidth;
        private double preview_LastMaxHeight;

        // When a NodeModel is removed, WPF places the NodeView into a "disconnected"
        // state (i.e. NodeView.DataContext becomes "DisconnectedItem") before 
        // eventually removing the view. This is the result of the host canvas being 
        // virtualized. This property is used by InfoBubbleView to determine if it should 
        // still continue to access the InfoBubbleViewModel that it is bound to.
        private bool IsDisconnected { get { return (this.ViewModel == null); } }

        private Hyperlink hyperlink;

        #endregion

        #region Storyboards
        private Storyboard fadeInStoryBoard;
        private Storyboard fadeOutStoryBoard;
        #endregion

        /// <summary>
        /// Used to present useful/important information to user when the node is in Error or Warning state.
        /// </summary>
        public InfoBubbleView()
        {
            InitializeComponent();

            // Make sure to unsubscribe to the event handlers
            Unloaded += (s, e) =>
            {
                Dispose();
            };

            fadeInStoryBoard = (Storyboard)FindResource("fadeInStoryBoard");
            fadeOutStoryBoard = (Storyboard)FindResource("fadeOutStoryBoard");

            ContentFontSize = Configurations.PreviewTextFontSize;
            preview_LastMaxWidth = double.MaxValue;
            preview_LastMaxHeight = double.MaxValue;

            this.DataContextChanged += InfoBubbleView_DataContextChanged;
        }

        private void InfoBubbleWindowUserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                switch (ViewModel.InfoBubbleState)
                {
                    case InfoBubbleViewModel.State.Minimized:
                        mainGrid.Visibility = Visibility.Collapsed;
                        mainGrid.Opacity = 0;
                        break;
                    case InfoBubbleViewModel.State.Pinned:
                        mainGrid.Visibility = Visibility.Visible;
                        mainGrid.Opacity = Configurations.MaxOpacity;
                        UpdateStyle();
                        UpdateContent();
                        UpdateShape();
                        UpdatePosition();
                        break;
                }
            }
        }

        #region FadeIn FadeOut Event Handling

        private void CountDownDoubleAnimation_Completed(object sender, EventArgs e)
        {
            //Console.WriteLine("FadeOut done");
            fadeInStoryBoard.Stop(this);
            fadeOutStoryBoard.Stop(this);

            mainGrid.Opacity = 0;
            mainGrid.Visibility = Visibility.Collapsed;
        }

        private void CountUpDoubleAnimation_Completed(object sender, EventArgs e)
        {
            //Console.WriteLine("FadeIn done");
            mainGrid.Opacity = Configurations.MaxOpacity;
            mainGrid.Visibility = Visibility.Visible;
        }
        #endregion

        private void InfoBubbleView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            ViewModel = e.NewValue as InfoBubbleViewModel;
            UpdateContent();
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // The fix for the following issue was previously performed in 
            // NodeModel. This is shifted over to infobubble to centralize 
            // the issue until code restructuring is completed.
            //
            // This is a temporarily measure, it work by dispatching the 
            // work to UI thread when info bubble UI values need to be 
            // modified by background evaluation thread.
            // To completely solve this, changes affecting UI values should be 
            // restructured into UI Binding in order for things to be thread 
            // safe. 
            // The above mentioned issue is being documented in:
            //
            //      http://adsk-oss.myjetbrains.com/youtrack/issue/MAGN-847
            //
            Action propertyChanged = (() =>
            {
                switch (e.PropertyName)
                {
                    case "Content":
                        UpdateStyle();
                        UpdateContent();
                        UpdateShape();
                        UpdatePosition();
                        break;

                    case "TargetTopLeft":
                    case "TargetBotRight":
                        UpdatePosition();
                        break;

                    case "ConnectingDirection":
                        UpdateShape();
                        UpdatePosition();
                        break;

                    case "InfoBubbleState":
                        UpdateShape();
                        UpdatePosition();

                        HandleInfoBubbleStateChanged(ViewModel.InfoBubbleState);
                        break;

                    case "InfoBubbleStyle":
                        UpdateStyle();
                        break;
                }
            });

            if (this.ViewModel.DynamoViewModel.UIDispatcher != null &&
                this.ViewModel.DynamoViewModel.UIDispatcher != null)
            {
                if (this.ViewModel.DynamoViewModel.UIDispatcher.CheckAccess())
                    propertyChanged();
                else
                    this.ViewModel.DynamoViewModel.UIDispatcher.BeginInvoke(propertyChanged);
            }
        }

        private void HandleInfoBubbleStateChanged(InfoBubbleViewModel.State state)
        {
            switch (state)
            {
                case InfoBubbleViewModel.State.Minimized:
                    // Changing to Minimized
                    this.HideInfoBubble();
                    break;

                case InfoBubbleViewModel.State.Pinned:
                    // Changing to Pinned
                    this.ShowInfoBubble();
                    break;
            }
        }

        #region Update Style

        private void UpdateStyle()
        {
            InfoBubbleViewModel.Style style = ViewModel.InfoBubbleStyle;
            ViewModel.LimitedDirection = InfoBubbleViewModel.Direction.None;

            switch (style)
            {
                case InfoBubbleViewModel.Style.Warning:
                    SetStyle_Warning();
                    break;
                case InfoBubbleViewModel.Style.WarningCondensed:
                    SetStyle_WarningCondensed();
                    break;
                case InfoBubbleViewModel.Style.Error:
                    SetStyle_Error();
                    break;
                case InfoBubbleViewModel.Style.ErrorCondensed:
                    SetStyle_ErrorCondensed();
                    break;
                case InfoBubbleViewModel.Style.None:
                    ViewModel.DynamoViewModel.Model.Logger.Log("InfoWindow didn't have a style (456B24E0F400)");
                    break;
            }
        }

        private void SetStyle_Warning()
        {
            backgroundPolygon.Fill = FrozenResources.WarningFrameFill;
            backgroundPolygon.StrokeThickness = Configurations.ErrorFrameStrokeThickness;
            backgroundPolygon.Stroke = FrozenResources.WarningFrameStrokeColor;

            ContentContainer.MaxWidth = Configurations.ErrorMaxWidth;
            ContentContainer.MaxHeight = Configurations.ErrorMaxHeight;

            ContentMargin = Configurations.ErrorContentMargin.AsWindowsType();
            ContentMaxWidth = Configurations.ErrorContentMaxWidth;
            ContentMaxHeight = Configurations.ErrorContentMaxHeight;

            ContentFontSize = Configurations.ErrorTextFontSize;
            ContentForeground = FrozenResources.WarningTextForeground;
            ContentFontWeight = VisualConfigurations.ErrorTextFontWeight;
        }

        private void SetStyle_WarningCondensed()
        {
            backgroundPolygon.Fill = FrozenResources.WarningFrameFill;
            backgroundPolygon.StrokeThickness = Configurations.ErrorFrameStrokeThickness;
            backgroundPolygon.Stroke = FrozenResources.WarningFrameStrokeColor;

            ContentContainer.MaxWidth = Configurations.ErrorCondensedMaxWidth;
            ContentContainer.MinWidth = Configurations.ErrorCondensedMinWidth;
            ContentContainer.MaxHeight = Configurations.ErrorCondensedMaxHeight;
            ContentContainer.MinHeight = Configurations.ErrorCondensedMinHeight;

            ContentMargin = Configurations.ErrorContentMargin.AsWindowsType();
            ContentMaxWidth = Configurations.ErrorCondensedContentMaxWidth;
            ContentMaxHeight = Configurations.ErrorCondensedContentMaxHeight;

            ContentFontSize = Configurations.ErrorTextFontSize;
            ContentForeground = FrozenResources.WarningTextForeground;
            ContentFontWeight = VisualConfigurations.ErrorTextFontWeight;
        }

        private void SetStyle_Error()
        {
            backgroundPolygon.Fill = FrozenResources.ErrorFrameFill;
            backgroundPolygon.StrokeThickness = Configurations.ErrorFrameStrokeThickness;
            backgroundPolygon.Stroke = FrozenResources.ErrorFrameStrokeColor;

            ContentContainer.MaxWidth = Configurations.ErrorMaxWidth;
            ContentContainer.MaxHeight = Configurations.ErrorMaxHeight;

            ContentMargin = Configurations.ErrorContentMargin.AsWindowsType();
            ContentMaxWidth = Configurations.ErrorContentMaxWidth;
            ContentMaxHeight = Configurations.ErrorContentMaxHeight;

            ContentFontSize = Configurations.ErrorTextFontSize;
            ContentForeground = FrozenResources.ErrorTextForeground;
            ContentFontWeight = VisualConfigurations.ErrorTextFontWeight;
        }

        private void SetStyle_ErrorCondensed()
        {
            backgroundPolygon.Fill = FrozenResources.ErrorFrameFill;
            backgroundPolygon.StrokeThickness = Configurations.ErrorFrameStrokeThickness;
            backgroundPolygon.Stroke = FrozenResources.ErrorFrameStrokeColor;

            ContentContainer.MaxWidth = Configurations.ErrorCondensedMaxWidth;
            ContentContainer.MinWidth = Configurations.ErrorCondensedMinWidth;
            ContentContainer.MaxHeight = Configurations.ErrorCondensedMaxHeight;
            ContentContainer.MinHeight = Configurations.ErrorCondensedMinHeight;

            ContentMargin = Configurations.ErrorContentMargin.AsWindowsType();
            ContentMaxWidth = Configurations.ErrorCondensedContentMaxWidth;
            ContentMaxHeight = Configurations.ErrorCondensedContentMaxHeight;

            ContentFontSize = Configurations.ErrorTextFontSize;
            ContentForeground = FrozenResources.ErrorTextForeground;
            ContentFontWeight = VisualConfigurations.ErrorTextFontWeight;
        }

        #endregion

        #region Update Content

        private void UpdateContent()
        {
            //The reason of changing the content from the code behind like this is due to a bug of WPF
            //  The bug if when you set the max width of an existing text box and then try to get the 
            //  expected size of it by using TextBox.Measure(..) method it will return the wrong value.
            //  The only solution that I can come up for now is clean the StackPanel content and 
            //  then add a new TextBox to it

            ContentContainer.Children.Clear();

            if (ViewModel == null) return;


            if (ViewModel.Content == "...")
            {
                #region Draw Icon

                Rectangle r1 = new Rectangle();
                r1.Fill = Brushes.Black;
                r1.Height = 1;
                r1.Width = 16;
                r1.UseLayoutRounding = true;

                Rectangle r2 = new Rectangle();
                r2.Fill = Brushes.Black;
                r2.Height = 1;
                r2.Width = 16;
                r2.UseLayoutRounding = true;

                Rectangle r3 = new Rectangle();
                r3.Fill = Brushes.Black;
                r3.Height = 1;
                r3.Width = 10;
                r3.UseLayoutRounding = true;
                r3.HorizontalAlignment = HorizontalAlignment.Left;

                Grid myGrid = new Grid();
                myGrid.HorizontalAlignment = HorizontalAlignment.Stretch;
                myGrid.VerticalAlignment = VerticalAlignment.Stretch;
                myGrid.Background = Brushes.Transparent;
                myGrid.Margin = ContentMargin;
                myGrid.MaxHeight = ContentMaxHeight;
                myGrid.MaxWidth = contentMaxWidth;

                // Create row definitions.
                RowDefinition rowDefinition1 = new RowDefinition();
                RowDefinition rowDefinition2 = new RowDefinition();
                RowDefinition rowDefinition3 = new RowDefinition();
                rowDefinition1.Height = new GridLength(3);
                rowDefinition2.Height = new GridLength(3);
                rowDefinition3.Height = new GridLength(3);

                myGrid.RowDefinitions.Add(rowDefinition1);
                myGrid.RowDefinitions.Add(rowDefinition2);
                myGrid.RowDefinitions.Add(rowDefinition3);
                myGrid.Children.Add(r1);
                Grid.SetRow(r1, 0);
                myGrid.Children.Add(r2);
                Grid.SetRow(r2, 1);
                myGrid.Children.Add(r3);
                Grid.SetRow(r3, 2);
                myGrid.UseLayoutRounding = true;

                ContentContainer.Children.Add(myGrid);

                #endregion
            }
            else
            {
                string content = ViewModel.Content;
                if (ViewModel.InfoBubbleStyle == InfoBubbleViewModel.Style.Warning)
                {
                    content = Wpf.Properties.Resources.InfoBubbleWarning + content;
                }
                else if (ViewModel.InfoBubbleStyle == InfoBubbleViewModel.Style.Error)
                {
                    content = Wpf.Properties.Resources.InfoBubbleError + content;
                }

                TextBox textBox = GetNewTextBox(content);
                ContentContainer.Children.Add(textBox);

                if (viewModel.DocumentationLink != null)
                {
                    TextBlock linkBlock = GetHyperlinkTextBlock();
                    ContentContainer.Children.Add(linkBlock);
                }
            }
        }

        private void RequestNavigateToDocumentationLinkHandler(object sender, RequestNavigateEventArgs e)
        {
            this.viewModel.OpenDocumentationLinkCommand.Execute(e.Uri);
        }

        private TextBox GetNewTextBox(string text)
        {
            TextBox textBox = new TextBox();
            textBox.Text = text;
            textBox.TextWrapping = TextWrapping.Wrap;

            textBox.Margin = ContentMargin;
            textBox.MaxHeight = ContentMaxHeight;
            textBox.MaxWidth = ContentMaxWidth;

            textBox.Foreground = ContentForeground;
            textBox.FontWeight = ContentFontWeight;
            textBox.FontSize = ContentFontSize;

            var font = SharedDictionaryManager.DynamoModernDictionary["OpenSansRegular"];
            textBox.FontFamily = font as FontFamily;

            textBox.Background = Brushes.Transparent;
            textBox.IsReadOnly = true;
            textBox.BorderThickness = new System.Windows.Thickness(0);

            textBox.HorizontalAlignment = HorizontalAlignment.Center;
            textBox.VerticalAlignment = VerticalAlignment.Center;

            textBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

            return textBox;
        }

        private void UpdateHyperlink()
        {
            // if we have already generated a hyperlink then don't create a new one, but simply update the link uri
            // this is to avoid losing track of objects that have event handler subscriptions
            if (this.hyperlink != null)
            {
                this.hyperlink.NavigateUri = viewModel.DocumentationLink;
                return;
            }

            this.hyperlink = new Hyperlink();
            this.hyperlink.NavigateUri = viewModel.DocumentationLink;
            this.hyperlink.RequestNavigate += RequestNavigateToDocumentationLinkHandler;
            this.hyperlink.Inlines.Add(Wpf.Properties.Resources.InfoBubbleDocumentationLinkText);
        }

        private TextBlock GetHyperlinkTextBlock()
        {
            this.UpdateHyperlink();

            var font = SharedDictionaryManager.DynamoModernDictionary["OpenSansRegular"];
            TextBlock linkBlock = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,

                Margin = ContentMargin,
                MaxHeight = ContentMaxHeight,
                MaxWidth = ContentMaxWidth,

                Foreground = ContentForeground,
                FontWeight = ContentFontWeight,
                FontSize = ContentFontSize,

                Background = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,

                FontFamily = font as FontFamily
            };

            linkBlock.Inlines.Add(this.hyperlink);

            return linkBlock;
        }
        #endregion

        #region Update Shape

        private void UpdateShape()
        {
            ContentContainer.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double estimatedHeight = ContentContainer.DesiredSize.Height;
            double estimatedWidth = ContentContainer.DesiredSize.Width;

            PointCollection framePoints = new PointCollection();
            switch (ViewModel.InfoBubbleStyle)
            {
                case InfoBubbleViewModel.Style.Warning:
                case InfoBubbleViewModel.Style.WarningCondensed:
                case InfoBubbleViewModel.Style.Error:
                case InfoBubbleViewModel.Style.ErrorCondensed:
                    framePoints = GetFramePoints_Error(estimatedHeight, estimatedWidth);
                    break;
                case InfoBubbleViewModel.Style.None:
                    break;
            }

            if (framePoints != null)
                backgroundPolygon.Points = framePoints;
        }

        private PointCollection GetFramePoints_NodeTooltipConnectBottom(double estimatedHeight, double estimatedWidth)
        {
            PointCollection pointCollection = new PointCollection();

            double arrowHeight = Configurations.NodeTooltipArrowHeight_SideConnecting;
            double arrowWidth = Configurations.NodeTooltipArrowWidth_SideConnecting;

            if (ViewModel.TargetTopLeft.Y - estimatedHeight >= 40)
            {
                arrowHeight = Configurations.NodeTooltipArrowHeight_BottomConnecting;
                arrowWidth = Configurations.NodeTooltipArrowWidth_BottomConnecting;

                ViewModel.LimitedDirection = InfoBubbleViewModel.Direction.None;
                pointCollection.Add(new Point(estimatedWidth, 0));
                pointCollection.Add(new Point(0, 0));
                pointCollection.Add(new Point(0, estimatedHeight - arrowHeight));
                pointCollection.Add(new Point((estimatedWidth / 2) - arrowWidth / 2, estimatedHeight - arrowHeight));
                pointCollection.Add(new Point(estimatedWidth / 2, estimatedHeight));
                pointCollection.Add(new Point((estimatedWidth / 2) + (arrowWidth / 2), estimatedHeight - arrowHeight));
                pointCollection.Add(new Point(estimatedWidth, estimatedHeight - arrowHeight));
            }
            else if (ViewModel.TargetBotRight.X + estimatedWidth <= this.ViewModel.DynamoViewModel.WorkspaceActualWidth)
            {
                ViewModel.LimitedDirection = InfoBubbleViewModel.Direction.Top;
                contentMargin = Configurations.NodeTooltipContentMarginLeft.AsWindowsType();
                //UpdateContent(Content);

                pointCollection.Add(new Point(estimatedWidth, 0));
                pointCollection.Add(new Point(0, 0));
                pointCollection.Add(new Point(arrowWidth, arrowHeight / 2));
                pointCollection.Add(new Point(arrowWidth, estimatedHeight));
                pointCollection.Add(new Point(estimatedWidth, estimatedHeight));
            }
            else
            {
                ViewModel.LimitedDirection = InfoBubbleViewModel.Direction.TopRight;
                contentMargin = Configurations.NodeTooltipContentMarginRight.AsWindowsType();
                //UpdateContent(Content);

                pointCollection.Add(new Point(estimatedWidth, 0));
                pointCollection.Add(new Point(0, 0));
                pointCollection.Add(new Point(0, estimatedHeight));
                pointCollection.Add(new Point(estimatedWidth - arrowWidth, estimatedHeight));
                pointCollection.Add(new Point(estimatedWidth - arrowWidth, arrowHeight / 2));

            }

            return pointCollection;
        }

        private PointCollection GetFramePoints_NodeTooltipConnectLeft(double estimatedHeight, double estimatedWidth)
        {
            if (ViewModel.TargetBotRight.X + estimatedWidth > this.ViewModel.DynamoViewModel.WorkspaceActualWidth)
            {
                ViewModel.LimitedDirection = InfoBubbleViewModel.Direction.Right;
                contentMargin = Configurations.NodeTooltipContentMarginRight.AsWindowsType();

                return GeneratePointCollection_TooltipConnectRight(estimatedHeight, estimatedWidth);
            }
            else
                return GeneratePointCollection_TooltipConnectLeft(estimatedHeight, estimatedWidth);
        }

        private PointCollection GetFramePoints_NodeTooltipConnectRight(double estimatedHeight, double estimatedWidth)
        {
            if (ViewModel.TargetTopLeft.X - estimatedWidth < 0)
            {
                ViewModel.LimitedDirection = InfoBubbleViewModel.Direction.Left;
                contentMargin = Configurations.NodeTooltipContentMarginLeft.AsWindowsType();

                return GeneratePointCollection_TooltipConnectLeft(estimatedHeight, estimatedWidth);
            }
            else
                return GeneratePointCollection_TooltipConnectRight(estimatedHeight, estimatedWidth);
        }

        private PointCollection GeneratePointCollection_TooltipConnectLeft(double estimatedHeight, double estimatedWidth)
        {
            PointCollection pointCollection = new PointCollection();

            double arrowHeight = Configurations.NodeTooltipArrowHeight_SideConnecting;
            double arrowWidth = Configurations.NodeTooltipArrowWidth_SideConnecting;

            pointCollection.Add(new Point(estimatedWidth, 0));
            pointCollection.Add(new Point(arrowWidth, 0));
            pointCollection.Add(new Point(arrowWidth, estimatedHeight / 2 - arrowHeight / 2));
            pointCollection.Add(new Point(0, estimatedHeight / 2));
            pointCollection.Add(new Point(arrowWidth, estimatedHeight / 2 + arrowHeight / 2));
            pointCollection.Add(new Point(arrowWidth, estimatedHeight));
            pointCollection.Add(new Point(estimatedWidth, estimatedHeight));

            return pointCollection;
        }

        private PointCollection GeneratePointCollection_TooltipConnectRight(double estimatedHeight, double estimatedWidth)
        {
            PointCollection pointCollection = new PointCollection();

            double arrowHeight = Configurations.NodeTooltipArrowHeight_SideConnecting;
            double arrowWidth = Configurations.NodeTooltipArrowWidth_SideConnecting;

            pointCollection.Add(new Point(estimatedWidth - arrowWidth, 0));
            pointCollection.Add(new Point(0, 0));
            pointCollection.Add(new Point(0, estimatedHeight));
            pointCollection.Add(new Point(estimatedWidth - arrowWidth, estimatedHeight));
            pointCollection.Add(new Point(estimatedWidth - arrowWidth, estimatedHeight / 2 + arrowHeight / 2));
            pointCollection.Add(new Point(estimatedWidth, estimatedHeight / 2));
            pointCollection.Add(new Point(estimatedWidth - arrowWidth, estimatedHeight / 2 - arrowHeight / 2));

            return pointCollection;
        }

        private PointCollection GetFramePoints_Error(double estimatedHeight, double estimatedWidth)
        {
            double arrowHeight = Configurations.ErrorArrowHeight;
            double arrowWidth = Configurations.ErrorArrowWidth;

            PointCollection pointCollection = new PointCollection();
            pointCollection.Add(new Point(estimatedWidth, 0));
            pointCollection.Add(new Point(0, 0));
            pointCollection.Add(new Point(0, estimatedHeight - arrowHeight));
            pointCollection.Add(new Point((estimatedWidth / 2) - arrowWidth / 2, estimatedHeight - arrowHeight));
            pointCollection.Add(new Point(estimatedWidth / 2, estimatedHeight));
            pointCollection.Add(new Point((estimatedWidth / 2) + arrowWidth / 2, estimatedHeight - arrowHeight));
            pointCollection.Add(new Point(estimatedWidth, estimatedHeight - arrowHeight));
            return pointCollection;
        }

        #endregion

        #region Update Position

        /// <summary>
        /// Ensures that the InfoBubbleView moves in tandem with the node it's attached to.
        /// </summary>
        private void UpdatePosition()
        {
            Canvas.SetTop(mainGrid, ViewModel.TargetTopLeft.Y);
            Canvas.SetLeft(mainGrid, ViewModel.TargetTopLeft.X);
        }
        
        #endregion

        #region Resize

        private void Resize(object parameter)
        {
            Point deltaPoint = (Point)parameter;

            double newMaxWidth = deltaPoint.X;
            double newMaxHeight = deltaPoint.Y;

            if (deltaPoint.X != double.MaxValue && newMaxWidth >= Configurations.PreviewMinWidth &&
                newMaxWidth <= Configurations.PreviewMaxWidth)
            {
                ContentContainer.MaxWidth = newMaxWidth;
                contentMaxWidth = newMaxWidth - 10;
            }

            if (deltaPoint.Y != double.MaxValue && newMaxHeight >= Configurations.PreviewMinHeight)
            {
                ContentContainer.MaxHeight = newMaxHeight;
                contentMaxHeight = newMaxHeight - 17;
            }

            UpdateContent();
            UpdateShape();
            UpdatePosition();

            this.preview_LastMaxWidth = ContentContainer.MaxWidth;
            this.preview_LastMaxHeight = ContentContainer.MaxHeight;
        }

        #endregion

        private void ShowErrorBubbleFullContent()
        {
            if (this.IsDisconnected)
                return;

            InfoBubbleDataPacket data = new InfoBubbleDataPacket();
            if (ViewModel.InfoBubbleStyle == InfoBubbleViewModel.Style.ErrorCondensed)
            {
                data.Style = InfoBubbleViewModel.Style.Error;
            }
            else if (ViewModel.InfoBubbleStyle == InfoBubbleViewModel.Style.WarningCondensed)
            {
                data.Style = InfoBubbleViewModel.Style.Warning;
            }

            data.ConnectingDirection = InfoBubbleViewModel.Direction.Bottom;

            this.ViewModel.ShowFullContentCommand.Execute(data);
        }

        private void ShowErrorBubbleCondensedContent()
        {
            if (this.IsDisconnected)
                return;

            InfoBubbleDataPacket data = new InfoBubbleDataPacket();
            if (ViewModel.InfoBubbleStyle == InfoBubbleViewModel.Style.Error)
            {
                data.Style = InfoBubbleViewModel.Style.ErrorCondensed;
            }
            else if (ViewModel.InfoBubbleStyle == InfoBubbleViewModel.Style.Warning)
            {
                data.Style = InfoBubbleViewModel.Style.WarningCondensed;
            }

            data.ConnectingDirection = InfoBubbleViewModel.Direction.Bottom;

            this.ViewModel.ShowCondensedContentCommand.Execute(data);
        }

        private void InfoBubbleRequestAction(object sender, InfoBubbleEventArgs e)
        {
            switch (e.RequestType)
            {
                case InfoBubbleEventArgs.Request.Show:
                    ShowInfoBubble();
                    break;
                case InfoBubbleEventArgs.Request.Hide:
                    HideInfoBubble();
                    break;
                case InfoBubbleEventArgs.Request.FadeIn:
                    FadeInInfoBubble();
                    break;
                case InfoBubbleEventArgs.Request.FadeOut:
                    FadeOutInfoBubble();
                    break;
            }
        }

        private void ShowInfoBubble()
        {
            if (mainGrid.Visibility == System.Windows.Visibility.Collapsed)
            {
                mainGrid.Visibility = Visibility.Visible;
                // Run animation and skip it to end state i.e. MaxOpacity
                fadeInStoryBoard.Begin(this);
                fadeInStoryBoard.SkipToFill(this);
            }
        }

        // Hide bubble instantly
        private void HideInfoBubble()
        {
            if (mainGrid.Visibility == System.Windows.Visibility.Visible)
            {
                mainGrid.Visibility = Visibility.Collapsed;
                fadeOutStoryBoard.Begin(this);
                fadeOutStoryBoard.SkipToFill(this);
            }
        }

        private void FadeInInfoBubble()
        {
            if (this.IsDisconnected)
                return;

            if (this.ViewModel.DynamoViewModel.IsMouseDown ||
                !this.ViewModel.DynamoViewModel.CurrentSpaceViewModel.CanShowInfoBubble)
                return;

            fadeOutStoryBoard.Stop(this);
            mainGrid.Visibility = Visibility.Visible;
            fadeInStoryBoard.Begin(this);
        }

        private void FadeOutInfoBubble()
        {
            if (this.IsDisconnected)
                return;

            if (this.ViewModel.InfoBubbleState == InfoBubbleViewModel.State.Pinned)
                return;

            fadeInStoryBoard.Stop(this);
            fadeOutStoryBoard.Begin(this);
        }

        #region Mouse Event Handlers

        private void ContentContainer_MouseEnter(object sender, MouseEventArgs e)
        {
            if (this.IsDisconnected)
                return;

            if (ViewModel.InfoBubbleStyle == InfoBubbleViewModel.Style.ErrorCondensed ||
                ViewModel.InfoBubbleStyle == InfoBubbleViewModel.Style.WarningCondensed)
                ShowErrorBubbleFullContent();

            ShowInfoBubble();

            this.Cursor = CursorLibrary.GetCursor(CursorSet.Pointer);
        }

        private void InfoBubble_MouseLeave(object sender, MouseEventArgs e)
        {
            // It is possible for MouseLeave message (that was scheduled earlier) to reach
            // InfoBubbleView when it becomes disconnected from InfoBubbleViewModel (i.e. 
            // when the NodeModel it belongs is deleted by user). In this case, InfoBubbleView
            // should simply ignore the message, since the node is no longer valid.
            if (this.IsDisconnected)
                return;

            switch (ViewModel.InfoBubbleStyle)
            {
                case InfoBubbleViewModel.Style.Warning:
                case InfoBubbleViewModel.Style.Error:
                    ShowErrorBubbleCondensedContent();
                    break;

                default:
                    FadeOutInfoBubble();
                    break;
            }

            this.Cursor = CursorLibrary.GetCursor(CursorSet.Pointer);
        }

        private void InfoBubble_MouseEnter(object sender, MouseEventArgs e)
        {
            this.Cursor = CursorLibrary.GetCursor(CursorSet.Condense);
        }

        private void MainGrid_MouseMove(object sender, MouseEventArgs e)
        {
            if (this.IsDisconnected)
                return;

            if (!isResizing)
                return;

            Point mouseLocation = Mouse.GetPosition(mainGrid);
            if (!isResizeHeight)
                mouseLocation.Y = double.MaxValue;
            if (!isResizeWidth)
                mouseLocation.X = double.MaxValue;

            //ViewModel.ResizeCommand.Execute(mouseLocation);
            Resize(mouseLocation);
        }

        private void InfoBubble_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
        }

        /// <summary>
        /// Dispose function adding resubscribe logic
        /// </summary>
        public void Dispose()
        {
            viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            viewModel.RequestAction -= InfoBubbleRequestAction;

            // make sure we unsubscribe from handling the hyperlink click event
            if (this.hyperlink != null)
                this.hyperlink.RequestNavigate -= RequestNavigateToDocumentationLinkHandler;
        }

        private void ShowAllErrorsButton_Click(object sender, RoutedEventArgs e)
        {
            // If we're already expanded, this button collapses the border
            if (ViewModel.NodeErrorsVisibilityState == 
                InfoBubbleViewModel.NodeMessageVisibility.ShowAllMessages)
            {
                ViewModel.NodeErrorsVisibilityState = 
                    InfoBubbleViewModel.NodeMessageVisibility.CollapseMessages;
                ViewModel.NodeErrorsShowLessMessageVisible = false;
                return;
            }
            ViewModel.NodeErrorsVisibilityState =
                InfoBubbleViewModel.NodeMessageVisibility.ShowAllMessages;
            ViewModel.NodeErrorsShowLessMessageVisible = true;
        }

        private void ShowAllWarningsButton_Click(object sender, RoutedEventArgs e)
        {
            // If we're already expanded, this button collapses the border
            if (ViewModel.NodeWarningsVisibilityState ==
                InfoBubbleViewModel.NodeMessageVisibility.ShowAllMessages)
            {
                ViewModel.NodeWarningsVisibilityState =
                    InfoBubbleViewModel.NodeMessageVisibility.CollapseMessages;
                ViewModel.NodeWarningsShowLessMessageVisible = false;
                return;
            }
            ViewModel.NodeWarningsVisibilityState =
                InfoBubbleViewModel.NodeMessageVisibility.ShowAllMessages;
            ViewModel.NodeWarningsShowLessMessageVisible = true;
        }

        private void ShowAllInfoButton_Click(object sender, RoutedEventArgs e)
        {
            // If we're already expanded, this button collapses the border
            if (ViewModel.NodeInfoVisibilityState ==
                InfoBubbleViewModel.NodeMessageVisibility.ShowAllMessages)
            {
                ViewModel.NodeInfoVisibilityState = 
                    InfoBubbleViewModel.NodeMessageVisibility.CollapseMessages;
                ViewModel.NodeInfoShowLessMessageVisible = false;
                return;
            }
            ViewModel.NodeInfoVisibilityState =
                InfoBubbleViewModel.NodeMessageVisibility.ShowAllMessages;
            ViewModel.NodeInfoShowLessMessageVisible = true;
        }

        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            ScrollViewer scv = (ScrollViewer)sender;
            scv.ScrollToVerticalOffset(scv.VerticalOffset - e.Delta);
            e.Handled = true;
        }

        /// <summary>
        /// Retrieves all of a node's dismissed messages of a particular style.
        /// </summary>
        /// <param name="style"></param>
        /// <returns></returns>
        private ObservableCollection<InfoBubbleDataPacket> GetDismissedMessagesOfStyle(InfoBubbleViewModel.Style style)
        {
            return ViewModel.DismissedMessages.Where(x => x.Style == style).ToObservableCollection();
        }

        /// <summary>
        /// Clears out the node's collection of all dismissed messages of a particular style (e.g. Error).
        /// </summary>
        /// <param name="style"></param>
        private void ClearDismissedMessagesOfStyle(InfoBubbleViewModel.Style style)
        {
            ObservableCollection<InfoBubbleDataPacket> messagesToDismiss = GetDismissedMessagesOfStyle(style);
            
            for (int i = messagesToDismiss.Count - 1; i >= 0; i--)
            {
                ViewModel.DismissedMessages.RemoveAt(i);
            }
        }

        /// <summary>
        /// Repopulates the node's collection of dismissed messages.
        /// </summary>
        /// <param name="style"></param>
        private void RecreateDismissedMessagesOfStyle(InfoBubbleViewModel.Style style)
        {
            foreach (InfoBubbleDataPacket infoBubbleDataPacket in ViewModel.NodeMessages)
            {
                if (infoBubbleDataPacket.Style != style) continue;
                ViewModel.DismissedMessages.Add(infoBubbleDataPacket);
            }
        }

        /// <summary>
        /// Combines the clearing and repopulation functions of a node's dismissed messages collection.
        /// </summary>
        private void RefreshDismissedMessages(InfoBubbleViewModel.Style style)
        {
            ClearDismissedMessagesOfStyle(style);
            RecreateDismissedMessagesOfStyle(style);
        }

        /// <summary>
        /// Click event handler for when the user clicks 'Dismiss All' on a node's info messages.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DismissAllInfoButton_Click(object sender, RoutedEventArgs e)
        {
            // Clearing and recreating all existing info-level messages from the dismissed collection.
            RefreshDismissedMessages(InfoBubbleViewModel.Style.Info);

            ViewModel.RefreshNodeInformationalStateDisplay();
        }

        /// <summary>
        /// Click event handler for when the user clicks 'Dismiss All' on a node's warnings.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DismissAllWarningsButton_Click(object sender, RoutedEventArgs e)
        {
            // Clearing and recreating all existing warning-level messages from the dismissed collection.
            RefreshDismissedMessages(InfoBubbleViewModel.Style.Warning);
            RefreshDismissedMessages(InfoBubbleViewModel.Style.WarningCondensed);
            // Track dismiss warning event and how many warnings dismissed
            Analytics.TrackEvent(Actions.Dismiss, Categories.NodeOperations, "Warning", ViewModel.DismissedMessages.Count);
            ViewModel.RefreshNodeInformationalStateDisplay();
            viewModel.ValidateWorkspaceRunStatusMsg();
        }


        private void Border_OnMouseLeave(object sender, MouseEventArgs e)
        {
            if (!(sender is Border border)) return;

            InfoBubbleViewModel.NodeMessageVisibility nodeMessageVisibility = InfoBubbleViewModel.NodeMessageVisibility.Icon;
            HorizontalAlignment horizontalAlignment = HorizontalAlignment.Left;

            switch (border.Name)
            {
                case "InfoBorder":
                    InfoBorder.HorizontalAlignment = horizontalAlignment;
                    ViewModel.NodeInfoVisibilityState = nodeMessageVisibility;
                    break;
                case "WarningsBorder":
                    WarningsBorder.HorizontalAlignment = horizontalAlignment;
                    ViewModel.NodeWarningsVisibilityState = nodeMessageVisibility;
                    break;
                case "ErrorsBorder":
                    ErrorsBorder.HorizontalAlignment = horizontalAlignment;
                    ViewModel.NodeErrorsVisibilityState = nodeMessageVisibility;
                    break;
                default:
                    return;
            }

            e.Handled = true;
        }

        #endregion

        private void Border_OnMouseEnter(object sender, MouseEventArgs e)
        {
            if (!(sender is Border border)) return;

            InfoBubbleViewModel.NodeMessageVisibility collapsed = InfoBubbleViewModel.NodeMessageVisibility.CollapseMessages;
            InfoBubbleViewModel.NodeMessageVisibility icon = InfoBubbleViewModel.NodeMessageVisibility.Icon;
            HorizontalAlignment stretch = HorizontalAlignment.Stretch;
            HorizontalAlignment left = HorizontalAlignment.Left;

            bool isIcon;
            
            switch (border.Name)
            {
                case "InfoBorder":
                    isIcon = ViewModel.NodeInfoVisibilityState == InfoBubbleViewModel.NodeMessageVisibility.Icon;
                    ViewModel.NodeInfoVisibilityState = isIcon ? collapsed : icon;
                    break;
                case "WarningsBorder":
                    isIcon = ViewModel.NodeWarningsVisibilityState == InfoBubbleViewModel.NodeMessageVisibility.Icon;
                    ViewModel.NodeWarningsVisibilityState = isIcon ? collapsed : icon;
                    break;
                case "ErrorsBorder":
                    isIcon = ViewModel.NodeErrorsVisibilityState == InfoBubbleViewModel.NodeMessageVisibility.Icon;
                    ViewModel.NodeErrorsVisibilityState = isIcon ? collapsed : icon;
                    break;
                default:
                    return;
            }

            border.HorizontalAlignment = isIcon ? stretch : left;
            ViewModel.NodeWarningsShowLessMessageVisible = false;
            e.Handled = true;
        }
    }
}
