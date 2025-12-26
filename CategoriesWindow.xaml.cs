using MoneyFlowWPF.Models;
using MoneyFlowWPF.Services;
using System.Linq;
using System.Windows;

namespace MoneyFlowWPF
{
    public partial class CategoriesWindow : Window
    {
        readonly private DatabaseService _dbService;

        public CategoriesWindow()
        {
            InitializeComponent();
            _dbService = new DatabaseService();
            LoadCategories();
        }

        private void LoadCategories()
        {
            var categories = _dbService.GetAllCategories();
            CategoriesGrid.ItemsSource = categories;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void AddCategory_Click(object sender, RoutedEventArgs e)
        {
            var inputWindow = new InputCategoryWindow();
            if (inputWindow.ShowDialog() == true)
            {
                var category = inputWindow.Category;
                if (string.IsNullOrWhiteSpace(category.Name))
                {
                    MessageBox.Show("Название категории не может быть пустым", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                _dbService.AddCategory(category);
                LoadCategories();
            }
        }

        private void DeleteCategory_Click(object sender, RoutedEventArgs e)
        {
            if (CategoriesGrid.SelectedItem is not Category selectedCategory)
            {
                MessageBox.Show("Выберите категорию для удаления", "Инфо", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Запрещаем удалять категории, используемые в транзакциях
            var transactionCount = _dbService.GetTransactionCountByCategoryId(selectedCategory.Id);
            if (transactionCount > 0)
            {
                MessageBox.Show($"Невозможно удалить категорию \"{selectedCategory.Name}\":\nона используется в {transactionCount} операциях.",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show($"Удалить категорию \"{selectedCategory.Name}\"?", "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                _dbService.DeleteCategory(selectedCategory.Id);
                LoadCategories();
            }
        }
    }
}