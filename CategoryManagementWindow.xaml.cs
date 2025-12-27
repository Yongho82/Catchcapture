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

            UpdateUIText();
            CatchCapture.Resources.LocalizationManager.LanguageChanged += (s, e) => UpdateUIText();
        }

        private void UpdateUIText()
        {
            this.Title = CatchCapture.Resources.LocalizationManager.GetString("CategoryManagementTitle");
            if (TxtTitle != null) TxtTitle.Text = CatchCapture.Resources.LocalizationManager.GetString("CategoryManagementTitle");
            if (TxtDesc != null) TxtDesc.Text = CatchCapture.Resources.LocalizationManager.GetString("CategoryManagementDesc");
            if (TxtNewCategory != null) TxtNewCategory.Tag = CatchCapture.Resources.LocalizationManager.GetString("NewCategoryPlaceholder");
            if (BtnClose != null) BtnClose.Content = CatchCapture.Resources.LocalizationManager.GetString("Close");
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
                    CatchCapture.CustomMessageBox.Show(CatchCapture.Resources.LocalizationManager.GetString("DefaultCategoryDeleteError"), CatchCapture.Resources.LocalizationManager.GetString("Notice"));
                    return;
                }

                if (CatchCapture.CustomMessageBox.Show(CatchCapture.Resources.LocalizationManager.GetString("DeleteCategoryConfirm"), CatchCapture.Resources.LocalizationManager.GetString("ConfirmDelete"), MessageBoxButton.YesNo) == MessageBoxResult.Yes)
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
