using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CatchCapture.Models;

namespace CatchCapture
{
    public partial class PrivacyPolicyWindow : Window
    {
        public PrivacyPolicyWindow()
        {
            InitializeComponent();
            UpdateUIText();
            RenderPolicyForCurrentLanguage();
            LocalizationManager.LanguageChanged += OnLanguageChanged;
        }

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            try { UpdateUIText(); } catch { }
            try { RenderPolicyForCurrentLanguage(); } catch { }
        }

        private void UpdateUIText()
        {
            try
            {
                this.Title = LocalizationManager.Get("PrivacyPolicy");
                if (CloseButton != null) CloseButton.Content = LocalizationManager.Get("Close");
            }
            catch { }
        }

        private void RenderPolicyForCurrentLanguage()
        {
            if (ContentPanel == null) return;
            ContentPanel.Children.Clear();

            var data = PrivacyPolicyContent.Get(LocalizationManager.CurrentLanguage);

            // Title
            ContentPanel.Children.Add(new TextBlock
            {
                Text = data.Title,
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 122, 255)),
                Margin = new Thickness(0, 0, 0, 10)
            });

            // Subtitle
            ContentPanel.Children.Add(new TextBlock
            {
                Text = data.Subtitle,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                Margin = new Thickness(0, 0, 0, 20)
            });

            // Intro
            ContentPanel.Children.Add(new TextBlock
            {
                Text = data.Intro,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 20),
                LineHeight = 24
            });

            // Highlight box
            var highlightBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(255, 243, 205)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(255, 193, 7)),
                BorderThickness = new Thickness(4, 0, 0, 0),
                Padding = new Thickness(20),
                Margin = new Thickness(0, 0, 0, 20),
                CornerRadius = new CornerRadius(4)
            };
            var highlightStack = new StackPanel();
            highlightStack.Children.Add(new TextBlock
            {
                Text = data.HighlightTitle,
                FontWeight = FontWeights.Bold,
                FontSize = 16,
                Foreground = new SolidColorBrush(Color.FromRgb(214, 128, 0)),
                Margin = new Thickness(0, 0, 0, 10)
            });
            highlightStack.Children.Add(new TextBlock
            {
                Text = data.HighlightText,
                TextWrapping = TextWrapping.Wrap,
                FontWeight = FontWeights.Bold,
                LineHeight = 24
            });
            highlightBorder.Child = highlightStack;
            ContentPanel.Children.Add(highlightBorder);

            // Sections
            foreach (var (title, body) in data.Sections)
            {
                ContentPanel.Children.Add(new TextBlock
                {
                    Text = title,
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0, 122, 255)),
                    Margin = new Thickness(0, 0, 0, 10)
                });
                ContentPanel.Children.Add(new TextBlock
                {
                    Text = body,
                    TextWrapping = TextWrapping.Wrap,
                    LineHeight = 24,
                    Margin = new Thickness(0, 0, 0, 20)
                });
            }

            // Contact box
            var contactBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(248, 249, 250)),
                Padding = new Thickness(20),
                Margin = new Thickness(0, 20, 0, 0),
                CornerRadius = new CornerRadius(4)
            };
            var contactStack = new StackPanel();
            contactStack.Children.Add(new TextBlock
            {
                Text = data.ContactTitle,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 122, 255)),
                Margin = new Thickness(0, 0, 0, 10)
            });
            contactStack.Children.Add(new TextBlock
            {
                Text = data.ContactInfo,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 22
            });
            contactBorder.Child = contactStack;
            ContentPanel.Children.Add(contactBorder);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            try { LocalizationManager.LanguageChanged -= OnLanguageChanged; } catch { }
            base.OnClosed(e);
        }
    }
}
