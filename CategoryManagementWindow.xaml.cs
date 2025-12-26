using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CatchCapture.Utilities;
using CatchCapture.Models;

namespace CatchCapture
{
    public partial class CategoryManagementWindow : Window
    {
        public CategoryManagementWindow()
        {
            InitializeComponent();
            LoadCategories();
            this.MouseDown += (s, e) => { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); };
        }

        private void LoadCategories()
        {
            var categories = DatabaseManager.Instance.GetAllCategories();
            CategoriesList.ItemsSource = categories;
        }

        private void BtnAddCategory_Click(object sender, RoutedEventArgs e)
        {
            string name = TxtNewCategory.Text.Trim();
            if (string.IsNullOrEmpty(name)) return;

            // Simple random color logic for new categories
            string[] colors = { "#8E2DE2", "#3498DB", "#2ECC71", "#F1C40F", "#E67E22", "#E74C3C", "#1ABC9C" };
            string color = colors[new Random().Next(colors.Length)];

            DatabaseManager.Instance.InsertCategory(name, color);
            TxtNewCategory.Clear();
            LoadCategories();
        }

        private void BtnDeleteCategory_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is long id)
            {
                if (id == 1)
                {
                    MessageBox.Show("기본 분류는 삭제할 수 없습니다.", "알림");
                    return;
                }

                if (MessageBox.Show("이 분류를 삭제하시겠습니까? 연결된 메모는 '기본' 분류로 이동됩니다.", "확인", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    DatabaseManager.Instance.DeleteCategory(id);
                    LoadCategories();
                }
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
