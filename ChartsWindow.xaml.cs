using MoneyFlowWPF.Models;
using MoneyFlowWPF.Services;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace MoneyFlowWPF
{
    public partial class ChartsWindow : Window
    {
        readonly private DatabaseService _dbService;
        private List<Transaction> _allTransactions = [];

        public ChartsWindow()
        {
            InitializeComponent();
            _dbService = new DatabaseService();
            LoadData();
            UpdateCharts();
        }

        private void LoadData()
        {
            _allTransactions = _dbService.GetAllTransactions()
                .Where(t => !t.IsInternalTransfer)
                .ToList();
        }

        private void UpdateCharts()
        {
            DateTime startDate = GetStartDate();
            var periodTransactions = _allTransactions
                .Where(t => t.Date >= startDate)
                .ToList();

            if (periodTransactions.Count == 0)
            {
                MessageBox.Show("Нет данных для построения графиков", "Информация");
                return;
            }

            UpdateCategoriesChart(periodTransactions);        // круговая (расходы топ-8)
            UpdateMonthlyChart(periodTransactions);           // баланс по месяцам
            UpdateHistogramChart(periodTransactions);         // гистограмма (BarSeries)
            UpdateMonthlyReportTable(periodTransactions);     // помесячный отчёт
            UpdateIncomeCategoriesTable(periodTransactions);  // ✅ доходы
            UpdateExpenseCategoriesTable(periodTransactions); // ✅ расходы
        }

        private DateTime GetStartDate()
        {
            return PeriodComboBox.SelectedIndex == 0
                ? DateTime.Now.AddMonths(-1)
                : DateTime.MinValue;
        }

        private void UpdateCategoriesChart(List<Transaction> transactions)
        {
            // Берем только расходы для категорий
            var expenses = transactions.Where(t => !t.IsIncome).ToList();

            if (expenses.Count == 0)
            {
                CategoriesPlot.Model = null;
                return;
            }

            // Группируем по категориям
            var categories = expenses
                .GroupBy(t => t.Category)
                .Select(g => new
                {
                    Category = g.Key,
                    Total = g.Sum(t => t.Amount)
                })
                .OrderByDescending(x => x.Total)
                .Take(8) // Топ-8 категорий
                .ToList();

            var plotModel = new PlotModel
            {
                Title = "Расходы по категориям",
                TitleColor = OxyColors.Black
            };

            var pieSeries = new PieSeries
            {
                StrokeThickness = 2,
                InsideLabelPosition = 0.5,
                AngleSpan = 360,
                StartAngle = 0
            };

            // Цвета для категорий
            var colors = new[]
            {
                OxyColors.Red,
                OxyColors.Orange,
                OxyColors.Gold,
                OxyColors.Green,
                OxyColors.Blue,
                OxyColors.Purple,
                OxyColors.Brown,
                OxyColors.Gray
            };

            for (int i = 0; i < categories.Count; i++)
            {
                var category = categories[i];
                var color = colors[i % colors.Length];

                pieSeries.Slices.Add(new PieSlice(
                    category.Category,
                    (double)category.Total)
                {
                    Fill = color,
                    IsExploded = false
                });
            }

            plotModel.Series.Add(pieSeries);
            CategoriesPlot.Model = plotModel;
        }

        private void UpdateMonthlyChart(List<Transaction> transactions)
        {
            // Группируем по месяцам
            var monthlyData = transactions
                .GroupBy(t => new { t.Date.Year, t.Date.Month })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    Balance = g.Where(t => t.IsIncome).Sum(t => t.Amount) -
                             g.Where(t => !t.IsIncome).Sum(t => t.Amount)
                })
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .ToList();

            if (monthlyData.Count == 0) return;

            var plotModel = new PlotModel
            {
                Title = "Баланс по месяцам",
                TitleColor = OxyColors.Black
            };

            // Линейный график баланса
            var lineSeries = new LineSeries
            {
                Title = "Баланс",
                Color = OxyColors.Blue,
                MarkerType = MarkerType.Circle,
                MarkerSize = 5,
                MarkerFill = OxyColors.White,
                MarkerStroke = OxyColors.Blue,
                MarkerStrokeThickness = 2
            };

            // Ось X - месяцы как категории
            var labels = new List<string>();

            // Ось X - месяцы
            var categoryAxis = new CategoryAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Месяц",
                Angle = 45
            };

            // Ось Y - суммы
            var valueAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Баланс (₽)"
            };

            plotModel.Axes.Add(categoryAxis);
            plotModel.Axes.Add(valueAxis);

            // Добавляем данные
            foreach (var month in monthlyData)
            {
                string label = $"{month.Month:00}.{month.Year % 100}";
                categoryAxis.Labels.Add(label);
                lineSeries.Points.Add(new DataPoint(categoryAxis.Labels.Count - 1, (double)month.Balance));
            }

            plotModel.Series.Add(lineSeries);
            MonthlyPlot.Model = plotModel;
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
            UpdateCharts();
        }
        private void UpdateHistogramChart(List<Transaction> transactions)
        {
            var expenses = transactions.Where(t => !t.IsIncome).ToList();
            if (expenses.Count == 0)
            {
                HistogramPlot.Model = null;
                return;
            }

            var categories = expenses
                .GroupBy(t => t.Category)
                .Select(g => new { Name = g.Key, Total = g.Sum(t => t.Amount) })
                .OrderByDescending(x => x.Total)
                .ToList();

            if (categories.Count == 0)
            {
                HistogramPlot.Model = null;
                return;
            }

            var plotModel = new PlotModel
            {
                Title = "Расходы по категориям",
                TitleColor = OxyColors.Black,
                Background = OxyColors.White,
                PlotAreaBackground = OxyColors.White
            };

            // Ось Y — категории
            var categoryAxis = new CategoryAxis
            {
                Position = AxisPosition.Left,
                Title = "Категория",
                Angle = 0,
                FontSize = 10
            };

            // Ось X — суммы
            var valueAxis = new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Сумма (₽)",
                MinimumPadding = 0.05
            };

            plotModel.Axes.Add(categoryAxis);
            plotModel.Axes.Add(valueAxis);

            // Горизонтальные столбцы
            var barSeries = new BarSeries
            {
                LabelFormatString = "{0:0} ₽",
                LabelPlacement = LabelPlacement.Outside,
                FontSize = 10,
                FillColor = OxyColors.SteelBlue
            };

            foreach (var cat in categories)
            {
                categoryAxis.Labels.Add(cat.Name);
                barSeries.Items.Add(new BarItem { Value = (double)cat.Total });
            }

            plotModel.Series.Add(barSeries);
            HistogramPlot.Model = plotModel;
        }
        private void UpdateMonthlyReportTable(List<Transaction> transactions)
        {
            var monthlyData = transactions
                .GroupBy(t => new { t.Date.Year, t.Date.Month })
                .Select(g => new MonthlyReportItem
                {
                    MonthLabel = $"{g.Key.Month:00}.{g.Key.Year}",
                    Income = g.Where(t => t.IsIncome).Sum(t => t.Amount),
                    Expense = g.Where(t => !t.IsIncome).Sum(t => t.Amount)
                })
                .OrderBy(x => x.MonthLabel)
                .ToList();

            // Считаем итоги
            var totalIncome = monthlyData.Sum(x => x.Income);
            var totalExpense = monthlyData.Sum(x => x.Expense);

            // Добавляем итоговую строку
            monthlyData.Add(new MonthlyReportItem
            {
                MonthLabel = "Итого за период",
                Income = totalIncome,
                Expense = totalExpense,
                IsTotalRow = true
            });

            MonthlyReportGrid.ItemsSource = monthlyData;
        }
        private void UpdateIncomeCategoriesTable(List<Transaction> transactions)
        {
            var incomes = transactions.Where(t => t.IsIncome).ToList();
            if (incomes.Count == 0)
            {
                IncomeCategoriesGrid.ItemsSource = null;
                return;
            }

            var totalIncome = incomes.Sum(t => t.Amount);
            var reportData = incomes
                .GroupBy(t => t.Category)
                .Select(g => new CategoryReportItem
                {
                    CategoryName = g.Key,
                    Amount = g.Sum(t => t.Amount),
                    Percent = totalIncome > 0 ? (g.Sum(t => t.Amount) / totalIncome) * 100m : 0
                })
                .OrderByDescending(x => x.Amount)
                .ToList();

            IncomeCategoriesGrid.ItemsSource = reportData;
        }

        private void UpdateExpenseCategoriesTable(List<Transaction> transactions)
        {
            var expenses = transactions.Where(t => !t.IsIncome).ToList();
            if (expenses.Count == 0)
            {
                ExpenseCategoriesGrid.ItemsSource = null;
                return;
            }

            var totalExpense = expenses.Sum(t => t.Amount);
            var reportData = expenses
                .GroupBy(t => t.Category)
                .Select(g => new CategoryReportItem
                {
                    CategoryName = g.Key,
                    Amount = g.Sum(t => t.Amount),
                    Percent = totalExpense > 0 ? (g.Sum(t => t.Amount) / totalExpense) * 100m : 0
                })
                .OrderByDescending(x => x.Amount)
                .ToList();

            ExpenseCategoriesGrid.ItemsSource = reportData;
        }
    }
    public class MonthlyReportItem
    {
        public string MonthLabel { get; set; } = "";
        public decimal Income { get; set; }
        public decimal Expense { get; set; }
        public decimal Balance => Income - Expense;
        public bool IsPositiveBalance => Balance >= 0;
        public bool IsTotalRow { get; set; } = false; // ← новое
    }
    public class CategoryReportItem
    {
        public string CategoryName { get; set; } = "";
        public decimal Amount { get; set; }
        public decimal Percent { get; set; }
    }
}