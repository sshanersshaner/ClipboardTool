using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace ClipboardTool
{
    public class ButtonColorScheme
    {
        public string Name;
        public string Normal;
        public string Hover;
        public string Press;
        public string Foreground;

        public ButtonColorScheme(string name, string normal, string hover, string press, string foreground)
        {
            Name = name;
            Normal = normal;
            Hover = hover;
            Press = press;
            Foreground = foreground;
        }
    }

    public enum ToolboxActionType { Process, RunAs, WinKey, Countdown, Pomodoro }

    public class ToolboxItem
    {
        public string Label;
        public string ActionParam;
        public ToolboxActionType ActionType;
        public int Count;
    }

    public class MainWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, IntPtr dwExtraInfo);

        private const byte VK_LWIN = 0x5B;
        private const byte VK_SHIFT = 0x10;
        private const byte VK_CONTROL = 0x11;
        private const byte VK_C = 0x43;
        private const byte VK_X = 0x58;
        private const byte VK_V = 0x56;
        private const byte VK_S = 0x53;
        private const uint KEYEVENTF_KEYUP = 0x02;

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr lpdwProcessId);
        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        private System.Windows.Threading.DispatcherTimer _screenshotTimer;
        private int _screenshotPollCount;
        private const int ScreenshotTimeoutCount = 150;
        private IntPtr _previousHwnd = IntPtr.Zero;

        private static Dictionary<string, string> TextColorOptions = new Dictionary<string, string>
        {
            {"\u9ed1", "#2B2B3A"},
            {"\u6df1\u7070", "#4A4A5C"},
            {"\u7070", "#6B6B7A"},
            {"\u767d", "#FFFFFF"},
            {"\u7c89", "#FF2D7A"},
            {"\u7ea2", "#FF3B3B"},
            {"\u6a59", "#FF7A00"},
            {"\u9ec4", "#FFD000"},
            {"\u7eff", "#00C853"},
            {"\u9752", "#00DCEC"},
            {"\u84dd", "#2B7BFF"},
            {"\u7d2b", "#A830E8"},
        };

        private static List<string> TextColorRow1 = new List<string>
            { "\u9ed1", "\u6df1\u7070", "\u7070", "\u767d", "\u7c89", "\u7ea2", "\u6a59", "\u9ec4", "\u7eff", "\u9752", "\u84dd", "\u7d2b" };

        private static Dictionary<string, ButtonColorScheme> ButtonSchemes = new Dictionary<string, ButtonColorScheme>
        {
            {"\u7c89", new ButtonColorScheme("\u7c89", "#FF2D7A", "#FF5C9E", "#CC1A5E", "#FFFFFF")},
            {"\u7ea2", new ButtonColorScheme("\u7ea2", "#FF3B3B", "#FF6B6B", "#D52020", "#FFFFFF")},
            {"\u6a59", new ButtonColorScheme("\u6a59", "#FF7A00", "#FF9D3D", "#E65C00", "#FFFFFF")},
            {"\u9ec4", new ButtonColorScheme("\u9ec4", "#FFD000", "#FFE03D", "#E6B800", "#3D2E00")},
            {"\u7eff", new ButtonColorScheme("\u7eff", "#00C853", "#33D677", "#00A040", "#FFFFFF")},
            {"\u9752", new ButtonColorScheme("\u9752", "#00DCEC", "#3DE8F5", "#00B0C0", "#FFFFFF")},
            {"\u84dd", new ButtonColorScheme("\u84dd", "#2B7BFF", "#5B9CFF", "#1455D6", "#FFFFFF")},
            {"\u7d2b", new ButtonColorScheme("\u7d2b", "#A830E8", "#C25EFF", "#8A1BC2", "#FFFFFF")},
            {"\u9ed1", new ButtonColorScheme("\u9ed1", "#2B2B3A", "#454556", "#1A1A28", "#FFFFFF")},
            {"\u767d", new ButtonColorScheme("\u767d", "#ECECF0", "#F5F5F8", "#D0D0DA", "#424242")},
        };

        private static Dictionary<string, string> BackgroundColors = new Dictionary<string, string>
        {
            {"\u9ed1", "#1A1A2E"},
            {"\u6df1\u7070", "#4A4A5C"},
            {"\u767d", "#F8F8FC"},
            {"\u7070", "#C8C8D0"},
            {"\u7ea2", "#FFE0E6"},
            {"\u6df1\u7ea2", "#FF7A8A"},
            {"\u6a59", "#FFE8D0"},
            {"\u6df1\u6a59", "#FFB050"},
            {"\u9ec4", "#FFFBE0"},
            {"\u6df1\u9ec4", "#FFE03D"},
            {"\u7eff", "#DCF5E8"},
            {"\u6df1\u7eff", "#66D699"},
            {"\u9752", "#D0F5F8"},
            {"\u6df1\u9752", "#33DCEE"},
            {"\u84dd", "#D8EAFF"},
            {"\u6df1\u84dd", "#5BA0FF"},
            {"\u7d2b", "#F0DCF5"},
            {"\u6df1\u7d2b", "#B066D6"},
        };

        private static List<string> BgRow1 = new List<string>
            { "\u7ea2", "\u6a59", "\u9ec4", "\u7eff", "\u9752", "\u84dd", "\u7d2b", "\u7070", "\u767d" };
        private static List<string> BgRow2 = new List<string>
            { "\u6df1\u7ea2", "\u6df1\u6a59", "\u6df1\u9ec4", "\u6df1\u7eff", "\u6df1\u9752", "\u6df1\u84dd", "\u6df1\u7d2b", "\u9ed1", "\u6df1\u7070" };

        private static string[] PageNames = { "\u4e3b\u9875\u9762", "\u526a\u8d34\u677f", "\u5e38\u7528\u4fe1\u606f", "\u5de5\u5177\u7bb1" };

        private UIElement _settingsButton;
        private bool _isSettingsOpen = false;
        private Grid _mainGrid;
        private Border _mainBorder;
        private StackPanel _buttonRow;
        private UIElement _settingsPanel;
        private string _currentButtonScheme = "\u767d";
        private string _currentBackgroundColor = "\u767d";
        private string _currentTextColor = "\u6df1\u7d2b";
        private bool _useGradient = false;
        private string _currentGradientScheme = "";
        private bool _isTopmost = true;
        private bool _edgeSnapEnabled = false;
        private bool _isAnimating = false;
        private bool _isTransparencyEnabled = false;
        private double _transparencyValue = 0.85;
        private bool _autoHideEnabled = false;
        private bool _extractTextEnabled = false;
        private bool _extractImageEnabled = false;
        private bool _saveScreenshotToDesktop = false;
        private bool _isAutoHidden = false;
        private System.Windows.Threading.DispatcherTimer _autoHideTimer;
        private double _restoreLeft;
        private double _restoreTop;
        private string _autoHiddenEdge = "";

        // Page state
        private int _currentPage = 0;
        private TextBlock _pageTitle;
        private Grid _pageContent;
        private Border _page0; // Main
        private Border _page1; // Clipboard
        private Border _page2; // Common info

        // Clipboard history
        private List<string> _clipboardHistory = new List<string>();
        private string _lastClipboardText = "";
        private System.Windows.Threading.DispatcherTimer _clipboardWatcher;
        private StackPanel _clipListPanel;
        private ScrollViewer _clipScrollViewer;
        private HashSet<int> _clipAnimatedIndices = new HashSet<int>();
        private bool _clipSearchMode = false;
        private StackPanel _clipJumpRow;
        private StackPanel _clipSearchRow;
        private TextBox _clipJumpToInput;
        private TextBox _clipSearchInput;
        private TextBlock _clipCountLabel;
        private List<string> _filteredClipItems;
        private int _maxHistory = 255;

        // Common info
        private List<string> _commonItems = new List<string>();
        private StackPanel _commonListPanel;
        private ScrollViewer _commonScrollViewer;
        private HashSet<int> _commonAnimatedIndices = new HashSet<int>();
        private Border _page3;
        private List<ToolboxItem> _toolboxItems = new List<ToolboxItem>();
        private StackPanel _toolboxListPanel;
        private ScrollViewer _toolboxScrollViewer;
        private HashSet<int> _toolboxAnimatedIndices = new HashSet<int>();
        private System.Windows.Threading.DispatcherTimer _countdownTimer;
        private System.Windows.Threading.DispatcherTimer _pomodoroTimer;
        private ToolboxItem _activeCountdownItem;
        private ToolboxItem _activePomodoroItem;
        private TextBlock _countdownLabel;
        private TextBlock _pomodoroLabel;
        private int _countdownRemaining;
        private int _pomodoroRemaining;
        private int _pomodoroPhase;
        private bool _isDragging = false;
        private Point _dragOrigin;
        private Tesseract.TesseractEngine _ocrEngine;
        private readonly object _ocrLock = new object();
        private TextBox _commonJumpToInput;
        private TextBlock _commonCountLabel;

        public MainWindow()
        {
            LoadSettings();
            InitializeWindow();
            BuildUI();
            ApplyBackgroundColor();
            ApplyTopmost();
            SwitchPage(0);
            // Startup fade-in
            SourceInitialized += (s2, e2) =>
            {
                Opacity = 0;
                var targetOpacity = _isTransparencyEnabled ? _transparencyValue : 1.0;
                var anim = new DoubleAnimation(targetOpacity, TimeSpan.FromSeconds(0.35))
                {
                    EasingFunction = new CircleEase { EasingMode = EasingMode.EaseOut }
                };
                anim.Completed += (s3, e3) =>
                {
                    BeginAnimation(Window.OpacityProperty, null);
                    Opacity = targetOpacity;
                };
                BeginAnimation(Window.OpacityProperty, anim);
            };
        }

        private void InitializeWindow()
        {
            Title = "\u526a\u8d34\u5de5\u5177";
            Width = 380;
            Height = 120;
            MinWidth = Width;
            MinHeight = Height;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = null;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = 0;
            Top = 0;
            ShowInTaskbar = true;

            _previousHwnd = GetForegroundWindow();
            MouseEnter += (s, e) =>
            {
                var hwnd = GetForegroundWindow();
                var myHwnd = new WindowInteropHelper(this).Handle;
                if (hwnd != myHwnd && hwnd != IntPtr.Zero)
                    _previousHwnd = hwnd;
            };

            MouseLeftButtonDown += (s, e) =>
            {
                if (e.OriginalSource is Button == false)
                {
                    _dragOrigin = e.GetPosition(this);
                    _isDragging = false;
                }
            };

            MouseMove += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed && !_isDragging)
                {
                    var pos = e.GetPosition(this);
                    if (Math.Abs(pos.X - _dragOrigin.X) > SystemParameters.MinimumHorizontalDragDistance || Math.Abs(pos.Y - _dragOrigin.Y) > SystemParameters.MinimumVerticalDragDistance)
                    {
                        _isDragging = true;
                        DragMove();
                        if (_edgeSnapEnabled) SnapToEdge();
                        _isDragging = false;
                    }
                }
            };

            // Clipboard watcher
            _clipboardWatcher = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _clipboardWatcher.Tick += (s, e2) =>
            {
                try
                {
                    if (Clipboard.ContainsText())
                    {
                        var txt = Clipboard.GetText();
                        if (!string.IsNullOrEmpty(txt) && txt != _lastClipboardText)
                        {
                            _lastClipboardText = txt;
                            _clipboardHistory.Remove(txt);
                            _clipboardHistory.Insert(0, txt);
                            if (_clipboardHistory.Count > _maxHistory) _clipboardHistory.RemoveRange(_maxHistory, _clipboardHistory.Count - _maxHistory);
                            if (_currentPage == 1) RefreshClipboardList();
                        }
                    }
                }
                catch { }
            };
            _clipboardWatcher.Start();

            Closing += (s, e) => { SaveClipboardHistory(); if (_ocrEngine != null) { try { _ocrEngine.Dispose(); _ocrEngine = null; } catch { } } };
        }

        private void BuildUI()
        {
            _mainBorder = new Border
            {
                CornerRadius = new CornerRadius(0),
                Background = new SolidColorBrush(Color.FromArgb(255, 250, 250, 254)),
                Effect = new DropShadowEffect { BlurRadius = 24, ShadowDepth = 2, Color = Colors.Black, Opacity = 0.22 },
                Padding = new Thickness(8, 6, 8, 6),
            };

            _mainGrid = new Grid();
            _mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Title bar
            _mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Page content
            _mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Settings

            // Row 0: Title bar
            _mainGrid.Children.Add(BuildTitleBar());

            // Row 1: Page content
            _pageContent = new Grid { ClipToBounds = true };
            _page0 = new Border();
            _page1 = BuildClipboardPage();
            _page2 = BuildCommonInfoPage();
            _page3 = BuildToolboxPage();
            _pageContent.Children.Add(_page0);
            _pageContent.Children.Add(_page1);
            _pageContent.Children.Add(_page2);
            _pageContent.Children.Add(_page3);
            Grid.SetRow(_pageContent, 1);
            _mainGrid.Children.Add(_pageContent);

            // Row 2: Settings
            _settingsPanel = BuildSettingsPanel();
            _settingsPanel.Visibility = Visibility.Collapsed;
            Grid.SetRow(_settingsPanel, 2);
            _mainGrid.Children.Add(_settingsPanel);

            SetMainPageContent();

            _mainBorder.Child = _mainGrid;
            Content = _mainBorder;
        }

        private UIElement BuildTitleBar()
        {
            var bar = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Left arrow
            var leftBtn = new Button
            {
Content = "\u2039",
FontSize = 16, FontWeight = FontWeights.Bold,
Foreground = new SolidColorBrush(Color.FromRgb(130, 130, 142)),
                Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand, Width = 24, Height = 24,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            leftBtn.Click += (s, e) => { SwitchPageAnimated((_currentPage + 3) % 4); };
            leftBtn.MouseEnter += (s, e) => { leftBtn.Foreground = Brushes.White; leftBtn.Background = new SolidColorBrush(Color.FromRgb(43, 123, 255)); };
            leftBtn.MouseLeave += (s, e) => { leftBtn.Foreground = new SolidColorBrush(Color.FromRgb(130, 130, 142)); leftBtn.Background = Brushes.Transparent; };
            Grid.SetColumn(leftBtn, 0);
            bar.Children.Add(leftBtn);

            // Page title
            _pageTitle = new TextBlock
            {
                Text = PageNames[0],
                FontSize = 14, FontWeight = FontWeights.SemiBold,
Foreground = new SolidColorBrush(Color.FromRgb(70, 70, 82)),
HorizontalAlignment = HorizontalAlignment.Center,
VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(_pageTitle, 0);
            bar.Children.Add(_pageTitle);

            // Right section: settings + close + right arrow
            var rightStack = new StackPanel { Orientation = Orientation.Horizontal };
            Grid.SetColumn(rightStack, 2);

            var rightBtn = new Button
            {
Content = "\u203A",
FontSize = 16, FontWeight = FontWeights.Bold,
Foreground = new SolidColorBrush(Color.FromRgb(130, 130, 142)),
                Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand, Width = 24, Height = 24,
            };
            rightBtn.Click += (s, e) => { SwitchPageAnimated((_currentPage + 1) % 4); };
            rightBtn.MouseEnter += (s, e) => { rightBtn.Foreground = Brushes.White; rightBtn.Background = new SolidColorBrush(Color.FromRgb(43, 123, 255)); };
            rightBtn.MouseLeave += (s, e) => { rightBtn.Foreground = new SolidColorBrush(Color.FromRgb(130, 130, 142)); rightBtn.Background = Brushes.Transparent; };
            rightStack.Children.Add(_settingsButton = BuildSettingsButton());
            rightStack.Children.Add(BuildCloseButton());
            rightStack.Children.Add(rightBtn);

            bar.Children.Add(rightStack);
            Grid.SetRow(bar, 0);
            return bar;
        }

        private void SwitchPage(int page)
        {
            _currentPage = page;
            _pageTitle.Text = PageNames[page];

            _page0.Visibility = page == 0 ? Visibility.Visible : Visibility.Collapsed;
            _page1.Visibility = page == 1 ? Visibility.Visible : Visibility.Collapsed;
            _page2.Visibility = page == 2 ? Visibility.Visible : Visibility.Collapsed;
            _page3.Visibility = page == 3 ? Visibility.Visible : Visibility.Collapsed;

            // Settings only on main page
            _settingsButton.Visibility = page == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (page != 0 && _isSettingsOpen)
            {
                _isSettingsOpen = false;
                _settingsPanel.Visibility = Visibility.Collapsed;
            }

            SizeToContent = SizeToContent.Manual;

            if (page == 0) { MinHeight = 120; Height = 120; }
            else { MinHeight = 440; Height = 440; }

            UpdateLayout();

            if (page == 1) RefreshClipboardList();
            if (page == 2) RefreshCommonList();
            if (page == 3) RefreshToolboxList();

            _pendingDeleteIndex = -1;
        }

        private void SwitchPageAnimated(int page)
        {
            if (_isAnimating) return;
            if (page == _currentPage) return;

            _isAnimating = true;
            int oldPage = _currentPage;
            bool goingRight = ((page - oldPage + 4) % 4) == 1;

            _currentPage = page;
            _pageTitle.Text = PageNames[page];

            // Settings only on main page
            _settingsButton.Visibility = page == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (page != 0 && _isSettingsOpen)
            {
                _isSettingsOpen = false;
                _settingsPanel.Visibility = Visibility.Collapsed;
            }

            SizeToContent = SizeToContent.Manual;

            // Hide the incoming page during height animation; old page stays visible
            GetPageBorder(page).Visibility = Visibility.Collapsed;

            _pendingDeleteIndex = -1;

            double oldHeight = oldPage == 0 ? 120 : 440;
            double newHeight = page == 0 ? 120 : 440;

            // Allow shrinking to the smaller of the two
            MinHeight = Math.Min(oldHeight, newHeight);

            if (Math.Abs(oldHeight - newHeight) > 1)
            {
                var heightAnim = new DoubleAnimation(newHeight, TimeSpan.FromSeconds(0.25))
                {
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };
                heightAnim.Completed += (s, e) =>
                {
                    BeginAnimation(HeightProperty, null);
                    MinHeight = newHeight;
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (page == 1) RefreshClipboardList();
                        if (page == 2) RefreshCommonList();
                        if (page == 3) RefreshToolboxList();
                    }), System.Windows.Threading.DispatcherPriority.Background);
                    DoSlideAnimation(oldPage, page, goingRight);
                };
                BeginAnimation(HeightProperty, heightAnim);
            }
            else
            {
                BeginAnimation(HeightProperty, null);
                MinHeight = newHeight;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (page == 1) RefreshClipboardList();
                    if (page == 2) RefreshCommonList();
                    if (page == 3) RefreshToolboxList();
                }), System.Windows.Threading.DispatcherPriority.Background);
                DoSlideAnimation(oldPage, page, goingRight);
            }
        }

        private Border GetPageBorder(int index)
        {
            if (index == 0) return _page0;
            if (index == 1) return _page1;
            if (index == 2) return _page2;
            return _page3;
        }

        private void DoSlideAnimation(int oldPage, int newPage, bool goingRight)
        {
            var oldBorder = GetPageBorder(oldPage);
            var newBorder = GetPageBorder(newPage);

            var oldTransform = new TranslateTransform(0, 0);
            var newTransform = new TranslateTransform(goingRight ? 220 : -220, 0);
            oldBorder.RenderTransform = oldTransform;
            newBorder.RenderTransform = newTransform;

            oldBorder.Visibility = Visibility.Visible;
            newBorder.Visibility = Visibility.Visible;
            oldBorder.Opacity = 1.0;
            newBorder.Opacity = 0.0;

            var duration = TimeSpan.FromSeconds(0.25);
            var ease = new CircleEase { EasingMode = EasingMode.EaseOut };

            var oldSlideX = new DoubleAnimation(goingRight ? -220 : 220, duration) { EasingFunction = ease };
            var oldFade = new DoubleAnimation(0.0, duration) { EasingFunction = ease };
            var newSlideX = new DoubleAnimation(0, duration) { EasingFunction = ease };
            var newFade = new DoubleAnimation(1.0, TimeSpan.FromSeconds(0.37)) { EasingFunction = ease };

            newFade.Completed += (s, e) =>
            {
                // Commit final states to prevent snap-back
                oldBorder.BeginAnimation(OpacityProperty, null);
                oldBorder.RenderTransform = new TranslateTransform(0, 0);
                oldBorder.Opacity = 1.0;
                oldBorder.Visibility = Visibility.Collapsed;

                newBorder.BeginAnimation(OpacityProperty, null);
                newBorder.RenderTransform = new TranslateTransform(0, 0);
                newBorder.Opacity = 1.0;

                _isAnimating = false;
            };

            oldTransform.BeginAnimation(TranslateTransform.XProperty, oldSlideX);
            oldBorder.BeginAnimation(OpacityProperty, oldFade);
            newTransform.BeginAnimation(TranslateTransform.XProperty, newSlideX);
            newBorder.BeginAnimation(OpacityProperty, newFade);
        }

        // Called from BuildUI after _page0 is created, to set its content
        private void SetMainPageContent()
        {
            _buttonRow = BuildButtonRow();
            _page0.Child = _buttonRow;
        }

        // ==================== Clipboard Page ====================
        private int _pendingDeleteIndex = -1;
        private Button _pendingDeleteBtn = null;

        private Border BuildClipboardPage()
        {
            var page = new Border();
            var layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            _clipScrollViewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            _clipListPanel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 4, 0, 0) };
            _clipScrollViewer.Content = _clipListPanel;
            _clipScrollViewer.ScrollChanged += OnClipScrollChanged;
            Grid.SetRow(_clipScrollViewer, 0);
            layout.Children.Add(_clipScrollViewer);

            var bottomBar = BuildClipBottomBar();
            Grid.SetRow(bottomBar, 1);
            layout.Children.Add(bottomBar);

            page.Child = layout;
            _page1 = page;
            return page;
        }

        private UIElement BuildClipBottomBar()
        {
            var bar = new Grid { Margin = new Thickness(0, 6, 0, 0), Height = 28 };
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _clipJumpRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
            _clipJumpToInput = new TextBox { Width = 40, Height = 22, FontSize = 12, VerticalContentAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center };
            _clipCountLabel = new TextBlock { Text = "", FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0), Foreground = new SolidColorBrush(Color.FromRgb(110, 110, 122)) };
            _clipJumpRow.Children.Add(new TextBlock { Text = "\u8df3\u8f6c\u5230\u7b2c ", FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush(Color.FromRgb(110, 110, 122)) });
            _clipJumpRow.Children.Add(_clipJumpToInput);
            _clipJumpRow.Children.Add(new TextBlock { Text = " \u6761", FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush(Color.FromRgb(110, 110, 122)) });
            _clipJumpRow.Children.Add(_clipCountLabel);
            _clipJumpToInput.KeyDown += (s, e) => { if (e.Key == Key.Enter) DoClipJump(); };
            Grid.SetColumn(_clipJumpRow, 0);
            bar.Children.Add(_clipJumpRow);

            _clipSearchRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed };
            _clipSearchInput = new TextBox { Width = 260, Height = 22, FontSize = 12, VerticalContentAlignment = VerticalAlignment.Center };
            _clipSearchInput.TextChanged += (s4, e4) => DoClipSearch();
            _clipSearchRow.Children.Add(new TextBlock { Text = "\u641c\u7d22: ", FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush(Color.FromRgb(110, 110, 122)) });
            _clipSearchRow.Children.Add(_clipSearchInput);
            Grid.SetColumn(_clipSearchRow, 0);
            bar.Children.Add(_clipSearchRow);

            var searchBtn = new Button { Content = "\u641c\u7d22", FontSize = 11, Width = 40, Height = 22, Background = Brushes.Transparent, Foreground = new SolidColorBrush(Color.FromRgb(110, 110, 122)), BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
            searchBtn.Click += (s5, e5) => ToggleClipSearch(searchBtn);
            Grid.SetColumn(searchBtn, 1);
            bar.Children.Add(searchBtn);

            return bar;
        }

        private void ToggleClipSearch(Button searchBtn)
        {
            _clipSearchMode = !_clipSearchMode;
            if (_clipSearchMode)
            {
                _clipJumpRow.Visibility = Visibility.Collapsed;
                _clipSearchRow.Visibility = Visibility.Visible;
                _clipSearchInput.Text = "";
                _clipSearchInput.Focus();
                searchBtn.Background = new SolidColorBrush(Color.FromRgb(0x00, 0xC8, 0x53));
                searchBtn.Foreground = Brushes.White;
            }
            else
            {
                _clipJumpRow.Visibility = Visibility.Visible;
                _clipSearchRow.Visibility = Visibility.Collapsed;
                searchBtn.Background = Brushes.Transparent;
                searchBtn.Foreground = new SolidColorBrush(Color.FromRgb(110, 110, 122));
                _filteredClipItems = null;
                RefreshClipboardList();
            }
        }

        private void DoClipSearch()
        {
            var query = _clipSearchInput.Text.Trim();
            if (string.IsNullOrEmpty(query))
            {
                _filteredClipItems = null;
                RefreshClipboardListFromSearch();
                return;
            }
            _filteredClipItems = new List<string>();
            foreach (var item in _clipboardHistory)
            {
                if (item.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    _filteredClipItems.Add(item);
            }
            RefreshClipboardListFromSearch();
        }

        private void DoClipJump()
        {
            int num;
            var sourceList = (_clipSearchMode && _filteredClipItems != null) ? _filteredClipItems : _clipboardHistory;
            if (int.TryParse(_clipJumpToInput.Text.Trim(), out num) && num >= 1 && num <= sourceList.Count)
            {
                int idx = num - 1;
                var targetCard = _clipListPanel.Children[idx] as Border;
                if (targetCard != null)
                {
                    var origBg = targetCard.Background;
                    var accent = (SolidColorBrush)new BrushConverter().ConvertFromString(ButtonSchemes[_currentButtonScheme].Normal);
                    var hl = new SolidColorBrush(Color.FromArgb(80, accent.Color.R, accent.Color.G, accent.Color.B));
                    targetCard.Background = hl;
                    var t1 = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(0.8) };
                    t1.Tick += (s6, e6) => { targetCard.Background = origBg; t1.Stop(); };
                    t1.Start();
                    targetCard.BringIntoView();
                }
            }
            else
            {
                var origBg2 = _clipJumpToInput.Background;
                _clipJumpToInput.Background = new SolidColorBrush(Colors.Red);
                var t2 = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
                t2.Tick += (s7, e7) =>
                {
                    var a2 = new ColorAnimation { To = ((SolidColorBrush)origBg2).Color, Duration = TimeSpan.FromSeconds(0.15) };
                    _clipJumpToInput.Background.BeginAnimation(SolidColorBrush.ColorProperty, a2);
                    t2.Stop();
                };
                t2.Start();
                var t3 = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(0.2) };
                t3.Tick += (s8, e8) => {
                    var a3 = new ColorAnimation { To = Colors.Red, Duration = TimeSpan.FromSeconds(0.2) };
                    _clipJumpToInput.Background.BeginAnimation(SolidColorBrush.ColorProperty, a3);
                    t3.Stop();
                };
                t3.Start();
            }
        }

        private void OnClipScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            AnimateVisibleCards(_clipListPanel, _clipScrollViewer, _clipAnimatedIndices);
        }

        private void RefreshClipboardList()
        {
            _clipListPanel.Children.Clear();
            _pendingDeleteIndex = -1;
            _pendingDeleteBtn = null;
            _clipAnimatedIndices.Clear();

            var items = (_clipSearchMode && _filteredClipItems != null) ? _filteredClipItems : _clipboardHistory;
            _clipCountLabel.Text = "/ \u5171 " + items.Count + " \u6761";

            if (items.Count == 0)
            {
                var empty = new TextBlock
                {
                    Text = _clipSearchMode ? "\u65e0" : "\u7a7a",
                    FontSize = 18,
                    Foreground = new SolidColorBrush(Color.FromArgb(100, 150, 150, 160)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 60, 0, 0),
                };
                _clipListPanel.Children.Add(empty);
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                int idx = i;
                var card = BuildClipboardCard(items[i], idx);
                card.Opacity = 0;
                _clipListPanel.Children.Add(card);
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                AnimateVisibleCards(_clipListPanel, _clipScrollViewer, _clipAnimatedIndices);
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void RefreshClipboardListFromSearch()
        {
            _clipListPanel.Children.Clear();
            _pendingDeleteIndex = -1;
            _pendingDeleteBtn = null;
            _clipAnimatedIndices.Clear();

            var items = _filteredClipItems ?? _clipboardHistory;
            _clipCountLabel.Text = "/ \u5171 " + items.Count + " \u6761";

            if (items.Count == 0)
            {
                var empty = new TextBlock
                {
                    Text = "\u65e0",
                    FontSize = 18,
                    Foreground = new SolidColorBrush(Color.FromArgb(100, 150, 150, 160)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 60, 0, 0),
                };
                _clipListPanel.Children.Add(empty);
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                int idx = i;
                var card = BuildClipboardCard(items[i], idx);
                card.Opacity = 0;
                _clipListPanel.Children.Add(card);
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                AnimateVisibleCards(_clipListPanel, _clipScrollViewer, _clipAnimatedIndices);
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private Border BuildClipboardCard(string text, int index)
        {
            var cardBg = (SolidColorBrush)new BrushConverter().ConvertFromString(ButtonSchemes[_currentButtonScheme].Normal);
            var cardTint = new SolidColorBrush(Color.FromArgb(20, cardBg.Color.R, cardBg.Color.G, cardBg.Color.B));

            var card = new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = cardTint,
                Margin = new Thickness(0, 0, 0, 4),
                Padding = new Thickness(8, 6, 4, 6),
                RenderTransformOrigin = new Point(0.5, 0.5),
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var contentBlock = new TextBlock
            {
                Text = text,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxHeight = 48,
                Cursor = Cursors.Hand,
                Foreground = new SolidColorBrush(Color.FromRgb(55, 55, 66)),
            };
            contentBlock.MouseDown += (s9, e9) =>
            {
                if (e9.LeftButton == MouseButtonState.Pressed)
                {
                    _pendingDeleteIndex = -1;
                    _pendingDeleteBtn = null;
                    if (text != "[截图]") { try { Clipboard.SetText(text); } catch { } }
                    AnimatePasteScale(card);
                    SimulateCtrlKey(VK_V);
                }
            };
            Grid.SetColumn(contentBlock, 0);
            grid.Children.Add(contentBlock);

            var delBtn = new Button
            {
                Content = "\u2212",
                FontSize = 16, FontWeight = FontWeights.Bold,
                Width = 26, Height = 26,
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 172)),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
            };

            int capturedIndex = index;
            var delNormalFg = new SolidColorBrush(Color.FromRgb(160, 160, 172));
            delBtn.MouseEnter += (s10, e10) => {
                if (_pendingDeleteIndex != capturedIndex) {
                    delBtn.Background = new SolidColorBrush(Color.FromArgb(60, 0xE5, 0x3E, 0x3E));
                    delBtn.Foreground = new SolidColorBrush(Color.FromRgb(0xE5, 0x3E, 0x3E));
                }
            };
            delBtn.MouseLeave += (s11, e11) => {
                if (_pendingDeleteIndex != capturedIndex) {
                    delBtn.Background = Brushes.Transparent;
                    delBtn.Foreground = delNormalFg;
                }
            };
            delBtn.Click += (s12, e12) =>
            {
                if (_pendingDeleteIndex == capturedIndex)
                {
                    var srcList = (_clipSearchMode && _filteredClipItems != null) ? _filteredClipItems : _clipboardHistory;
                    if (capturedIndex < srcList.Count)
                    {
                        var itemToRemove = srcList[capturedIndex];
                        _clipboardHistory.Remove(itemToRemove);
                        if (_filteredClipItems != null) _filteredClipItems.Remove(itemToRemove);
                    }
                    _pendingDeleteIndex = -1;
                    if (_clipSearchMode) RefreshClipboardListFromSearch();
                    else RefreshClipboardList();
                }
                else
                {
                    if (_pendingDeleteBtn != null) {
                        _pendingDeleteBtn.Background = Brushes.Transparent;
                        _pendingDeleteBtn.Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 172));
                    }
                    _pendingDeleteIndex = capturedIndex;
                    _pendingDeleteBtn = delBtn;
                    delBtn.Foreground = Brushes.White;
                    var redBrush = new SolidColorBrush(Color.FromRgb(0xE5, 0x3E, 0x3E));
                    delBtn.Background = redBrush;
                }
            };
            Grid.SetColumn(delBtn, 1);
            grid.Children.Add(delBtn);

            card.Child = grid;
            return card;
        }

        // ==================== Common Info Page ====================
        private Border BuildCommonInfoPage()
        {
            var page = new Border();
            var layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            _commonScrollViewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            _commonListPanel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 4, 0, 0) };
            _commonScrollViewer.Content = _commonListPanel;
            _commonScrollViewer.ScrollChanged += OnCommonScrollChanged;
            Grid.SetRow(_commonScrollViewer, 0);
            layout.Children.Add(_commonScrollViewer);

            var bottomBar = BuildCommonBottomBar();
            Grid.SetRow(bottomBar, 1);
            layout.Children.Add(bottomBar);

            page.Child = layout;
            _page2 = page;
            return page;
        }

        private UIElement BuildCommonBottomBar()
        {
            var bar = new Grid { Margin = new Thickness(0, 6, 0, 0), Height = 28 };
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var addBtn = new Button { Content = "\uff0b", FontSize = 14, Width = 24, Height = 24, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Foreground = new SolidColorBrush(Color.FromRgb(110, 110, 122)) };
            addBtn.Click += (s13, e13) => ShowAddCommonDialog();
            Grid.SetColumn(addBtn, 0);
            bar.Children.Add(addBtn);

            var jumpRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
            _commonJumpToInput = new TextBox { Width = 40, Height = 22, FontSize = 12, VerticalContentAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center };
            _commonCountLabel = new TextBlock { Text = "", FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0), Foreground = new SolidColorBrush(Color.FromRgb(110, 110, 122)) };
            jumpRow.Children.Add(new TextBlock { Text = "\u8df3\u8f6c\u5230\u7b2c ", FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush(Color.FromRgb(110, 110, 122)) });
            jumpRow.Children.Add(_commonJumpToInput);
            jumpRow.Children.Add(new TextBlock { Text = " \u6761", FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush(Color.FromRgb(110, 110, 122)) });
            jumpRow.Children.Add(_commonCountLabel);
            _commonJumpToInput.KeyDown += (s14, e14) => { if (e14.Key == Key.Enter) DoCommonJump(); };
            Grid.SetColumn(jumpRow, 1);
            bar.Children.Add(jumpRow);

            return bar;
        }

        private void DoCommonJump()
        {
            int num;
            if (int.TryParse(_commonJumpToInput.Text.Trim(), out num) && num >= 1 && num <= _commonItems.Count)
            {
                int idx = num - 1;
                var targetCard = _commonListPanel.Children[idx] as Border;
                if (targetCard != null)
                {
                    var origBg = targetCard.Background;
                    var accent = (SolidColorBrush)new BrushConverter().ConvertFromString(ButtonSchemes[_currentButtonScheme].Normal);
                    var hl = new SolidColorBrush(Color.FromArgb(80, accent.Color.R, accent.Color.G, accent.Color.B));
                    targetCard.Background = hl;
                    var t4 = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(0.8) };
                    t4.Tick += (s15, e15) => { targetCard.Background = origBg; t4.Stop(); };
                    t4.Start();
                    targetCard.BringIntoView();
                }
            }
            else
            {
                var origBg3 = _commonJumpToInput.Background;
                _commonJumpToInput.Background = new SolidColorBrush(Colors.Red);
                var t5 = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
                t5.Tick += (s16, e16) =>
                {
                    var a4 = new ColorAnimation { To = ((SolidColorBrush)origBg3).Color, Duration = TimeSpan.FromSeconds(0.15) };
                    _commonJumpToInput.Background.BeginAnimation(SolidColorBrush.ColorProperty, a4);
                    t5.Stop();
                };
                t5.Start();
                var t6 = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(0.2) };
                t6.Tick += (s17, e17) => {
                    var a5 = new ColorAnimation { To = Colors.Red, Duration = TimeSpan.FromSeconds(0.2) };
                    _commonJumpToInput.Background.BeginAnimation(SolidColorBrush.ColorProperty, a5);
                    t6.Stop();
                };
                t6.Start();
            }
        }

        private void OnCommonScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            AnimateVisibleCards(_commonListPanel, _commonScrollViewer, _commonAnimatedIndices);
        }

        private void RefreshCommonList()
        {
            _commonListPanel.Children.Clear();
            _commonAnimatedIndices.Clear();
            _commonCountLabel.Text = "/ \u5171 " + _commonItems.Count + " \u6761";

            if (_commonItems.Count == 0)
            {
                var empty = new TextBlock
                {
                    Text = "\u7a7a",
                    FontSize = 18,
                    Foreground = new SolidColorBrush(Color.FromArgb(100, 150, 150, 160)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 60, 0, 0),
                };
                _commonListPanel.Children.Add(empty);
                return;
            }

            for (int i = 0; i < _commonItems.Count; i++)
            {
                int idx = i;
                var card = BuildCommonCard(_commonItems[i], idx);
                card.Opacity = 0;
                _commonListPanel.Children.Add(card);
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                AnimateVisibleCards(_commonListPanel, _commonScrollViewer, _commonAnimatedIndices);
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private Border BuildCommonCard(string text, int index)
        {
            var cardBg = (SolidColorBrush)new BrushConverter().ConvertFromString(ButtonSchemes[_currentButtonScheme].Normal);
            var cardTint = new SolidColorBrush(Color.FromArgb(20, cardBg.Color.R, cardBg.Color.G, cardBg.Color.B));
            var btnHoverBg = new SolidColorBrush(Color.FromArgb(40, cardBg.Color.R, cardBg.Color.G, cardBg.Color.B));
            var btnHoverFg = (SolidColorBrush)new BrushConverter().ConvertFromString(ButtonSchemes[_currentButtonScheme].Normal);

            var card = new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = cardTint,
                Margin = new Thickness(0, 0, 0, 4),
                Padding = new Thickness(8, 6, 0, 6),
                RenderTransformOrigin = new Point(0.5, 0.5),
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var contentBlock = new TextBlock
            {
                Text = text,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxHeight = 48,
                Height = 48,
                Cursor = Cursors.Hand,
                Foreground = new SolidColorBrush(Color.FromRgb(55, 55, 66)),
            };
            contentBlock.MouseDown += (s18, e18) =>
            {
                if (e18.LeftButton == MouseButtonState.Pressed)
                {
                    try { Clipboard.SetText(text); }
                    catch { }
                    AnimatePasteScale(card);
                    SimulateCtrlKey(VK_V);
                }
            };
            Grid.SetColumn(contentBlock, 0);
            grid.Children.Add(contentBlock);

            var btnStack = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0) };

            int capturedIndex = index;
            var defaultFg = new SolidColorBrush(Color.FromRgb(130, 130, 140));

            var upBtn = new Button { Content = "\u2191", FontSize = 11, Width = 22, Height = 20, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Foreground = defaultFg, Padding = new Thickness(0) };
            upBtn.MouseEnter += (s19, e19) => { upBtn.Background = btnHoverBg; upBtn.Foreground = btnHoverFg; };
            upBtn.MouseLeave += (s20, e20) => { upBtn.Background = Brushes.Transparent; upBtn.Foreground = defaultFg; };
            upBtn.Click += (s21, e21) => {
                if (capturedIndex > 0)
                {
                    var tmp = _commonItems[capturedIndex];
                    _commonItems[capturedIndex] = _commonItems[capturedIndex - 1];
                    _commonItems[capturedIndex - 1] = tmp;
                    SaveCommonItems();
                    RefreshCommonList();
                }
            };

            var delBtn = new Button { Content = "\u2212", FontSize = 14, Width = 22, Height = 20, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Foreground = new SolidColorBrush(Color.FromRgb(130, 130, 140)), Padding = new Thickness(0) };
            delBtn.MouseEnter += (s22, e22) => { delBtn.Background = new SolidColorBrush(Color.FromArgb(60, 0xE5, 0x3E, 0x3E)); delBtn.Foreground = new SolidColorBrush(Color.FromRgb(0xE5, 0x3E, 0x3E)); };
            delBtn.MouseLeave += (s23, e23) => { delBtn.Background = Brushes.Transparent; delBtn.Foreground = new SolidColorBrush(Color.FromRgb(130, 130, 140)); };
            delBtn.Click += (s24, e24) => {
                _commonItems.RemoveAt(capturedIndex);
                SaveCommonItems();
                RefreshCommonList();
            };

            var downBtn = new Button { Content = "\u2193", FontSize = 11, Width = 22, Height = 20, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Foreground = defaultFg, Padding = new Thickness(0) };
            downBtn.MouseEnter += (s25, e25) => { downBtn.Background = btnHoverBg; downBtn.Foreground = btnHoverFg; };
            downBtn.MouseLeave += (s26, e26) => { downBtn.Background = Brushes.Transparent; downBtn.Foreground = defaultFg; };
            downBtn.Click += (s27, e27) => {
                if (capturedIndex < _commonItems.Count - 1)
                {
                    var tmp = _commonItems[capturedIndex];
                    _commonItems[capturedIndex] = _commonItems[capturedIndex + 1];
                    _commonItems[capturedIndex + 1] = tmp;
                    SaveCommonItems();
                    RefreshCommonList();
                }
            };

            btnStack.Children.Add(upBtn);
            btnStack.Children.Add(delBtn);
            btnStack.Children.Add(downBtn);
            Grid.SetColumn(btnStack, 1);
            grid.Children.Add(btnStack);

            card.Child = grid;
            return card;
        }

        private void ShowAddCommonDialog()
        {
            var scheme = ButtonSchemes[_currentButtonScheme];
            var accent = (SolidColorBrush)new BrushConverter().ConvertFromString(scheme.Normal);

            var dialog = new Window
            {
                Title = "\u6dfb\u52a0\u5e38\u7528\u4fe1\u606f",
                Width = 340, Height = 240,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                Topmost = true,
            };

            var outerBorder = new Border
            {
                CornerRadius = new CornerRadius(0),
                Background = new SolidColorBrush(Color.FromArgb(250, 245, 245, 250)),
                Effect = new DropShadowEffect { BlurRadius = 16, ShadowDepth = 2, Color = Colors.Black, Opacity = 0.20 },
            };

            var g2 = new Grid { Margin = new Thickness(14) };
            g2.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g2.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            g2.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var titleBar2 = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            titleBar2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleBar2.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var title2 = new TextBlock { Text = "\u6dfb\u52a0\u5e38\u7528\u4fe1\u606f", FontSize = 14, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center };
            Grid.SetColumn(title2, 0);
            titleBar2.Children.Add(title2);

            var confirmBtn = new Button { Content = "\u2713", FontSize = 16, Width = 28, Height = 28, Background = accent, Foreground = Brushes.White, BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
            Grid.SetColumn(confirmBtn, 1);
            titleBar2.Children.Add(confirmBtn);
            Grid.SetRow(titleBar2, 0);
            g2.Children.Add(titleBar2);

            var input = new TextBox { TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FontSize = 13 };
            Grid.SetRow(input, 1);
            g2.Children.Add(input);

            confirmBtn.Click += (s28, e28) =>
            {
                var txt = input.Text.Trim();
                if (!string.IsNullOrEmpty(txt))
                {
                    _commonItems.Add(txt);
                    RefreshCommonList();
                    SaveCommonItems();
                }
                dialog.Close();
            };

            Grid.SetRow(confirmBtn, 2);
            outerBorder.Child = g2;
            dialog.Content = outerBorder;

            outerBorder.MouseDown += (s29, e29) => { if (e29.LeftButton == MouseButtonState.Pressed) dialog.DragMove(); };

            dialog.ShowDialog();
        }

        // ==================== Toolbox Page ====================
        private string ToolboxPath { get { return Path.Combine(DataDir, "ClipboardTool.toolbox"); } }

        private Border BuildToolboxPage()
        {
            LoadToolbox();
            var page = new Border { Background = Brushes.Transparent };
            var layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            _toolboxScrollViewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Hidden };
            _toolboxListPanel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 4, 0, 0) };
            _toolboxScrollViewer.Content = _toolboxListPanel;
            _toolboxScrollViewer.ScrollChanged += OnToolboxScrollChanged;
            Grid.SetRow(_toolboxScrollViewer, 0);
            layout.Children.Add(_toolboxScrollViewer);

            page.Child = layout;
            return page;
        }

        private void OnToolboxScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            AnimateVisibleCards(_toolboxListPanel, _toolboxScrollViewer, _toolboxAnimatedIndices);
        }

        private void RefreshToolboxList()
        {
            _toolboxItems.Sort((a, b) => b.Count.CompareTo(a.Count));
            _toolboxListPanel.Children.Clear();
            _toolboxAnimatedIndices.Clear();

            for (int i = 0; i < _toolboxItems.Count; i++)
            {
                int idx = i;
                var card = BuildToolboxCard(_toolboxItems[i], idx);
                card.Opacity = 0;
                _toolboxListPanel.Children.Add(card);
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                // Cards already fit, no scroll needed; just fade in
                for (int j = 0; j < _toolboxListPanel.Children.Count; j++)
                {
                    var card = _toolboxListPanel.Children[j] as UIElement;
                    if (card != null)
                    {
                        var anim = new DoubleAnimation(1.0, TimeSpan.FromSeconds(0.15))
                        {
                            EasingFunction = new CircleEase { EasingMode = EasingMode.EaseOut }
                        };
                        card.BeginAnimation(UIElement.OpacityProperty, anim);
                    }
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private Border BuildToolboxCard(ToolboxItem item, int index)
        {
            var cardBg = (SolidColorBrush)new BrushConverter().ConvertFromString(ButtonSchemes[_currentButtonScheme].Normal);
            var cardTint = new SolidColorBrush(Color.FromArgb(20, cardBg.Color.R, cardBg.Color.G, cardBg.Color.B));

            var card = new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = cardTint,
                Margin = new Thickness(0, 0, 0, 4),
                Padding = new Thickness(12, 8, 8, 8),
                Cursor = Cursors.Hand,
            };

            var label = new TextBlock
            {
                Text = item.Label,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(55, 55, 66)),
                VerticalAlignment = VerticalAlignment.Center,
            };

            if (item.ActionType == ToolboxActionType.Countdown) _countdownLabel = label;
            if (item.ActionType == ToolboxActionType.Pomodoro) _pomodoroLabel = label;

            card.MouseDown += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    item.Count++;
                    SaveToolbox();
                    DoToolboxAction(item);
                    if (item.ActionType != ToolboxActionType.Countdown && item.ActionType != ToolboxActionType.Pomodoro)
                        RefreshToolboxList();
                }
            };

            card.Child = label;
            return card;
        }

        private void DoToolboxAction(ToolboxItem item)
        {
            switch (item.ActionType)
            {
                case ToolboxActionType.Process:
                    try { System.Diagnostics.Process.Start(item.ActionParam); } catch { }
                    break;
                case ToolboxActionType.RunAs:
                    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(item.ActionParam) { Verb = "RunAs" }); } catch { }
                    break;
                case ToolboxActionType.WinKey:
                    SendToolboxWinKey(item.ActionParam);
                    break;
                case ToolboxActionType.Countdown:
                    StartCountdown(item);
                    break;
                case ToolboxActionType.Pomodoro:
                    TogglePomodoro(item);
                    break;
            }
        }

        private void SendToolboxWinKey(string key)
        {
            byte vk = 0;
            if (key == "H") vk = 0x48;
            else if (key == ".") vk = 0xBE;
            if (vk == 0) return;

            uint selfThreadId = GetCurrentThreadId();
            Dispatcher.Invoke(() => Visibility = Visibility.Hidden, System.Windows.Threading.DispatcherPriority.Send);
            Thread.Sleep(20);
            IntPtr targetHwnd = _previousHwnd;
            if (targetHwnd != IntPtr.Zero)
            {
                uint targetTid = GetWindowThreadProcessId(targetHwnd, IntPtr.Zero);
                bool attached = (targetTid != 0 && targetTid != selfThreadId) ? AttachThreadInput(selfThreadId, targetTid, true) : false;
                Thread.Sleep(10); SetForegroundWindow(targetHwnd); Thread.Sleep(30);
                if (attached) AttachThreadInput(selfThreadId, targetTid, false);
                Thread.Sleep(60);
            }
            keybd_event(VK_LWIN, 0, 0, IntPtr.Zero);
            Thread.Sleep(30);
            keybd_event(vk, 0, 0, IntPtr.Zero);
            keybd_event(vk, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
            keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
            IntPtr myHwnd = new WindowInteropHelper(this).Handle;
            uint myTid = GetWindowThreadProcessId(myHwnd, IntPtr.Zero);
            bool attachedBack = (myTid != 0 && myTid != selfThreadId) ? AttachThreadInput(selfThreadId, myTid, true) : false;
            Thread.Sleep(10); SetForegroundWindow(myHwnd);
            if (attachedBack) AttachThreadInput(selfThreadId, myTid, false);
            Dispatcher.Invoke(() => { Visibility = Visibility.Visible; Opacity = 1.0; }, System.Windows.Threading.DispatcherPriority.Send);
        }

        private void StartCountdown(ToolboxItem item)
        {
            // Click while running = cancel
            if (_countdownTimer != null && _countdownTimer.IsEnabled)
            {
                _countdownTimer.Stop();
                _countdownTimer = null;
                _activeCountdownItem = null;
                if (_countdownLabel != null) _countdownLabel.Text = item.Label;
                return;
            }

            var scheme = ButtonSchemes[_currentButtonScheme];
            var accent = (SolidColorBrush)new BrushConverter().ConvertFromString(scheme.Normal);

            var dialog = new Window
            {
                Title = "\u5012\u8ba1\u65f6",
                Width = 280, Height = 180,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                Topmost = true,
            };

            var outerBorder = new Border
            {
                CornerRadius = new CornerRadius(0),
                Background = new SolidColorBrush(Color.FromArgb(250, 245, 245, 250)),
                Effect = new DropShadowEffect { BlurRadius = 16, ShadowDepth = 2, Color = Colors.Black, Opacity = 0.20 },
            };

            var g = new Grid { Margin = new Thickness(14) };
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var titleBar = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var title2 = new TextBlock { Text = "\u5012\u8ba1\u65f6\uff08\u5206\u949f\uff09", FontSize = 14, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center };
            Grid.SetColumn(title2, 0);
            titleBar.Children.Add(title2);

            var confirmBtn = new Button { Content = "\u2713", FontSize = 16, Width = 28, Height = 28, Background = accent, Foreground = Brushes.White, BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
            Grid.SetColumn(confirmBtn, 1);
            titleBar.Children.Add(confirmBtn);
            Grid.SetRow(titleBar, 0);
            g.Children.Add(titleBar);

            var input = new TextBox { FontSize = 16, TextAlignment = TextAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, Text = "5" };
            Grid.SetRow(input, 1);
            g.Children.Add(input);

            confirmBtn.Click += (sa, ea) =>
            {
                int minutes;
                if (int.TryParse(input.Text.Trim(), out minutes) && minutes > 0)
                {
                    _countdownRemaining = minutes * 60;
                    _activeCountdownItem = item;
                    _countdownTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                    _countdownTimer.Tick += CountdownTick;
                    _countdownTimer.Start();
                    UpdateCountdownLabel();
                    dialog.Close();
                }
            };

            Grid.SetRow(confirmBtn, 2);
            outerBorder.Child = g;
            dialog.Content = outerBorder;
            outerBorder.MouseDown += (sb, eb) => { if (eb.LeftButton == MouseButtonState.Pressed) dialog.DragMove(); };
            dialog.ShowDialog();
        }

        private void CountdownTick(object sender, EventArgs e)
        {
            _countdownRemaining--;
            UpdateCountdownLabel();
            if (_countdownRemaining <= 0)
            {
                _countdownTimer.Stop();
                _countdownTimer = null;
                if (_countdownLabel != null && _activeCountdownItem != null)
                    _countdownLabel.Text = _activeCountdownItem.Label;
                _activeCountdownItem = null;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    System.Windows.MessageBox.Show("\u65f6\u95f4\u5230\uff01", "\u5012\u8ba1\u65f6");
                    RefreshToolboxList();
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private void UpdateCountdownLabel()
        {
            if (_countdownLabel != null)
            {
                int m = _countdownRemaining / 60;
                int s = _countdownRemaining % 60;
                _countdownLabel.Text = _activeCountdownItem.Label + " " + m.ToString("D2") + ":" + s.ToString("D2");
            }
        }

        private void TogglePomodoro(ToolboxItem item)
        {
            if (_pomodoroTimer != null && _pomodoroTimer.IsEnabled)
            {
                _pomodoroTimer.Stop();
                _pomodoroTimer = null;
                _activePomodoroItem = null;
                _pomodoroPhase = 0;
                if (_pomodoroLabel != null) _pomodoroLabel.Text = item.Label;
                RefreshToolboxList();
                return;
            }

            _activePomodoroItem = item;
            _pomodoroPhase = 1;
            _pomodoroRemaining = 25 * 60;
            _pomodoroTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _pomodoroTimer.Tick += PomodoroTick;
            _pomodoroTimer.Start();
            UpdatePomodoroLabel();
        }

        private void PomodoroTick(object sender, EventArgs e)
        {
            _pomodoroRemaining--;
            UpdatePomodoroLabel();
            if (_pomodoroRemaining <= 0)
            {
                if (_pomodoroPhase == 1)
                {
                    _pomodoroPhase = 2;
                    _pomodoroRemaining = 5 * 60;
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        System.Windows.MessageBox.Show("\u4f11\u606f\u65f6\u95f4\uff01", "\u756a\u8304\u949f");
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
                else
                {
                    _pomodoroPhase = 1;
                    _pomodoroRemaining = 25 * 60;
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        System.Windows.MessageBox.Show("\u5de5\u4f5c\u65f6\u95f4\uff01", "\u756a\u8304\u949f");
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
                UpdatePomodoroLabel();
            }
        }

        private void UpdatePomodoroLabel()
        {
            if (_pomodoroLabel != null)
            {
                int m = _pomodoroRemaining / 60;
                int s = _pomodoroRemaining % 60;
                string phase = _pomodoroPhase == 1 ? "\u5de5\u4f5c" : "\u4f11\u606f";
                _pomodoroLabel.Text = _activePomodoroItem.Label + " " + phase + " " + m.ToString("D2") + ":" + s.ToString("D2");
            }
        }

        private void LoadToolbox()
        {
            var defaults = new List<ToolboxItem>
            {
                new ToolboxItem { Label = "\u7ba1\u7406\u5458Shell", ActionType = ToolboxActionType.RunAs, ActionParam = "powershell", Count = 0 },
                new ToolboxItem { Label = "CMD", ActionType = ToolboxActionType.Process, ActionParam = "cmd", Count = 0 },
                new ToolboxItem { Label = "\u684c\u9762\u952e\u76d8", ActionType = ToolboxActionType.Process, ActionParam = "osk.exe", Count = 0 },
                new ToolboxItem { Label = "\u5b57\u7b26\u5e93", ActionType = ToolboxActionType.Process, ActionParam = "charmap.exe", Count = 0 },
                new ToolboxItem { Label = "\u8bed\u97f3\u8f93\u5165", ActionType = ToolboxActionType.WinKey, ActionParam = "H", Count = 0 },
                new ToolboxItem { Label = "\u8868\u60c5\u5e93", ActionType = ToolboxActionType.WinKey, ActionParam = ".", Count = 0 },
                new ToolboxItem { Label = "\u8ba1\u7b97\u5668", ActionType = ToolboxActionType.Process, ActionParam = "calc", Count = 0 },
                new ToolboxItem { Label = "\u8bb0\u4e8b\u672c", ActionType = ToolboxActionType.Process, ActionParam = "notepad", Count = 0 },
                new ToolboxItem { Label = "\u4efb\u52a1\u7ba1\u7406\u5668", ActionType = ToolboxActionType.Process, ActionParam = "taskmgr", Count = 0 },
                new ToolboxItem { Label = "\u63a7\u5236\u9762\u677f", ActionType = ToolboxActionType.Process, ActionParam = "control", Count = 0 },
                new ToolboxItem { Label = "\u8d44\u6e90\u7ba1\u7406\u5668", ActionType = ToolboxActionType.Process, ActionParam = "explorer", Count = 0 },
                new ToolboxItem { Label = "\u5012\u8ba1\u65f6", ActionType = ToolboxActionType.Countdown, ActionParam = "", Count = 0 },
                new ToolboxItem { Label = "\u756a\u8304\u949f", ActionType = ToolboxActionType.Pomodoro, ActionParam = "", Count = 0 },
            };

            _toolboxItems = defaults;
            try
            {
                if (File.Exists(ToolboxPath))
                {
                    var counts = new Dictionary<string, int>();
                    foreach (var line in File.ReadAllLines(ToolboxPath))
                    {
                        var parts = line.Split(new[] { '=' }, 2);
                        if (parts.Length == 2)
                        {
                            int c;
                            if (int.TryParse(parts[1].Trim(), out c))
                                counts[parts[0].Trim()] = c;
                        }
                    }
                    foreach (var item2 in _toolboxItems)
                    {
                        if (counts.ContainsKey(item2.Label))
                            item2.Count = counts[item2.Label];
                    }
                }
            }
            catch { }
        }

        private void SaveToolbox()
        {
            try
            {
                var lines = new List<string>();
                foreach (var item in _toolboxItems)
                    lines.Add(item.Label + "=" + item.Count);
                File.WriteAllLines(ToolboxPath, lines.ToArray());
            }
            catch { }
        }

        // ==================== Shared Animation Helpers ====================
        private static void AnimateVisibleCards(StackPanel panel, ScrollViewer viewer, HashSet<int> animatedIndices)
        {
            for (int i = 0; i < panel.Children.Count; i++)
            {
                if (animatedIndices.Contains(i)) continue;
                var card = panel.Children[i] as UIElement;
                if (card == null) continue;

                var transform = card.TransformToVisual(viewer);
                var pos = transform.Transform(new Point(0, 0));
                double cardTop = pos.Y;
                double cardBottom = cardTop + card.RenderSize.Height;
                double viewBottom = viewer.ViewportHeight;

                if (cardBottom > 0 && cardTop < viewBottom && cardTop >= -10)
                {
                    animatedIndices.Add(i);
                    var anim = new DoubleAnimation(1.0, TimeSpan.FromSeconds(0.15))
                    {
                        EasingFunction = new CircleEase { EasingMode = EasingMode.EaseOut }
                    };
                    card.BeginAnimation(UIElement.OpacityProperty, anim);
                }
            }
        }

        private static void AnimatePasteScale(Border card)
        {
            if (card.RenderTransform == null || card.RenderTransform == Transform.Identity)
                card.RenderTransform = new ScaleTransform(1.0, 1.0);
            var st = card.RenderTransform as ScaleTransform;
            if (st == null) { st = new ScaleTransform(1.0, 1.0); card.RenderTransform = st; }

            var shrink = new DoubleAnimation(0.93, TimeSpan.FromSeconds(0.05));
            shrink.Completed += (s30, e30) =>
            {
                var grow = new DoubleAnimation(1.0, TimeSpan.FromSeconds(0.05));
                st.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
            };
            st.BeginAnimation(ScaleTransform.ScaleXProperty, shrink);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, shrink);
        }

        // ==================== Existing Methods ====================

        private UIElement BuildSettingsButton()
        {
            var btn = new Button
            {
Content = "\u22EF", Width = 24, Height = 24, FontSize = 18, FontWeight = FontWeights.Bold,
Foreground = new SolidColorBrush(Color.FromRgb(130, 130, 142)), Background = Brushes.Transparent,
                BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 2, 0), ToolTip = "\u8bbe\u7f6e",
            };
            btn.MouseEnter += (s, e) => { btn.Foreground = Brushes.White; btn.Background = new SolidColorBrush(Color.FromRgb(100, 149, 237)); };
            btn.MouseLeave += (s, e) => { btn.Foreground = new SolidColorBrush(Color.FromRgb(130, 130, 142)); btn.Background = Brushes.Transparent; };
            btn.Click += (s, e) => ToggleSettings();
            return btn;
        }

        private UIElement BuildCloseButton()
        {
            var btn = new Button
            {
Content = "\u00D7", Width = 24, Height = 24, FontSize = 16, FontWeight = FontWeights.Bold,
Foreground = new SolidColorBrush(Color.FromRgb(130, 130, 142)), Background = Brushes.Transparent,
                BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 2, 0), ToolTip = "\u5173\u95ed",
            };
            btn.MouseEnter += (s, e) => { btn.Foreground = Brushes.White; btn.Background = new SolidColorBrush(Color.FromRgb(231, 76, 60)); };
            btn.MouseLeave += (s, e) => { btn.Foreground = new SolidColorBrush(Color.FromRgb(130, 130, 142)); btn.Background = Brushes.Transparent; };
            btn.Click += (s, e) => Close();
            return btn;
        }


        private UIElement BuildSettingsPanel()
        {
            var panel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(4, 8, 4, 4) };
            var sep = new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(80, 180, 180, 190)), Margin = new Thickness(0, 0, 0, 4) };
            panel.Children.Add(sep);
            panel.Children.Add(BuildBackgroundColorRow());
            panel.Children.Add(BuildButtonColorRow());
            panel.Children.Add(BuildTextColorRow());
            var sep2 = new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(80, 180, 180, 190)), Margin = new Thickness(0, 4, 0, 0) };
            panel.Children.Add(sep2);
            panel.Children.Add(BuildToggleSwitch("\u7f6e\u9876", _isTopmost, (v) => { _isTopmost = v; ApplyTopmost(); SaveSettings(); }));
            panel.Children.Add(BuildToggleSwitch("\u8fb9\u7f18\u5438\u9644", _edgeSnapEnabled, (v) => { _edgeSnapEnabled = v; SaveSettings(); }));
            panel.Children.Add(BuildToggleSwitch("\u622a\u56fe\u63d0\u53d6\u6587\u5b57", _extractTextEnabled, (v) => { _extractTextEnabled = v; SaveSettings(); }));
            panel.Children.Add(BuildToggleSwitch("\u622a\u56fe\u63d0\u53d6\u56fe\u7247", _extractImageEnabled, (v) => { _extractImageEnabled = v; SaveSettings(); }));
            panel.Children.Add(BuildToggleSwitch("\u622a\u56fe\u5b58\u5165\u684c\u9762", _saveScreenshotToDesktop, (v) => { _saveScreenshotToDesktop = v; SaveSettings(); }));
            panel.Children.Add(BuildTransparencyRow());
            panel.Children.Add(BuildMaxHistoryRow());
            return new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 300, Content = panel };
        }

        private UIElement BuildMaxHistoryRow()
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 5, 4, 3) };
            var label = new TextBlock { Text = "\u6700\u5927\u8bb0\u5f55\u6570", FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = GetTextBrush(), VerticalAlignment = VerticalAlignment.Center, Width = 90 };
            row.Children.Add(label);
            var input = new TextBox { Width = 50, Height = 22, FontSize = 12, VerticalContentAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center, Text = _maxHistory.ToString() };
            row.Children.Add(input);
            input.LostFocus += (s, e) =>
            {
                int val;
                if (int.TryParse(input.Text.Trim(), out val) && val > 0)
                {
                    _maxHistory = val;
                    if (_clipboardHistory.Count > _maxHistory)
                        _clipboardHistory.RemoveRange(_maxHistory, _clipboardHistory.Count - _maxHistory);
                    SaveSettings();
                }
                else
                {
                    input.Text = _maxHistory.ToString();
                    var origBg = input.Background;
                    input.Background = new SolidColorBrush(Colors.Red);
                    var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
                    timer.Tick += (s2, e2) =>
                    {
                        var anim = new ColorAnimation { To = ((SolidColorBrush)origBg).Color, Duration = TimeSpan.FromSeconds(0.15) };
                        input.Background.BeginAnimation(SolidColorBrush.ColorProperty, anim);
                        timer.Stop();
                    };
                    timer.Start();
                    var timer2 = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(0.2) };
                    timer2.Tick += (s3, e3) => {
                        var anim = new ColorAnimation { To = Colors.Red, Duration = TimeSpan.FromSeconds(0.2) };
                        input.Background.BeginAnimation(SolidColorBrush.ColorProperty, anim);
                        timer2.Stop();
                    };
                    timer2.Start();
                }
            };
            input.KeyDown += (s, e) => { if (e.Key == Key.Enter) { var fe = Keyboard.FocusedElement as UIElement; if (fe != null) fe.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)); } };
            return row;
        }

        private StackPanel BuildTransparencyRow()
        {
            var container = new StackPanel { Orientation = Orientation.Vertical };

            StackPanel sliderRow = null;

            var toggleRow = BuildToggleSwitch("\u534a\u900f\u660e", _isTransparencyEnabled, (v) =>
            {
                _isTransparencyEnabled = v;
                Opacity = v ? _transparencyValue : 1.0;
                if (sliderRow != null) sliderRow.Visibility = v ? Visibility.Visible : Visibility.Collapsed;
                SaveSettings();
            });
            container.Children.Add(toggleRow);

            sliderRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(78, 2, 4, 3), Visibility = _isTransparencyEnabled ? Visibility.Visible : Visibility.Collapsed };
            var slider = new Slider { Minimum = 0.1, Maximum = 1.0, Value = _transparencyValue, Width = 120, SmallChange = 0.05, LargeChange = 0.1, VerticalAlignment = VerticalAlignment.Center };
            var valueLabel = new TextBlock { Text = (_transparencyValue * 100).ToString("F0") + "%", FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Foreground = new SolidColorBrush(Color.FromRgb(110, 110, 122)), Width = 36 };
            slider.ValueChanged += (s, e2) =>
            {
                _transparencyValue = e2.NewValue;
                valueLabel.Text = (e2.NewValue * 100).ToString("F0") + "%";
                if (_isTransparencyEnabled) Opacity = e2.NewValue;
                SaveSettings();
            };
            sliderRow.Children.Add(slider);
            sliderRow.Children.Add(valueLabel);
            container.Children.Add(sliderRow);

            return container;
        }

        private StackPanel BuildBackgroundColorRow()
        {
            var themeFg = GetTextBrush();
            var themeNormal = (SolidColorBrush)new BrushConverter().ConvertFromString(ButtonSchemes[_currentButtonScheme].Normal);
            var row = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 2, 0, 2) };
            var headerRow = new StackPanel { Orientation = Orientation.Horizontal };
            var expandBtn = new Button { Content = "\u80cc\u666f\u989c\u8272  \u203A", FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = themeFg, Background = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)), BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Padding = new Thickness(10, 5, 10, 5), HorizontalContentAlignment = HorizontalAlignment.Left };
            var expandNormalBg = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0));
            var expandHoverBg = new SolidColorBrush(themeNormal.Color);
            expandHoverBg.Opacity = 0.85;
            var expandBF = new FrameworkElementFactory(typeof(Border));
            expandBF.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            expandBF.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            var expandCP = new FrameworkElementFactory(typeof(ContentPresenter));
            expandCP.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            expandCP.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            expandBF.AppendChild(expandCP);
            var expandTpl = new ControlTemplate(typeof(Button)); expandTpl.VisualTree = expandBF; expandBtn.Template = expandTpl;

            var colorContainer = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(8, 4, 0, 0), Visibility = Visibility.Collapsed };
            var wr1 = new WrapPanel { Orientation = Orientation.Horizontal };
            foreach (var k in BgRow1) { if (BackgroundColors.ContainsKey(k)) wr1.Children.Add(BuildColorSwatch(k, BackgroundColors[k], _currentBackgroundColor, OnBackgroundColorSelected)); }
            var wr2 = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
            foreach (var k in BgRow2) { if (BackgroundColors.ContainsKey(k)) wr2.Children.Add(BuildColorSwatch(k, BackgroundColors[k], _currentBackgroundColor, OnBackgroundColorSelected)); }
            colorContainer.Children.Add(wr1); colorContainer.Children.Add(wr2);

            expandBtn.Click += (s, e) => { colorContainer.Visibility = colorContainer.Visibility == Visibility.Collapsed ? Visibility.Visible : Visibility.Collapsed; };
            expandBtn.MouseEnter += (s, e) => { expandBtn.Background = expandHoverBg; };
            expandBtn.MouseLeave += (s, e) => { expandBtn.Background = expandNormalBg; };
            headerRow.Children.Add(expandBtn); row.Children.Add(headerRow); row.Children.Add(colorContainer);
            return row;
        }

        private StackPanel BuildButtonColorRow()
        {
            var themeFg = GetTextBrush();
            var themeNormal = (SolidColorBrush)new BrushConverter().ConvertFromString(ButtonSchemes[_currentButtonScheme].Normal);
            var row = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 2, 0, 2) };
            var hr = new StackPanel { Orientation = Orientation.Horizontal };
            var btn = new Button { Content = "\u6309\u94ae\u989c\u8272  \u203A", FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = themeFg, Background = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)), BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Padding = new Thickness(10, 5, 10, 5), HorizontalContentAlignment = HorizontalAlignment.Left };
            var nBg = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0));
            var hBg = new SolidColorBrush(themeNormal.Color); hBg.Opacity = 0.85;
            var bf = new FrameworkElementFactory(typeof(Border)); bf.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            bf.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            bf.AppendChild(cp); var tpl = new ControlTemplate(typeof(Button)); tpl.VisualTree = bf; btn.Template = tpl;

            var picker = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(8, 4, 0, 0), Visibility = Visibility.Collapsed };
            var swatchRow = new WrapPanel { Orientation = Orientation.Horizontal };
            var currentKey = _useGradient ? _currentGradientScheme : _currentButtonScheme;
            foreach (var k in ButtonSchemes.Keys)
            {
                var dc = ButtonSchemes[k].Normal;
                swatchRow.Children.Add(BuildColorSwatch(k, dc, currentKey, (name) => {
                    _currentButtonScheme = name;
                    _currentGradientScheme = name;
                    RebuildButtonRow();
                    SaveSettings();
                    RebuildSettingsPanel();
                }));
            }
            picker.Children.Add(swatchRow);

            // \u6e10\u53d8\u5b50\u5f00\u5173
            var gradRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            var gradLabel = new TextBlock { Text = "\u6e10\u53d8\u6548\u679c", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = themeFg, VerticalAlignment = VerticalAlignment.Center, Width = 80 };
            gradRow.Children.Add(gradLabel);
            var gradTc = _useGradient ? new SolidColorBrush(themeNormal.Color) : new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));
            var gradTrack = new Border { Width = 40, Height = 20, CornerRadius = new CornerRadius(10), Background = gradTc, Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center };
            var gradThumb = new Border { Width = 16, Height = 16, CornerRadius = new CornerRadius(8), Background = Brushes.White, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(_useGradient ? 22 : 2, 0, 0, 0) };
            var gradCt = new Grid(); gradCt.Children.Add(gradThumb); gradTrack.Child = gradCt;
            bool gradOn = _useGradient;
            gradTrack.MouseDown += (s, e) => {
                gradOn = !gradOn;
                _useGradient = gradOn;
                gradThumb.BeginAnimation(FrameworkElement.MarginProperty, new ThicknessAnimation { To = new Thickness(gradOn ? 22 : 2, 0, 0, 0), Duration = TimeSpan.FromSeconds(0.2), AccelerationRatio = 0.3, DecelerationRatio = 0.3 });
                var toC = gradOn ? themeNormal.Color : Color.FromRgb(0xCC, 0xCC, 0xCC);
                gradTrack.Background.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation { To = toC, Duration = TimeSpan.FromSeconds(0.2) });
                RebuildButtonRow();
                SaveSettings();
            };
            gradRow.Children.Add(gradTrack);
            picker.Children.Add(gradRow);

            btn.Click += (s, e) => { picker.Visibility = picker.Visibility == Visibility.Collapsed ? Visibility.Visible : Visibility.Collapsed; };
            btn.MouseEnter += (s, e) => { btn.Background = hBg; };
            btn.MouseLeave += (s, e) => { btn.Background = nBg; };
            hr.Children.Add(btn); row.Children.Add(hr); row.Children.Add(picker);
            return row;
        }

        private StackPanel BuildTextColorRow()
        {
            var themeFg = GetTextBrush();
            var themeNormal = (SolidColorBrush)new BrushConverter().ConvertFromString(ButtonSchemes[_currentButtonScheme].Normal);
            var row = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 2, 0, 2) };
            var hr = new StackPanel { Orientation = Orientation.Horizontal };
            var btn = new Button { Content = "\u6587\u5b57\u989c\u8272  \u203A", FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = themeFg, Background = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)), BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Padding = new Thickness(10, 5, 10, 5), HorizontalContentAlignment = HorizontalAlignment.Left };
            var nBg = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0));
            var hBg = new SolidColorBrush(themeNormal.Color); hBg.Opacity = 0.85;
            var bf = new FrameworkElementFactory(typeof(Border));
            bf.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            bf.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            bf.AppendChild(cp); var tpl = new ControlTemplate(typeof(Button)); tpl.VisualTree = bf; btn.Template = tpl;

            var picker = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 4, 0, 0), Visibility = Visibility.Collapsed };
            foreach (var k in TextColorRow1)
            {
                if (TextColorOptions.ContainsKey(k))
                    picker.Children.Add(BuildColorSwatch(k, TextColorOptions[k], _currentTextColor, OnTextColorSelected));
            }

            btn.Click += (s, e) => { picker.Visibility = picker.Visibility == Visibility.Collapsed ? Visibility.Visible : Visibility.Collapsed; };
            btn.MouseEnter += (s, e) => { btn.Background = hBg; };
            btn.MouseLeave += (s, e) => { btn.Background = nBg; };
            hr.Children.Add(btn); row.Children.Add(hr); row.Children.Add(picker);
            return row;
        }

        private Border BuildColorSwatch(string name, string hexColor, string selectedKey, Action<string> onClick)
        {
            var b = (SolidColorBrush)new BrushConverter().ConvertFromString(hexColor);
            bool isSelected = (name == selectedKey);
            var accentColor = (SolidColorBrush)new BrushConverter().ConvertFromString(ButtonSchemes[_currentButtonScheme].Normal);
            var border = new Border
            {
                Width = 28, Height = 28, Margin = new Thickness(3),
                CornerRadius = new CornerRadius(6),
                Background = b, Cursor = Cursors.Hand, ToolTip = name,
                BorderThickness = new Thickness(isSelected ? 2.5 : 1.5),
                BorderBrush = isSelected ? new SolidColorBrush(accentColor.Color) : new SolidColorBrush(Color.FromArgb(50, 0, 0, 0)),
                RenderTransformOrigin = new Point(0.5, 0.5),
            };
            if (isSelected) border.RenderTransform = new ScaleTransform(1.08, 1.08);
            border.MouseEnter += (s, e) => {
                border.BorderBrush = new SolidColorBrush(accentColor.Color);
                border.BorderThickness = new Thickness(2.5);
                border.RenderTransform = new ScaleTransform(1.15, 1.15);
            };
            border.MouseLeave += (s, e) => {
                border.BorderBrush = isSelected ? new SolidColorBrush(accentColor.Color) : new SolidColorBrush(Color.FromArgb(50, 0, 0, 0));
                border.BorderThickness = new Thickness(isSelected ? 2.5 : 1.5);
                border.RenderTransform = isSelected ? new ScaleTransform(1.08, 1.08) : Transform.Identity;
            };
            border.MouseDown += (s, e) => { if (e.LeftButton == MouseButtonState.Pressed) onClick(name); };
            return border;
        }

        private StackPanel BuildToggleSwitch(string label, bool initialState, Action<bool> onChanged)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 5, 4, 3) };
            var lt = new TextBlock { Text = label, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = GetTextBrush(), VerticalAlignment = VerticalAlignment.Center, Width = 90 };
            row.Children.Add(lt);
            var tc = initialState ? (SolidColorBrush)new BrushConverter().ConvertFromString(ButtonSchemes[_currentButtonScheme].Normal) : new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));
            var track = new Border { Width = 44, Height = 22, CornerRadius = new CornerRadius(11), Background = tc, Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center };
            var thumb = new Border { Width = 18, Height = 18, CornerRadius = new CornerRadius(9), Background = Brushes.White, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(initialState ? 24 : 2, 0, 0, 0), Effect = new DropShadowEffect { BlurRadius = 2, ShadowDepth = 1, Color = Colors.Black, Opacity = 0.2 } };
            var ct = new Grid(); ct.Children.Add(thumb); track.Child = ct;
            bool isOn = initialState;
            track.MouseDown += (s, e) => {
                isOn = !isOn;
                thumb.BeginAnimation(FrameworkElement.MarginProperty, new ThicknessAnimation { To = new Thickness(isOn ? 24 : 2, 0, 0, 0), Duration = TimeSpan.FromSeconds(0.2), AccelerationRatio = 0.3, DecelerationRatio = 0.3 });
                var toC = isOn ? ((SolidColorBrush)new BrushConverter().ConvertFromString(ButtonSchemes[_currentButtonScheme].Normal)).Color : Color.FromRgb(0xCC, 0xCC, 0xCC);
                track.Background.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation { To = toC, Duration = TimeSpan.FromSeconds(0.2) });
                onChanged(isOn);
            };
            row.Children.Add(track);
            return row;
        }

        private LinearGradientBrush MakeGradient(string s, string e)
        {
            var b = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            b.GradientStops.Add(new GradientStop(((SolidColorBrush)new BrushConverter().ConvertFromString(s)).Color, 0));
            b.GradientStops.Add(new GradientStop(((SolidColorBrush)new BrushConverter().ConvertFromString(e)).Color, 1));
            return b;
        }

        private string DarkenHex(string hex, double f)
        {
            var c = ((SolidColorBrush)new BrushConverter().ConvertFromString(hex)).Color;
            return string.Format("#{0:X2}{1:X2}{2:X2}", (byte)(c.R * f), (byte)(c.G * f), (byte)(c.B * f));
        }

        private StackPanel BuildButtonRow()
        {
            var stack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            var scheme = ButtonSchemes[_currentButtonScheme];
            if (_useGradient && ButtonSchemes.ContainsKey(_currentGradientScheme))
            {
                var gs = ButtonSchemes[_currentGradientScheme];
                var ng = MakeGradient(gs.Hover, gs.Press);
                var hg = MakeGradient(gs.Normal, gs.Hover);
                var pg = MakeGradient(gs.Press, DarkenHex(gs.Press, 0.65));
                stack.Children.Add(BuildButtonGradient("\u590d\u5236", VK_C, ng, hg, pg, gs.Foreground));
                stack.Children.Add(BuildButtonGradient("\u526a\u5207", VK_X, ng, hg, pg, gs.Foreground));
                stack.Children.Add(BuildButtonGradient("\u7c98\u8d34", VK_V, ng, hg, pg, gs.Foreground));
                stack.Children.Add(BuildScreenshotButtonGradient(gs));
            }
            else
            {
                stack.Children.Add(BuildButton("\u590d\u5236", VK_C, scheme.Normal, scheme.Hover, scheme.Press, scheme.Foreground));
                stack.Children.Add(BuildButton("\u526a\u5207", VK_X, scheme.Normal, scheme.Hover, scheme.Press, scheme.Foreground));
                stack.Children.Add(BuildButton("\u7c98\u8d34", VK_V, scheme.Normal, scheme.Hover, scheme.Press, scheme.Foreground));
                stack.Children.Add(BuildScreenshotButton());
            }
            return stack;
        }

        private Button BuildButton(string text, byte kc, string nh, string hh, string ph, string fh = "#FFFFFF")
        {
            return BuildButtonCore(text, kc, (SolidColorBrush)new BrushConverter().ConvertFromString(nh), (SolidColorBrush)new BrushConverter().ConvertFromString(hh), (SolidColorBrush)new BrushConverter().ConvertFromString(ph), (SolidColorBrush)new BrushConverter().ConvertFromString(fh));
        }

        private Button BuildButtonGradient(string text, byte kc, Brush nb, Brush hb, Brush pb, string fh)
        {
            return BuildButtonCore(text, kc, nb, hb, pb, (SolidColorBrush)new BrushConverter().ConvertFromString(fh));
        }

        private Button BuildButtonCore(string text, byte kc, Brush nb, Brush hb, Brush pb, Brush fb)
        {
            var btn = new Button { Content = text, Width = 76, Height = 44, Margin = new Thickness(5), Cursor = Cursors.Hand, FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = fb, Background = nb, BorderThickness = new Thickness(0), RenderTransformOrigin = new Point(0.5, 0.5) };
            var bf = new FrameworkElementFactory(typeof(Border)); bf.SetValue(Border.CornerRadiusProperty, new CornerRadius(10)); bf.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty)); bf.SetValue(Border.EffectProperty, new DropShadowEffect { BlurRadius = 6, ShadowDepth = 2, Color = Colors.Black, Opacity = 0.25 });
            var cp = new FrameworkElementFactory(typeof(ContentPresenter)); cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center); cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            bf.AppendChild(cp); var tpl = new ControlTemplate(typeof(Button)); tpl.VisualTree = bf; btn.Template = tpl;
            btn.MouseEnter += (s, e) => { btn.Background = hb; btn.RenderTransform = new ScaleTransform(1.06, 1.06); var sh = btn.Effect as DropShadowEffect; if (sh != null) { sh.BlurRadius = 10; sh.Opacity = 0.35; } };
            btn.MouseLeave += (s, e) => { btn.Background = nb; btn.RenderTransform = Transform.Identity; var sh = btn.Effect as DropShadowEffect; if (sh != null) { sh.BlurRadius = 6; sh.Opacity = 0.25; } };
            btn.PreviewMouseLeftButtonDown += (s, e) => { btn.Background = pb; btn.RenderTransform = new ScaleTransform(0.93, 0.93); var sh = btn.Effect as DropShadowEffect; if (sh != null) { sh.BlurRadius = 3; sh.Opacity = 0.15; } };
            btn.PreviewMouseLeftButtonUp += (s, e) => { btn.Background = hb; btn.RenderTransform = new ScaleTransform(1.06, 1.06); var sh = btn.Effect as DropShadowEffect; if (sh != null) { sh.BlurRadius = 10; sh.Opacity = 0.35; } };
            btn.Click += (s, e) => SimulateCtrlKey(kc);
            return btn;
        }

        private Button BuildScreenshotButton()
        {
            var sc = ButtonSchemes[_currentButtonScheme];
            return BuildScreenshotCore((SolidColorBrush)new BrushConverter().ConvertFromString(sc.Normal), (SolidColorBrush)new BrushConverter().ConvertFromString(sc.Hover), (SolidColorBrush)new BrushConverter().ConvertFromString(sc.Press), (SolidColorBrush)new BrushConverter().ConvertFromString(sc.Foreground));
        }

        private Button BuildScreenshotButtonGradient(ButtonColorScheme gs)
        {
            return BuildScreenshotCore(MakeGradient(gs.Hover, gs.Press), MakeGradient(gs.Normal, gs.Hover), MakeGradient(gs.Press, DarkenHex(gs.Press, 0.65)), (SolidColorBrush)new BrushConverter().ConvertFromString(gs.Foreground));
        }

        private Button BuildScreenshotCore(Brush nb, Brush hb, Brush pb, Brush fb)
        {
            var btn = new Button { Content = "\u622a\u56fe", Width = 76, Height = 44, Margin = new Thickness(5), Cursor = Cursors.Hand, FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = fb, Background = nb, BorderThickness = new Thickness(0), RenderTransformOrigin = new Point(0.5, 0.5) };
            var bf = new FrameworkElementFactory(typeof(Border)); bf.SetValue(Border.CornerRadiusProperty, new CornerRadius(10)); bf.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty)); bf.SetValue(Border.EffectProperty, new DropShadowEffect { BlurRadius = 6, ShadowDepth = 2, Color = Colors.Black, Opacity = 0.25 });
            var cp = new FrameworkElementFactory(typeof(ContentPresenter)); cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center); cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            bf.AppendChild(cp); var tpl = new ControlTemplate(typeof(Button)); tpl.VisualTree = bf; btn.Template = tpl;
            btn.MouseEnter += (s, e) => { btn.Background = hb; btn.RenderTransform = new ScaleTransform(1.06, 1.06); var sh = btn.Effect as DropShadowEffect; if (sh != null) { sh.BlurRadius = 10; sh.Opacity = 0.35; } };
            btn.MouseLeave += (s, e) => { btn.Background = nb; btn.RenderTransform = Transform.Identity; var sh = btn.Effect as DropShadowEffect; if (sh != null) { sh.BlurRadius = 6; sh.Opacity = 0.25; } };
            btn.PreviewMouseLeftButtonDown += (s, e) => { btn.Background = pb; btn.RenderTransform = new ScaleTransform(0.93, 0.93); var sh = btn.Effect as DropShadowEffect; if (sh != null) { sh.BlurRadius = 3; sh.Opacity = 0.15; } };
            btn.PreviewMouseLeftButtonUp += (s, e) => { btn.Background = hb; btn.RenderTransform = new ScaleTransform(1.06, 1.06); var sh = btn.Effect as DropShadowEffect; if (sh != null) { sh.BlurRadius = 10; sh.Opacity = 0.35; } };
            btn.Click += (s, e) => LaunchScreenshot();
            return btn;
        }

        private void ToggleSettings()
        {
            _isSettingsOpen = !_isSettingsOpen;
            if (_isSettingsOpen) { _settingsPanel.Visibility = Visibility.Visible; SizeToContent = SizeToContent.Height; MinHeight = 90; }
            else { _settingsPanel.Visibility = Visibility.Collapsed; SizeToContent = SizeToContent.Manual; Height = _currentPage == 0 ? 120 : 440; MinHeight = _currentPage == 0 ? 120 : 440; UpdateLayout(); }
        }

        private void OnBackgroundColorSelected(string n) { string hex; if (BackgroundColors.TryGetValue(n, out hex)) { _currentBackgroundColor = n; var b = (SolidColorBrush)new BrushConverter().ConvertFromString(hex); _mainBorder.Background = b; SaveSettings(); RebuildSettingsPanel(); } }
        private void OnButtonColorSelected(string n) { if (ButtonSchemes.ContainsKey(n)) { _currentButtonScheme = n; _currentGradientScheme = n; RebuildButtonRow(); SaveSettings(); RebuildSettingsPanel(); } }
        private void OnGradientColorSelected(string n) { if (ButtonSchemes.ContainsKey(n)) { _useGradient = true; _currentGradientScheme = n; RebuildButtonRow(); SaveSettings(); RebuildSettingsPanel(); } }

        private SolidColorBrush GetTextBrush()
        {
            string hex;
            if (!TextColorOptions.TryGetValue(_currentTextColor, out hex)) hex = "#424242";
            try { return (SolidColorBrush)new BrushConverter().ConvertFromString(hex); }
            catch { return new SolidColorBrush(Color.FromRgb(0x42, 0x42, 0x42)); }
        }
        private void OnTextColorSelected(string n) { if (TextColorOptions.ContainsKey(n)) { _currentTextColor = n; SaveSettings(); RebuildSettingsPanel(); } }

        private void RebuildSettingsPanel()
        {
            var old = _settingsPanel;
            var newPanel = BuildSettingsPanel();
            Grid.SetRow(newPanel, 2);
            _mainGrid.Children.Remove(old);
            _mainGrid.Children.Add(newPanel);
            _settingsPanel = newPanel;
            if (_isSettingsOpen) { newPanel.Visibility = Visibility.Visible; SizeToContent = SizeToContent.Height; MinHeight = 90; }
            else { newPanel.Visibility = Visibility.Collapsed; }
        }

        private void ApplyBackgroundColor()
        {
            string hex;
            if (BackgroundColors.TryGetValue(_currentBackgroundColor, out hex))
            {
                var b = (SolidColorBrush)new BrushConverter().ConvertFromString(hex);
                _mainBorder.Background = b;
            }
        }

        private void RebuildButtonRow()
        {
            var old = _buttonRow; _buttonRow = BuildButtonRow();
            _page0.Child = _buttonRow;
        }

        private void GetDpiScale(out double scaleX, out double scaleY)
        {
            scaleX = 1.0;
            scaleY = 1.0;
            var source = PresentationSource.FromVisual(this);
            if (source != null && source.CompositionTarget != null)
            {
                scaleX = source.CompositionTarget.TransformToDevice.M11;
                scaleY = source.CompositionTarget.TransformToDevice.M22;
            }
        }

        private void SnapToEdge()
        {
            const int d = 20;
            var a = SystemParameters.WorkArea;

            // Use Win32 API to get the actual window position (WPF Left/Top/ActualWidth may be stale after DragMove)
            var hwnd = new WindowInteropHelper(this).Handle;
            RECT r;
            if (!GetWindowRect(hwnd, out r)) return;

            // GetWindowRect returns physical pixels; convert to DIPs for WPF Left/Top
            double scaleX, scaleY;
            GetDpiScale(out scaleX, out scaleY);
            double w = (r.Right - r.Left) / scaleX;
            double h = (r.Bottom - r.Top) / scaleY;
            double x = r.Left / scaleX;
            double y = r.Top / scaleY;

            // Clamp: bring window back into screen bounds if it goes off-screen
            if (x < a.Left) x = a.Left;
            if (x + w > a.Right) x = a.Right - w;
            if (y < a.Top) y = a.Top;
            if (y + h > a.Bottom) y = a.Bottom - h;

            // Snap: pull to edge when near (within d pixels)
            if (Math.Abs(x - a.Left) < d) x = a.Left;
            else if (Math.Abs(x + w - a.Right) < d) x = a.Right - w;
            if (Math.Abs(y - a.Top) < d) y = a.Top;
            else if (Math.Abs(y + h - a.Bottom) < d) y = a.Bottom - h;

            // Apply the corrected position
            Left = x;
            Top = y;
        }

        private void StartAutoHideTimer()
        {
            CancelAutoHide();
            if (!_autoHideEnabled || _isAutoHidden) return;

            var area = SystemParameters.WorkArea;
            var hwnd = new WindowInteropHelper(this).Handle;
            RECT r;
            if (!GetWindowRect(hwnd, out r)) return;

            double scaleX, scaleY;
            GetDpiScale(out scaleX, out scaleY);
            double x = r.Left / scaleX, y = r.Top / scaleY, w = (r.Right - r.Left) / scaleX, h = (r.Bottom - r.Top) / scaleY;
            string edge = "";
            if (Math.Abs(x - area.Left) < 3) edge = "left";
            else if (Math.Abs(x + w - area.Right) < 3) edge = "right";
            else if (Math.Abs(y - area.Top) < 3) edge = "top";
            else if (Math.Abs(y + h - area.Bottom) < 3) edge = "bottom";
            if (edge == "") return;

            _autoHiddenEdge = edge;
            _restoreLeft = Left;
            _restoreTop = Top;

            _autoHideTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            _autoHideTimer.Tick += AutoHideTick;
            _autoHideTimer.Start();
        }

        private void CancelAutoHide()
        {
            if (_autoHideTimer != null) { _autoHideTimer.Stop(); _autoHideTimer = null; }
        }

        private void AutoHideTick(object sender, EventArgs e)
        {
            CancelAutoHide();
            _isAutoHidden = true;

            var area = SystemParameters.WorkArea;
            var hwnd = new WindowInteropHelper(this).Handle;
            RECT r;
            GetWindowRect(hwnd, out r);
            double scaleX, scaleY;
            GetDpiScale(out scaleX, out scaleY);
            double w = (r.Right - r.Left) / scaleX, h = (r.Bottom - r.Top) / scaleY;

            double targetLeft = _restoreLeft, targetTop = _restoreTop;
            if (_autoHiddenEdge == "left") targetLeft = area.Left + 3 - w;
            else if (_autoHiddenEdge == "right") targetLeft = area.Right - 3;
            else if (_autoHiddenEdge == "top") targetTop = area.Top + 3 - h;
            else if (_autoHiddenEdge == "bottom") targetTop = area.Bottom - 3;

            var ax = new DoubleAnimation(targetLeft, TimeSpan.FromSeconds(0.2)) { EasingFunction = new CircleEase { EasingMode = EasingMode.EaseOut } };
            var ay = new DoubleAnimation(targetTop, TimeSpan.FromSeconds(0.2)) { EasingFunction = new CircleEase { EasingMode = EasingMode.EaseOut } };
            ax.Completed += (s2, e2) => { BeginAnimation(LeftProperty, null); Left = targetLeft; };
            ay.Completed += (s2, e2) => { BeginAnimation(TopProperty, null); Top = targetTop; };
            BeginAnimation(LeftProperty, ax);
            BeginAnimation(TopProperty, ay);
        }

        private void RestoreFromAutoHide()
        {
            if (!_isAutoHidden) return;
            _isAutoHidden = false;
            CancelAutoHide();
            var ax = new DoubleAnimation(_restoreLeft, TimeSpan.FromSeconds(0.2)) { EasingFunction = new CircleEase { EasingMode = EasingMode.EaseOut } };
            var ay = new DoubleAnimation(_restoreTop, TimeSpan.FromSeconds(0.2)) { EasingFunction = new CircleEase { EasingMode = EasingMode.EaseOut } };
            ax.Completed += (s2, e2) => { BeginAnimation(LeftProperty, null); Left = _restoreLeft; };
            ay.Completed += (s2, e2) => { BeginAnimation(TopProperty, null); Top = _restoreTop; };
            BeginAnimation(LeftProperty, ax);
            BeginAnimation(TopProperty, ay);
        }

        private void ApplyTopmost() { Topmost = _isTopmost; }

        private string DataDir
        {
            get
            {
                var dir = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "ClipboardToolData");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        private string SettingsPath { get { return Path.Combine(DataDir, "ClipboardTool.settings"); } }

        private string CommonItemsPath { get { return Path.Combine(DataDir, "ClipboardTool.common"); } }

        private string HistoryPath { get { return Path.Combine(DataDir, "ClipboardTool.history"); } }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    foreach (var line in File.ReadAllLines(SettingsPath))
                    {
                        var parts = line.Split(new[] { '=' }, 2);
                        if (parts.Length != 2) continue;
                        var k = parts[0].Trim(); var v = parts[1].Trim();
                        if (k == "buttonScheme" && ButtonSchemes.ContainsKey(v)) _currentButtonScheme = v;
                        else if (k == "bgColor" && BackgroundColors.ContainsKey(v)) _currentBackgroundColor = v;
                        else if (k == "useGradient") bool.TryParse(v, out _useGradient);
                        else if (k == "gradientScheme" && ButtonSchemes.ContainsKey(v)) _currentGradientScheme = v;
                        else if (k == "textColor" && TextColorOptions.ContainsKey(v)) _currentTextColor = v;
                        else if (k == "topmost") bool.TryParse(v, out _isTopmost);
                        else if (k == "edgeSnap") bool.TryParse(v, out _edgeSnapEnabled);
                        else if (k == "maxHistory") { int mh; if (int.TryParse(v, out mh) && mh > 0) _maxHistory = mh; }
                        else if (k == "transparencyEnabled") bool.TryParse(v, out _isTransparencyEnabled);
                        else if (k == "transparencyValue") double.TryParse(v, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _transparencyValue);
                        else if (k == "autoHide") bool.TryParse(v, out _autoHideEnabled);
                        else if (k == "extractText") bool.TryParse(v, out _extractTextEnabled);
                        else if (k == "extractImage") bool.TryParse(v, out _extractImageEnabled);
                        else if (k == "saveScreenshot") bool.TryParse(v, out _saveScreenshotToDesktop);
                    }
                }
                LoadCommonItems();
                LoadClipboardHistory();
            }
            catch { }
        }

        private void SaveSettings()
        {
            try
            {
                File.WriteAllLines(SettingsPath, new string[] {
                    "buttonScheme=" + _currentButtonScheme, "bgColor=" + _currentBackgroundColor,
                    "useGradient=" + _useGradient.ToString().ToLower(), "gradientScheme=" + _currentGradientScheme,
                    "textColor=" + _currentTextColor,
                    "topmost=" + _isTopmost.ToString().ToLower(), "edgeSnap=" + _edgeSnapEnabled.ToString().ToLower(),
                    "maxHistory=" + _maxHistory.ToString(),
                    "transparencyEnabled=" + _isTransparencyEnabled.ToString().ToLower(), "transparencyValue=" + _transparencyValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "autoHide=" + _autoHideEnabled.ToString().ToLower(), "extractText=" + _extractTextEnabled.ToString().ToLower(), "extractImage=" + _extractImageEnabled.ToString().ToLower(), "saveScreenshot=" + _saveScreenshotToDesktop.ToString().ToLower(),
                });
            }
            catch { }
        }

        private void LoadCommonItems()
        {
            try
            {
                if (File.Exists(CommonItemsPath))
                    _commonItems = new List<string>(File.ReadAllLines(CommonItemsPath));
            }
            catch { _commonItems = new List<string>(); }
        }

        private void SaveCommonItems()
        {
            try { File.WriteAllLines(CommonItemsPath, _commonItems.ToArray()); }
            catch { }
        }

        private void LoadClipboardHistory()
        {
            try
            {
                _clipboardHistory = new List<string>();
                if (File.Exists(HistoryPath))
                {
                    foreach (var line in File.ReadAllLines(HistoryPath))
                    {
                        if (string.IsNullOrEmpty(line)) continue;
                        try
                        {
                            var bytes = Convert.FromBase64String(line);
                            _clipboardHistory.Add(System.Text.Encoding.UTF8.GetString(bytes));
                        }
                        catch
                        {
                            // 兼容旧格式：未编码的纯文本行
                            _clipboardHistory.Add(line);
                        }
                    }
                }
            }
            catch { _clipboardHistory = new List<string>(); }
        }

        private void SaveClipboardHistory()
        {
            try
            {
                var lines = new List<string>();
                foreach (var item in _clipboardHistory)
                {
                    var bytes = System.Text.Encoding.UTF8.GetBytes(item);
                    lines.Add(Convert.ToBase64String(bytes));
                }
                File.WriteAllLines(HistoryPath, lines.ToArray());
            }
            catch { }
        }

        private void SimulateCtrlKey(byte key)
        {
            IntPtr myHwnd = new WindowInteropHelper(this).Handle;
            uint selfThreadId = GetCurrentThreadId();
            Dispatcher.Invoke(() => Visibility = Visibility.Hidden, System.Windows.Threading.DispatcherPriority.Send);
            Thread.Sleep(30);
            IntPtr targetHwnd = _previousHwnd;
            if (targetHwnd != IntPtr.Zero && targetHwnd != myHwnd)
            {
                uint targetTid = GetWindowThreadProcessId(targetHwnd, IntPtr.Zero);
                bool attached = (targetTid != 0 && targetTid != selfThreadId) ? AttachThreadInput(selfThreadId, targetTid, true) : false;
                Thread.Sleep(10); SetForegroundWindow(targetHwnd); Thread.Sleep(30);
                if (attached) AttachThreadInput(selfThreadId, targetTid, false);
            }
            Thread.Sleep(80);
            keybd_event(VK_CONTROL, 0, 0, IntPtr.Zero); keybd_event(key, 0, 0, IntPtr.Zero); keybd_event(key, 0, KEYEVENTF_KEYUP, IntPtr.Zero); keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
            Thread.Sleep(50);
            uint myTid = GetWindowThreadProcessId(myHwnd, IntPtr.Zero);
            bool attachedBack = (myTid != 0 && myTid != selfThreadId) ? AttachThreadInput(selfThreadId, myTid, true) : false;
            Thread.Sleep(10); SetForegroundWindow(myHwnd);
            if (attachedBack) AttachThreadInput(selfThreadId, myTid, false);
            Dispatcher.Invoke(() => { Visibility = Visibility.Visible; Opacity = 1.0; }, System.Windows.Threading.DispatcherPriority.Send);
        }

        private void LaunchScreenshot()
        {

            uint selfThreadId = GetCurrentThreadId();
            Dispatcher.Invoke(() => Visibility = Visibility.Hidden, System.Windows.Threading.DispatcherPriority.Send);
            Thread.Sleep(20);
            try { Clipboard.Clear(); } catch { }
            Thread.Sleep(10);
            IntPtr targetHwnd = _previousHwnd;
            if (targetHwnd != IntPtr.Zero) {
                uint targetTid = GetWindowThreadProcessId(targetHwnd, IntPtr.Zero);
                bool attached = (targetTid != 0 && targetTid != selfThreadId) ? AttachThreadInput(selfThreadId, targetTid, true) : false;
                Thread.Sleep(10); SetForegroundWindow(targetHwnd); Thread.Sleep(30);
                if (attached) AttachThreadInput(selfThreadId, targetTid, false);
                Thread.Sleep(60);
            }
            keybd_event(VK_LWIN, 0, 0, IntPtr.Zero); keybd_event(VK_SHIFT, 0, 0, IntPtr.Zero); keybd_event(VK_S, 0, 0, IntPtr.Zero);
            keybd_event(VK_S, 0, KEYEVENTF_KEYUP, IntPtr.Zero); keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, IntPtr.Zero); keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
            _screenshotPollCount = 0;
            _screenshotTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _screenshotTimer.Tick += ScreenshotTimer_Tick; _screenshotTimer.Start();
        }

        private void ScreenshotTimer_Tick(object sender, EventArgs e)
        {
            _screenshotPollCount++;
            try
            {
                bool hasImage = Clipboard.ContainsImage();
                bool hasText = false;
                try { hasText = Clipboard.ContainsText(); } catch { }
                if (!hasImage && !hasText)
                {
                    try
                    {
                        var dataObj = Clipboard.GetDataObject();
                        if (dataObj != null)
                        {
                            var formats = dataObj.GetFormats();
                            if (formats != null && formats.Length > 0)
                                hasImage = true;
                        }
                        else
                        {
                        }
                    }
                    catch
                    {
                    }
                }
                else
                {
                }
                if (hasImage)
                {
                    ProcessScreenshot();
                    RestoreAfterScreenshot();
                    return;
                }
            }
            catch (Exception ex)
            {
            }
            if (_screenshotPollCount >= ScreenshotTimeoutCount)
            {
                RestoreAfterScreenshot();
            }
        }

        private void ProcessScreenshot()
        {

            var image = Clipboard.GetImage();
            if (image == null)
            {
                try
                {
                    var dataObj = Clipboard.GetDataObject();
                    if (dataObj != null)
                    {
                        var formats = dataObj.GetFormats();

                        if (formats != null)
                        {
                            foreach (var fmt in formats)
                            {
                                try
                                {
                                    var data = dataObj.GetData(fmt);
                                    if (data is BitmapSource)
                                    {
                                        image = (BitmapSource)data;
                                        break;
                                    }
                                    if (false)
                                    {
                                        var bmp = (object)data;
                                        var hBitmap = IntPtr.Zero;
                                        try
                                        {
                                            image = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                                                hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                                        }
                                        finally { DeleteObject(hBitmap); }
                                        break;
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                }

                if (image == null)
                {
                    return;
                }
            }


            bool keepImage = _extractImageEnabled;

            if (_extractTextEnabled)
            {
                StartOcrAsync(image);
            }
            else
            {
            }

            if (keepImage)
            {
                _clipboardHistory.Insert(0, "[截图]");
                if (_clipboardHistory.Count > _maxHistory) _clipboardHistory.RemoveRange(_maxHistory, _clipboardHistory.Count - _maxHistory);
                if (_currentPage == 1) RefreshClipboardList();
            }

            if (_saveScreenshotToDesktop && keepImage)
            {
                SaveImageToDesktop(image);
            }

            if (!keepImage)
            {
                try { Clipboard.Clear(); } catch { }
            }
        }

        private void SaveImageToDesktop(BitmapSource image)
        {
            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string filename = string.Format("\u622a\u56fe_{0:yyyyMMdd_HHmmss}.png", DateTime.Now);
                string path = Path.Combine(desktop, filename);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
                using (var stream = new FileStream(path, FileMode.Create)) encoder.Save(stream);
            }
            catch { }
        }

        private void StartOcrAsync(BitmapSource image)
        {
            // All WPF image operations must happen on the UI thread
            byte[] pngBytes = null;
            try
            {
                BitmapSource prepared = image;
                if (prepared.Format != PixelFormats.Bgra32)
                {
                    prepared = new FormatConvertedBitmap(prepared, PixelFormats.Bgra32, null, 0);
                }

                if (prepared.PixelWidth < 500)
                {
                    prepared = new TransformedBitmap(prepared, new ScaleTransform(2.0, 2.0));
                }

                if (prepared.CanFreeze) prepared.Freeze();

                using (var ms = new MemoryStream())
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(prepared));
                    encoder.Save(ms);
                    pngBytes = ms.ToArray();
                }
            }
            catch (Exception ex)
            {
                return;
            }

            if (pngBytes == null) return;

            // Now run OCR on a background thread (no WPF objects needed here)
            System.Threading.ThreadPool.QueueUserWorkItem(state =>
            {
                try
                {
                    string text = null;
                    lock (_ocrLock)
                    {
                        if (_ocrEngine == null)
                        {
                            string exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                            string tessdataDir = Path.Combine(exeDir, "tessdata");
                            _ocrEngine = new Tesseract.TesseractEngine(tessdataDir, "chi_sim", Tesseract.EngineMode.LstmOnly);
                        }

                        using (var pix = Tesseract.Pix.LoadFromMemory(pngBytes))
                        using (var page = _ocrEngine.Process(pix, Tesseract.PageSegMode.Auto))
                        {
                            text = page.GetText();
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        text = RemoveChineseSpaces(text.Trim());
                        if (!string.IsNullOrEmpty(text))
                        {
                            Dispatcher.Invoke(() =>
                            {
                                _clipboardHistory.Remove(text);
                                _clipboardHistory.Insert(0, text);
                                if (_clipboardHistory.Count > _maxHistory)
                                    _clipboardHistory.RemoveRange(_maxHistory, _clipboardHistory.Count - _maxHistory);
                                if (_currentPage == 1) RefreshClipboardList();
                                ShowOcrToast();
                            });
                        }
                    }
                    else
                    {
                    }
                }
                catch (Exception ex)
                {
                }
            });
        }

        private void ShowOcrToast()
        {
            var toast = new Window
            {
                Width = 200, Height = 44,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                Topmost = true,
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = Left + (Width - 200) / 2,
                Top = Top - 52,
                ResizeMode = ResizeMode.NoResize,
            };
            var border = new Border
            {
                CornerRadius = new CornerRadius(0),
                Background = new SolidColorBrush(Color.FromArgb(230, 40, 40, 45)),
                Effect = new DropShadowEffect { BlurRadius = 8, ShadowDepth = 2, Color = Colors.Black, Opacity = 0.3 },
                Child = new TextBlock
                {
                    Text = "\u8bc6\u522b\u5b8c\u6210\uff0c\u5df2\u5199\u5165\u526a\u8d34\u677f",
                    FontSize = 13,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                }
            };
            toast.Content = border;
            toast.Opacity = 0;
            toast.Loaded += (s, e) =>
            {
                var fadeIn = new DoubleAnimation(1.0, TimeSpan.FromSeconds(0.25));
                toast.BeginAnimation(Window.OpacityProperty, fadeIn);
                var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                t.Tick += (s2, e2) =>
                {
                    t.Stop();
                    var fadeOut = new DoubleAnimation(0, TimeSpan.FromSeconds(0.3));
                    fadeOut.Completed += (s3, e3) => toast.Close();
                    toast.BeginAnimation(Window.OpacityProperty, fadeOut);
                };
                t.Start();
            };
            toast.Show();
        }

        private static string RemoveChineseSpaces(string text)
        {
            if (text.Length < 3) return text;
            var sb = new System.Text.StringBuilder();
            sb.Append(text[0]);
            for (int i = 1; i < text.Length - 1; i++)
            {
                if (text[i] != ' ')
                {
                    sb.Append(text[i]);
                    continue;
                }
                char left = text[i - 1];
                char right = text[i + 1];
                bool leftIsCJK = left >= 0x4E00 && left <= 0x9FFF;
                bool rightIsCJK = right >= 0x4E00 && right <= 0x9FFF;
                if (text[i - 1] == ' ') continue;
                if (leftIsCJK && rightIsCJK)
                    continue;
                sb.Append(text[i]);
            }
            sb.Append(text[text.Length - 1]);
            return sb.ToString();
        }

        private void RestoreAfterScreenshot()
        {
            if (_screenshotTimer != null) { _screenshotTimer.Stop(); _screenshotTimer.Tick -= ScreenshotTimer_Tick; _screenshotTimer = null; }
            IntPtr myHwnd = new WindowInteropHelper(this).Handle;
            uint selfThreadId = GetCurrentThreadId();
            uint myTid = GetWindowThreadProcessId(myHwnd, IntPtr.Zero);
            bool attached = (myTid != 0 && myTid != selfThreadId) ? AttachThreadInput(selfThreadId, myTid, true) : false;
            Thread.Sleep(10); SetForegroundWindow(myHwnd);
            if (attached) AttachThreadInput(selfThreadId, myTid, false);
            Dispatcher.Invoke(() => { Visibility = Visibility.Visible; Opacity = 1.0; }, System.Windows.Threading.DispatcherPriority.Send);
        }
    }
}
