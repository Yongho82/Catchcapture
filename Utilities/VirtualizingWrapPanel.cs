using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace CatchCapture.Utilities
{
    /// <summary>
    /// A virtualizing panel that provides wrap layout behavior.
    /// This is a simplified implementation suited for items with fixed size.
    /// </summary>
    public class VirtualizingWrapPanel : VirtualizingPanel, IScrollInfo
    {
        private Size _extent = new Size(0, 0);
        private Size _viewport = new Size(0, 0);
        private Point _offset = new Point(0, 0);

        public static readonly DependencyProperty ItemWidthProperty =
            DependencyProperty.Register("ItemWidth", typeof(double), typeof(VirtualizingWrapPanel), new FrameworkPropertyMetadata(200.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty ItemHeightProperty =
            DependencyProperty.Register("ItemHeight", typeof(double), typeof(VirtualizingWrapPanel), new FrameworkPropertyMetadata(250.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public double ItemWidth
        {
            get => (double)GetValue(ItemWidthProperty);
            set => SetValue(ItemWidthProperty, value);
        }

        public double ItemHeight
        {
            get => (double)GetValue(ItemHeightProperty);
            set => SetValue(ItemHeightProperty, value);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            try
            {
                UpdateScrollInfo(availableSize);

                ItemsControl itemsControl = ItemsControl.GetItemsOwner(this);
                if (itemsControl == null) return new Size(0, 0);

                int itemCount = itemsControl.Items.Count;
                if (itemCount == 0)
                {
                    CleanUpItems(-1, -1);
                    return new Size(0, 0);
                }

                int itemsPerRow = GetItemsPerRow();

                // Determine visible range
                // Use a small buffer to avoid flickering
                int firstVisibleItemIndex = (int)(_offset.Y / ItemHeight) * itemsPerRow;
                int lastVisibleItemIndex = (int)((_offset.Y + _viewport.Height) / ItemHeight + 1) * itemsPerRow + itemsPerRow - 1;

                // Clamp indices
                if (firstVisibleItemIndex < 0) firstVisibleItemIndex = 0;
                if (firstVisibleItemIndex >= itemCount) firstVisibleItemIndex = itemCount - 1;
                
                if (lastVisibleItemIndex >= itemCount) lastVisibleItemIndex = itemCount - 1;
                if (lastVisibleItemIndex < firstVisibleItemIndex) lastVisibleItemIndex = firstVisibleItemIndex;

                IItemContainerGenerator generator = ItemContainerGenerator;
                GeneratorPosition startPos = generator.GeneratorPositionFromIndex(firstVisibleItemIndex);
                int childIndex = (startPos.Offset == 0) ? startPos.Index : startPos.Index + 1;

                using (generator.StartAt(startPos, GeneratorDirection.Forward, true))
                {
                    for (int i = firstVisibleItemIndex; i <= lastVisibleItemIndex; ++i)
                    {
                        bool isNewlyRealized;
                        UIElement? element = generator.GenerateNext(out isNewlyRealized) as UIElement;
                        if (element == null) continue;

                        if (isNewlyRealized)
                        {
                            if (childIndex >= InternalChildren.Count)
                                AddInternalChild(element);
                            else
                                InsertInternalChild(childIndex, element);
                            generator.PrepareItemContainer(element);
                        }

                        element.Measure(new Size(ItemWidth, ItemHeight));
                        childIndex++;
                    }
                }

                CleanUpItems(firstVisibleItemIndex, lastVisibleItemIndex);

                // Return a finite size to avoid collapsing
                return new Size(
                    double.IsInfinity(availableSize.Width) ? (itemsPerRow * ItemWidth) : availableSize.Width,
                    double.IsInfinity(availableSize.Height) ? _viewport.Height : availableSize.Height);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"VirtualizingWrapPanel Measure Error: {ex.Message}");
                return new Size(0, 0);
            }
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            UpdateScrollInfo(finalSize);

            IItemContainerGenerator generator = ItemContainerGenerator;
            int itemsPerRow = GetItemsPerRow();

            for (int i = 0; i < InternalChildren.Count; i++)
            {
                UIElement child = InternalChildren[i];
                int itemIndex = generator.IndexFromGeneratorPosition(new GeneratorPosition(i, 0));

                int row = itemIndex / itemsPerRow;
                int col = itemIndex % itemsPerRow;

                double x = col * ItemWidth;
                double y = row * ItemHeight - _offset.Y;

                child.Arrange(new Rect(x, y, ItemWidth, ItemHeight));
            }

            return finalSize;
        }

        private void CleanUpItems(int minIndex, int maxIndex)
        {
            UIElementCollection children = InternalChildren;
            IItemContainerGenerator generator = ItemContainerGenerator;

            for (int i = children.Count - 1; i >= 0; i--)
            {
                GeneratorPosition pos = new GeneratorPosition(i, 0);
                int itemIndex = generator.IndexFromGeneratorPosition(pos);
                if (itemIndex < minIndex || itemIndex > maxIndex)
                {
                    generator.Remove(pos, 1);
                    RemoveInternalChildRange(i, 1);
                }
            }
        }

        private int GetItemsPerRow()
        {
            int count = (int)(_viewport.Width / ItemWidth);
            return count > 0 ? count : 1;
        }

        private void UpdateScrollInfo(Size availableSize)
        {
            ItemsControl itemsControl = ItemsControl.GetItemsOwner(this);
            if (itemsControl == null) return;

            Size viewport = availableSize;

            // Handle infinite constraints by looking at actual size or parent size
            if (double.IsInfinity(viewport.Width))
            {
                if (ActualWidth > 0) viewport.Width = ActualWidth;
                else if (Application.Current?.MainWindow != null) viewport.Width = Application.Current.MainWindow.ActualWidth;
                else viewport.Width = 800; // Final fallback
            }

            if (double.IsInfinity(viewport.Height))
            {
                if (ActualHeight > 0) viewport.Height = ActualHeight;
                else if (Application.Current?.MainWindow != null) viewport.Height = Application.Current.MainWindow.ActualHeight;
                else viewport.Height = 600; // Final fallback
            }

            int itemCount = itemsControl.Items.Count;
            int itemsPerRow = (int)(viewport.Width / ItemWidth);
            if (itemsPerRow <= 0) itemsPerRow = 1;

            int rowCount = (int)Math.Ceiling((double)itemCount / itemsPerRow);
            Size extent = new Size(itemsPerRow * ItemWidth, rowCount * ItemHeight);

            if (extent != _extent || viewport != _viewport)
            {
                _extent = extent;
                _viewport = viewport;

                ScrollOwner?.InvalidateScrollInfo();
            }
        }

        protected override void OnItemsChanged(object sender, ItemsChangedEventArgs args)
        {
            base.OnItemsChanged(sender, args);
            // Invalidate layout when items change
            _offset = new Point(0, 0);
            InvalidateMeasure();
        }

        // IScrollInfo implementation
        public ScrollViewer? ScrollOwner { get; set; }
        public bool CanHorizontallyScroll { get; set; }
        public bool CanVerticallyScroll { get; set; }
        public double ExtentWidth => _extent.Width;
        public double ExtentHeight => _extent.Height;
        public double ViewportWidth => _viewport.Width;
        public double ViewportHeight => _viewport.Height;
        public double HorizontalOffset => _offset.X;
        public double VerticalOffset => _offset.Y;

        public void LineDown() => SetVerticalOffset(VerticalOffset + 10);
        public void LineUp() => SetVerticalOffset(VerticalOffset - 10);
        public void LineLeft() => SetHorizontalOffset(HorizontalOffset - 10);
        public void LineRight() => SetHorizontalOffset(HorizontalOffset + 10);
        public void PageDown() => SetVerticalOffset(VerticalOffset + _viewport.Height);
        public void PageUp() => SetVerticalOffset(VerticalOffset - _viewport.Height);
        public void PageLeft() => SetHorizontalOffset(HorizontalOffset - _viewport.Width);
        public void PageRight() => SetHorizontalOffset(HorizontalOffset + _viewport.Width);
        public void MouseWheelDown() => SetVerticalOffset(VerticalOffset + 30);
        public void MouseWheelUp() => SetVerticalOffset(VerticalOffset - 30);
        public void MouseWheelLeft() => SetHorizontalOffset(HorizontalOffset - 30);
        public void MouseWheelRight() => SetHorizontalOffset(HorizontalOffset + 30);

        public void SetHorizontalOffset(double offset) { /* Horizontal scroll not needed for wrap panel generally */ }

        public void SetVerticalOffset(double offset)
        {
            if (offset < 0 || _viewport.Height >= _extent.Height) offset = 0;
            else if (offset + _viewport.Height >= _extent.Height) offset = _extent.Height - _viewport.Height;

            _offset.Y = offset;
            ScrollOwner?.InvalidateScrollInfo();
            InvalidateMeasure();
        }

        public Rect MakeVisible(Visual visual, Rect rectangle) => new Rect();
    }
}
