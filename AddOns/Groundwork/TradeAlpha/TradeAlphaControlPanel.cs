using NinjaTrader.Gui.Chart;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class TradeAlpha : Strategy
    {
        private ChartTab _chartTab;
        private Chart _chartWindow;
        private Grid _chartTraderGrid, _mainGrid;
        private bool _panelActive;
        private TabItem _tabItem;
        private TextBlock _rockstarLabel;
        private Button _tradeEnableButton;

        public void InitializeUIManager()
        {
            LoadControlPanel();
        }

        private void LoadControlPanel()
        {
            ChartControl?.Dispatcher.InvokeAsync(CreateWPFControls);
        }

        private void UnloadControlPanel()
        {
            ChartControl?.Dispatcher.InvokeAsync(DisposeWPFControls);
        }

        private void ReadyControlPanel()
        {
            ChartControl?.Dispatcher.InvokeAsync(() => UpdateControlPanelLabel("Rockstar"));
        }

        private void UpdateControlPanelLabel(string text)
        {
            if (_rockstarLabel == null)
                return;

            if (_rockstarLabel.Dispatcher.CheckAccess())
                _rockstarLabel.Text = text;
            else
                _rockstarLabel.Dispatcher.Invoke(() => _rockstarLabel.Text = text);
        }

        private void RefreshToggleUI()
        {
            if (_tradeEnableButton == null) return;

            void SetLook()
            {
                bool enabled = _tradingEnabled;
                _tradeEnableButton.Content = enabled ? "Enabled" : "Disabled";
                _tradeEnableButton.Background = enabled ? Brushes.DarkSeaGreen : Brushes.IndianRed;
                _tradeEnableButton.Foreground = Brushes.White;
            }

            if (_tradeEnableButton.Dispatcher.CheckAccess()) SetLook();
            else _tradeEnableButton.Dispatcher.Invoke(SetLook);
        }

        private void CreateWPFControls()
        {
            _chartWindow = Window.GetWindow(ChartControl?.Parent) as Gui.Chart.Chart;
            if (_chartWindow == null)
                return;

            var chartTrader = _chartWindow.FindFirst("ChartWindowChartTraderControl") as ChartTrader;
            _chartTraderGrid = chartTrader?.Content as Grid;
            if (_chartTraderGrid == null)
                return;

            _mainGrid = new Grid
            {
                Margin = new Thickness(0, 50, 0, 0),
                Background = Brushes.Transparent
            };
            _mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            _rockstarLabel = new TextBlock
            {
                FontFamily = ChartControl.Properties.LabelFont.Family,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Background = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 6),
                Padding = new Thickness(4),
                Text = "Loading..."
            };
            Grid.SetRow(_rockstarLabel, 0);
            _mainGrid.Children.Add(_rockstarLabel);

            _tradeEnableButton = new Button
            {
                Content = "Disabled",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 0, 0),
                Padding = new Thickness(6),
            };
            _tradeEnableButton.Click += RockstarButton_Click;
            Grid.SetRow(_tradeEnableButton, 1);
            _mainGrid.Children.Add(_tradeEnableButton);

            RefreshToggleUI();

            if (TabSelected())
                InsertWPFControls();

            _chartWindow.MainTabControl.SelectionChanged += TabChangedHandler;
        }

        // use centralized handler so UI, logging, and ATM cleanup are consistent
        private void RockstarButton_Click(object sender, RoutedEventArgs e)
        {
            HandleEnabledDisabled(!_tradingEnabled);
        }

        private void DisposeWPFControls()
        {
            if (_chartWindow != null)
                _chartWindow.MainTabControl.SelectionChanged -= TabChangedHandler;

            RemoveWPFControls();

            if (_tradeEnableButton != null)
                _tradeEnableButton.Click -= RockstarButton_Click;

            _rockstarLabel = null;
            _tradeEnableButton = null;
            _mainGrid = null;
            _chartTraderGrid = null;
            _chartWindow = null;
        }

        private void InsertWPFControls()
        {
            if (_panelActive || _mainGrid == null || _chartTraderGrid == null)
                return;

            Grid.SetRow(_mainGrid, _chartTraderGrid.RowDefinitions.Count - 1);
            _chartTraderGrid.Children.Add(_mainGrid);
            _panelActive = true;
        }

        private void RemoveWPFControls()
        {
            if (!_panelActive || _chartTraderGrid == null || _mainGrid == null)
                return;

            _chartTraderGrid.Children.Remove(_mainGrid);
            _panelActive = false;
        }

        private bool TabSelected()
        {
            if (_chartWindow?.MainTabControl?.Items == null || ChartControl == null)
                return false;

            foreach (TabItem tab in _chartWindow.MainTabControl.Items)
            {
                var ct = tab.Content as ChartTab;
                if (ct != null && ct.ChartControl == ChartControl && tab == _chartWindow.MainTabControl.SelectedItem)
                    return true;
            }
            return false;
        }

        private void TabChangedHandler(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count <= 0)
                return;

            _tabItem = e.AddedItems[0] as TabItem;
            if (_tabItem == null)
                return;

            _chartTab = _tabItem.Content as ChartTab;
            if (_chartTab == null)
                return;

            if (TabSelected())
                InsertWPFControls();
            else
                RemoveWPFControls();
        }
    }
}
