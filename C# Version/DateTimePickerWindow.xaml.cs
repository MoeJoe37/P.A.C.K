using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace PACK
{
    public partial class DateTimePickerWindow : Window
    {
        // ── Public result ─────────────────────────────────────────────────────
        public DateTime? SelectedDateTime { get; private set; }

        // ── View state ────────────────────────────────────────────────────────
        private enum CalView { Days, Months, Years, Time }
        private CalView _view = CalView.Days;

        private DateTime _display;   // first day of the currently shown month
        private DateTime _selected;  // chosen date
        private int _h, _m, _s;     // time components

        // ── Brush palette (matches the main app theme) ────────────────────────
        private static SolidColorBrush Br(string hex) =>
            new((Color)ColorConverter.ConvertFromString(hex));

        private static readonly SolidColorBrush BrBg      = Br("#1E1E2E");
        private static readonly SolidColorBrush BrSurf    = Br("#313244");
        private static readonly SolidColorBrush BrSurf2   = Br("#45475A");
        private static readonly SolidColorBrush BrAccent  = Br("#CBA6F7");
        private static readonly SolidColorBrush BrSelBg   = Br("#5A3878");  // selected cell background
        private static readonly SolidColorBrush BrHover   = Br("#2E2E44");  // hover background
        private static readonly SolidColorBrush BrTodayBg = Br("#252535");  // today background
        private static readonly SolidColorBrush BrTxt     = Br("#CDD6F4");
        private static readonly SolidColorBrush BrSub     = Br("#A6ADC8");
        private static readonly SolidColorBrush BrDim     = Br("#4A4A62");
        private static readonly SolidColorBrush BrGreen   = Br("#A6E3A1");
        private static readonly SolidColorBrush BrTrans   = new(Colors.Transparent);

        // ── Constructor ───────────────────────────────────────────────────────
        public DateTimePickerWindow(DateTime? init = null)
        {
            InitializeComponent();
            var d      = init ?? DateTime.Now.AddMinutes(5);
            _display   = new DateTime(d.Year, d.Month, 1);
            _selected  = d.Date;
            _h = d.Hour; _m = d.Minute; _s = d.Second;
            Loaded += (_, _) => Render();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  TOP-LEVEL RENDER DISPATCHER
        // ═════════════════════════════════════════════════════════════════════
        private void Render()
        {
            ContentHost.Children.Clear();
            ContentHost.RowDefinitions.Clear();
            ContentHost.ColumnDefinitions.Clear();

            UpdateHeader();
            UpdateFooter();

            switch (_view)
            {
                case CalView.Days:   RenderDays();   break;
                case CalView.Months: RenderMonths(); break;
                case CalView.Years:  RenderYears();  break;
                case CalView.Time:   RenderTime();   break;
            }
        }

        private void UpdateHeader()
        {
            // Hide the nav header entirely in time view (row auto-collapses)
            NavHeader.Visibility = _view == CalView.Time
                ? Visibility.Collapsed
                : Visibility.Visible;

            TitleBtn.Content = _view switch
            {
                CalView.Days   => _display.ToString("MMMM yyyy"),
                CalView.Months => _display.Year.ToString(),
                CalView.Years  => GetDecadeLabel(_display.Year),
                _              => ""
            };
        }

        private void UpdateFooter()
        {
            // Toggle icon: clock in calendar views, calendar icon in time view
            FooterToggleBtn.Content  = _view == CalView.Time ? "📅" : "⏱";
        }

        private static string GetDecadeLabel(int year)
        {
            int s = (year / 10) * 10;
            return $"{s}-{s + 9}";
        }

        // ═════════════════════════════════════════════════════════════════════
        //  NAV BUTTONS
        // ═════════════════════════════════════════════════════════════════════
        private void Prev_Click(object s, RoutedEventArgs e)
        {
            switch (_view)
            {
                case CalView.Days:   _display = _display.AddMonths(-1); break;
                case CalView.Months: _display = _display.AddYears(-1);  break;
                case CalView.Years:  _display = _display.AddYears(-10); break;
            }
            Render();
        }

        private void Next_Click(object s, RoutedEventArgs e)
        {
            switch (_view)
            {
                case CalView.Days:   _display = _display.AddMonths(1); break;
                case CalView.Months: _display = _display.AddYears(1);  break;
                case CalView.Years:  _display = _display.AddYears(10); break;
            }
            Render();
        }

        private void Title_Click(object s, RoutedEventArgs e)
        {
            _view = _view switch
            {
                CalView.Days   => CalView.Months,
                CalView.Months => CalView.Years,
                _              => CalView.Years
            };
            Render();
        }

        private void FooterToggle_Click(object s, RoutedEventArgs e)
        {
            _view = _view == CalView.Time ? CalView.Days : CalView.Time;
            Render();
        }

        private void Ok_Click(object s, RoutedEventArgs e)
        {
            SelectedDateTime = _selected.Date + new TimeSpan(_h, _m, _s);
            DialogResult = true;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  DAYS VIEW
        // ═════════════════════════════════════════════════════════════════════
        private void RenderDays()
        {
            // 8 columns: col 0 = week#, cols 1-7 = days
            ContentHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            for (int i = 0; i < 7; i++)
                ContentHost.ColumnDefinitions.Add(new ColumnDefinition
                    { Width = new GridLength(1, GridUnitType.Star) });

            // 7 rows: row 0 = day-of-week headers, rows 1-6 = calendar weeks
            ContentHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(24) });
            for (int i = 0; i < 6; i++)
                ContentHost.RowDefinitions.Add(new RowDefinition
                    { Height = new GridLength(1, GridUnitType.Star) });

            // ── Day-of-week header row ──────────────────────────────────────
            string[] hdrs = { "#", "Su", "Mo", "Tu", "We", "Th", "Fr", "Sa" };
            for (int c = 0; c < 8; c++)
            {
                var tb = new TextBlock
                {
                    Text                = hdrs[c],
                    FontSize            = 10,
                    FontWeight          = FontWeights.SemiBold,
                    Foreground          = c == 0 ? BrSurf2 : BrSub,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment   = VerticalAlignment.Center
                };
                Grid.SetRow(tb, 0);
                Grid.SetColumn(tb, c);
                ContentHost.Children.Add(tb);
            }

            // ── Calendar cells ──────────────────────────────────────────────
            var today      = DateTime.Today;
            int startDow   = (int)_display.DayOfWeek;          // 0 = Sunday
            var gridStart  = _display.AddDays(-startDow);

            for (int week = 0; week < 6; week++)
            {
                var weekStart = gridStart.AddDays(week * 7);

                // Week number (ISO-ish: use the Thursday of the week)
                int wn = CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
                    weekStart.AddDays(3),
                    CalendarWeekRule.FirstFourDayWeek,
                    DayOfWeek.Monday);

                var wnTb = new TextBlock
                {
                    Text                = wn.ToString(),
                    FontSize            = 9,
                    Foreground          = BrSurf2,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment   = VerticalAlignment.Center
                };
                Grid.SetRow(wnTb, week + 1);
                Grid.SetColumn(wnTb, 0);
                ContentHost.Children.Add(wnTb);

                for (int d = 0; d < 7; d++)
                {
                    var date        = weekStart.AddDays(d);
                    bool isSel      = date == _selected;
                    bool isToday    = date == today;
                    bool otherMonth = date.Month != _display.Month;

                    var cell = BuildDayCell(date, isSel, isToday, otherMonth);
                    Grid.SetRow(cell, week + 1);
                    Grid.SetColumn(cell, d + 1);
                    ContentHost.Children.Add(cell);
                }
            }
        }

        private FrameworkElement BuildDayCell(DateTime date, bool sel, bool today, bool other)
        {
            var root = new Grid { Cursor = Cursors.Hand, Margin = new Thickness(1) };

            var bg = new Border
            {
                CornerRadius = new CornerRadius(5),
                Background   = sel ? BrSelBg : today ? BrTodayBg : BrTrans
            };
            root.Children.Add(bg);

            var tb = new TextBlock
            {
                Text                = date.Day.ToString(),
                FontSize            = 12,
                FontWeight          = sel ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground          = sel ? BrAccent : other ? BrDim : BrTxt,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center
            };
            root.Children.Add(tb);

            // Today triangle (bottom-right corner dot when not selected)
            if (today && !sel)
            {
                var tri = new Polygon
                {
                    Fill                = BrAccent,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment   = VerticalAlignment.Bottom,
                    Margin              = new Thickness(0, 0, 3, 3),
                    Points              = new PointCollection { new(0, 5), new(5, 0), new(5, 5) }
                };
                root.Children.Add(tri);
            }

            // Hover / click
            var captured = date;
            root.MouseEnter += (_, _) =>
            {
                if (!sel) bg.Background = BrHover;
            };
            root.MouseLeave += (_, _) =>
            {
                bg.Background = sel ? BrSelBg : today ? BrTodayBg : BrTrans;
            };
            root.MouseLeftButtonDown += (_, _) =>
            {
                _selected = captured;
                Render();
            };

            return root;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  MONTHS VIEW
        // ═════════════════════════════════════════════════════════════════════
        private void RenderMonths()
        {
            for (int c = 0; c < 4; c++)
                ContentHost.ColumnDefinitions.Add(new ColumnDefinition
                    { Width = new GridLength(1, GridUnitType.Star) });
            for (int r = 0; r < 3; r++)
                ContentHost.RowDefinitions.Add(new RowDefinition
                    { Height = new GridLength(1, GridUnitType.Star) });

            string[] names = { "Jan","Feb","Mar","Apr","May","Jun",
                                "Jul","Aug","Sep","Oct","Nov","Dec" };

            for (int i = 0; i < 12; i++)
            {
                int mo   = i + 1;
                bool sel = _display.Year == _selected.Year && mo == _selected.Month;

                var cell = BuildPickerCell(names[i], sel, false, () =>
                {
                    int days = DateTime.DaysInMonth(_display.Year, mo);
                    _selected = new DateTime(_display.Year, mo, Math.Min(_selected.Day, days));
                    _display  = new DateTime(_display.Year, mo, 1);
                    _view     = CalView.Days;
                    Render();
                });
                Grid.SetRow(cell, i / 4);
                Grid.SetColumn(cell, i % 4);
                ContentHost.Children.Add(cell);
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  YEARS VIEW
        // ═════════════════════════════════════════════════════════════════════
        private void RenderYears()
        {
            for (int c = 0; c < 4; c++)
                ContentHost.ColumnDefinitions.Add(new ColumnDefinition
                    { Width = new GridLength(1, GridUnitType.Star) });
            for (int r = 0; r < 3; r++)
                ContentHost.RowDefinitions.Add(new RowDefinition
                    { Height = new GridLength(1, GridUnitType.Star) });

            int decStart = (_display.Year / 10) * 10;

            for (int i = 0; i < 12; i++)
            {
                int yr       = decStart - 1 + i;          // one before → decade → one after
                bool sel     = yr == _selected.Year;
                bool outside = yr < decStart || yr > decStart + 9;

                var y = yr;
                var cell = BuildPickerCell(yr.ToString(), sel, outside, () =>
                {
                    int days = DateTime.DaysInMonth(y, _selected.Month);
                    _selected = new DateTime(y, _selected.Month, Math.Min(_selected.Day, days));
                    _display  = new DateTime(y, _display.Month, 1);
                    _view     = CalView.Months;
                    Render();
                });
                Grid.SetRow(cell, i / 4);
                Grid.SetColumn(cell, i % 4);
                ContentHost.Children.Add(cell);
            }
        }

        // ── Shared cell builder for Months & Years views ──────────────────────
        private FrameworkElement BuildPickerCell(string label, bool sel, bool dim, Action onClick)
        {
            var root = new Grid { Cursor = Cursors.Hand, Margin = new Thickness(4) };

            var bg = new Border
            {
                CornerRadius = new CornerRadius(6),
                Background   = sel ? BrSelBg : BrTrans
            };
            root.Children.Add(bg);

            var tb = new TextBlock
            {
                Text                = label,
                FontSize            = 13,
                FontWeight          = sel ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground          = sel ? BrAccent : dim ? BrDim : BrTxt,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center
            };
            root.Children.Add(tb);

            root.MouseEnter += (_, _) => { if (!sel) bg.Background = BrHover; };
            root.MouseLeave += (_, _) => { bg.Background = sel ? BrSelBg : BrTrans; };
            root.MouseLeftButtonDown += (_, _) => onClick();

            return root;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  TIME VIEW   (HH : MM : SS spinner)
        // ═════════════════════════════════════════════════════════════════════
        private void RenderTime()
        {
            // 5 columns: H, colon, M, colon, S
            int[] colWidths = { 1, 0, 1, 0, 1 };   // 1 = star, 0 = 22px for colons
            foreach (int w in colWidths)
                ContentHost.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = w == 1
                        ? new GridLength(1, GridUnitType.Star)
                        : new GridLength(22)
                });

            // 3 rows: up-arrow, value, down-arrow
            for (int r = 0; r < 3; r++)
                ContentHost.RowDefinitions.Add(new RowDefinition
                    { Height = new GridLength(1, GridUnitType.Star) });

            // Colon separators at columns 1 and 3
            foreach (int col in new[] { 1, 3 })
            {
                var colon = new TextBlock
                {
                    Text                = ":",
                    FontSize            = 26,
                    FontWeight          = FontWeights.Light,
                    Foreground          = BrSurf2,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment   = VerticalAlignment.Center
                };
                Grid.SetRow(colon, 1);
                Grid.SetColumn(colon, col);
                ContentHost.Children.Add(colon);
            }

            // Time fields
            AddTimeField(col: 0, min: 0, max: 23, getter: () => _h, setter: v => _h = v);
            AddTimeField(col: 2, min: 0, max: 59, getter: () => _m, setter: v => _m = v);
            AddTimeField(col: 4, min: 0, max: 59, getter: () => _s, setter: v => _s = v);
        }

        private void AddTimeField(int col, int min, int max,
                                  Func<int> getter, Action<int> setter)
        {
            // ▲ up button (row 0)
            var up = MakeArrowBtn("▲");
            up.Click += (_, _) =>
            {
                int v = getter(); setter(v >= max ? min : v + 1); Render();
            };
            Grid.SetRow(up, 0); Grid.SetColumn(up, col);
            ContentHost.Children.Add(up);

            // Value label (row 1)
            var val = new TextBlock
            {
                Text                = getter().ToString("D2"),
                FontSize            = 30,
                FontWeight          = FontWeights.Light,
                Foreground          = BrTxt,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center
            };
            Grid.SetRow(val, 1); Grid.SetColumn(val, col);
            ContentHost.Children.Add(val);

            // ▼ down button (row 2)
            var dn = MakeArrowBtn("▼");
            dn.Click += (_, _) =>
            {
                int v = getter(); setter(v <= min ? max : v - 1); Render();
            };
            Grid.SetRow(dn, 2); Grid.SetColumn(dn, col);
            ContentHost.Children.Add(dn);
        }

        private static Button MakeArrowBtn(string txt) => new()
        {
            Content             = txt,
            FontSize            = 14,
            Foreground          = BrSub,
            Background          = Brushes.Transparent,
            BorderThickness     = new Thickness(0),
            Cursor              = Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
            Width               = 46,
            Height              = 34
        };
    }
}
