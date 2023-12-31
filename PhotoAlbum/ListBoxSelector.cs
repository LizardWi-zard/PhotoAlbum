﻿using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace PhotoAlbum
{
    /// <summary>
    /// Enables the selection inside of a ListBox using a seleciton rectangle.
    /// </summary>
    public sealed class ListBoxSelector
    {
        /// <summary>Identifies the IsEnabled attached property.</summary>
        public static readonly DependencyProperty EnabledProperty =
            DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(ListBoxSelector), new UIPropertyMetadata(false, IsEnabledChangedCallback));

        // This stores the ListBoxSelector for each ListBox so we can unregister it.
        private static readonly Dictionary<ListBox, ListBoxSelector> attachedControls = new Dictionary<ListBox, ListBoxSelector>();

        private readonly ListBox listBox;
        private ScrollContentPresenter scrollContent;

        private SelectionAdorner selectionRect;
        private AutoScroller autoScroller;
        private ItemsControlSelector selector;

        private bool mouseCaptured;
        private Point start;
        private Point end;

        private ListBoxSelector(ListBox listBox)
        {
            this.listBox = listBox;
            if (this.listBox.IsLoaded)
            {
                this.Register();
            }
            else
            {
                // We need to wait for it to be loaded so we can find the
                // child controls.
                this.listBox.Loaded += this.OnListBoxLoaded;
            }
        }

        /// <summary>
        /// Gets the value of the IsEnabled attached property that indicates
        /// whether a selection rectangle can be used to select items or not.
        /// </summary>
        /// <param name="obj">Object on which to get the property.</param>
        /// <returns>
        /// true if items can be selected by a selection rectangle; otherwise, false.
        /// </returns>
        public static bool GetEnabled(DependencyObject obj)
        {
            return (bool)obj.GetValue(EnabledProperty);
        }

        /// <summary>
        /// Sets the value of the IsEnabled attached property that indicates
        /// whether a selection rectangle can be used to select items or not.
        /// </summary>
        /// <param name="obj">Object on which to set the property.</param>
        /// <param name="value">Value to set.</param>
        public static void SetEnabled(DependencyObject obj, bool value)
        {
            obj.SetValue(EnabledProperty, value);
        }

        private static void IsEnabledChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ListBox listBox = d as ListBox;
            if (listBox != null)
            {
                if ((bool)e.NewValue)
                {
                    // If we're enabling selection by a rectangle we can assume
                    // this means we want to be able to select more than one item.
                    if (listBox.SelectionMode == SelectionMode.Single)
                    {
                        listBox.SelectionMode = SelectionMode.Extended;
                    }

                    attachedControls.Add(listBox, new ListBoxSelector(listBox));
                }
                else // Unregister the selector
                {
                    ListBoxSelector selector;
                    if (attachedControls.TryGetValue(listBox, out selector))
                    {
                        attachedControls.Remove(listBox);
                        selector.UnRegister();
                    }
                }
            }
        }

        // Finds the nearest child of the specified type, or null if one wasn't found.
        private static T FindChild<T>(DependencyObject reference) where T : class
        {
            var queue = new Queue<DependencyObject>();
            queue.Enqueue(reference);
            while (queue.Count > 0)
            {
                DependencyObject child = queue.Dequeue();
                T obj = child as T;
                if (obj != null)
                {
                    return obj;
                }

                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(child); i++)
                {
                    queue.Enqueue(VisualTreeHelper.GetChild(child, i));
                }
            }
            return null; 
        }

        private bool Register()
        {
            this.scrollContent = FindChild<ScrollContentPresenter>(this.listBox);
            if (this.scrollContent != null)
            {
                this.autoScroller = new AutoScroller(this.listBox);
                this.autoScroller.OffsetChanged += this.OnOffsetChanged;

                this.selectionRect = new SelectionAdorner(this.scrollContent);
                this.scrollContent.AdornerLayer.Add(this.selectionRect);

                this.selector = new ItemsControlSelector(this.listBox);

                this.listBox.PreviewMouseLeftButtonDown += this.OnPreviewMouseLeftButtonDown;
                this.listBox.MouseLeftButtonUp += this.OnMouseLeftButtonUp;
                this.listBox.MouseMove += this.OnMouseMove;
            }

            return (this.scrollContent != null);
        }

        private void UnRegister()
        {
            this.StopSelection();

            this.listBox.PreviewMouseLeftButtonDown -= this.OnPreviewMouseLeftButtonDown;
            this.listBox.MouseLeftButtonUp -= this.OnMouseLeftButtonUp;
            this.listBox.MouseMove -= this.OnMouseMove;

            this.autoScroller.UnRegister();
        }

        private void OnListBoxLoaded(object sender, EventArgs e)
        {
            if (this.Register())
            {
                this.listBox.Loaded -= this.OnListBoxLoaded;
            }
        }

        private void OnOffsetChanged(object sender, OffsetChangedEventArgs e)
        {
            this.selector.Scroll(e.HorizontalChange, e.VerticalChange);
            this.UpdateSelection();
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (this.mouseCaptured)
            {
                this.mouseCaptured = false;
                this.scrollContent.ReleaseMouseCapture();
                this.StopSelection();
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (this.mouseCaptured)
            {
                // Get the position relative to the content of the ScrollViewer.
                this.end = e.GetPosition(this.scrollContent);
                this.autoScroller.Update(this.end);
                this.UpdateSelection();
            }
        }

        private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Check that the mouse is inside the scroll content (could be on the
            // scroll bars for example).
            Point mouse = e.GetPosition(this.scrollContent);
            if ((mouse.X >= 0) && (mouse.X < this.scrollContent.ActualWidth) &&
                (mouse.Y >= 0) && (mouse.Y < this.scrollContent.ActualHeight))
            {
                this.mouseCaptured = this.TryCaptureMouse(e);
                if (System.Windows.Input.Keyboard.IsKeyDown(Key.LeftCtrl) == false 
                    && System.Windows.Input.Keyboard.IsKeyDown(Key.RightCtrl) == false)
                {
                    this.StartSelection(mouse);
                }
            }
        }

        private bool TryCaptureMouse(MouseButtonEventArgs e)
        {
            Point position = e.GetPosition(this.scrollContent);

            // Check if there is anything under the mouse.
            UIElement element = this.scrollContent.InputHitTest(position) as UIElement;
            if (element != null)
            {
                // Simulate a mouse click by sending it the MouseButtonDown
                // event based on the data we received.
                var args = new MouseButtonEventArgs(e.MouseDevice, e.Timestamp, MouseButton.Left, e.StylusDevice);
                args.RoutedEvent = Mouse.MouseDownEvent;
                args.Source = e.Source;
                element.RaiseEvent(args);

                // The ListBox will try to capture the mouse unless something
                // else captures it.
                if (Mouse.Captured != this.listBox)
                {
                    return false; // Something else wanted the mouse, let it keep it.
                }
            }

            // Either there's nothing under the mouse or the element doesn't want the mouse.
            return this.scrollContent.CaptureMouse();
        }

        private void StopSelection()
        {
            // Hide the selection rectangle and stop the auto scrolling.
            this.selectionRect.IsEnabled = false;
            this.autoScroller.IsEnabled = false;
        }

        private void StartSelection(Point location)
        {
            // We've stolen the MouseLeftButtonDown event from the ListBox
            // so we need to manually give it focus.
            this.listBox.Focus();

            this.start = location;
            this.end = location;

            // Do we need to start a new selection?
            if (((Keyboard.Modifiers & ModifierKeys.Control) == 0) &&
                ((Keyboard.Modifiers & ModifierKeys.Shift) == 0))
            {
                // Neither the shift key or control key is pressed, so
                // clear the selection.
                this.listBox.SelectedItems.Clear();
            }

            this.selector.Reset();
            this.UpdateSelection();

            this.selectionRect.IsEnabled = true;
            this.autoScroller.IsEnabled = true;
        }

        private void UpdateSelection()
        {
            Point start = this.autoScroller.TranslatePoint(this.start);

            double x = Math.Min(start.X, this.end.X);
            double y = Math.Min(start.Y, this.end.Y);
            double width = Math.Abs(this.end.X - start.X);
            double height = Math.Abs(this.end.Y - start.Y);
            Rect area = new Rect(x, y, width, height);
            this.selectionRect.SelectionArea = area;

            Point topLeft = this.scrollContent.TranslatePoint(area.TopLeft, this.listBox);
            Point bottomRight = this.scrollContent.TranslatePoint(area.BottomRight, this.listBox);

            this.selector.UpdateSelection(new Rect(topLeft, bottomRight));
        }

        /// <summary>
        /// Automatically scrolls an ItemsControl when the mouse is dragged outside
        /// of the control.
        /// </summary>
        private sealed class AutoScroller
        {
            private readonly DispatcherTimer autoScroll = new DispatcherTimer();
            private readonly ItemsControl itemsControl;
            private readonly ScrollViewer scrollViewer;
            private readonly ScrollContentPresenter scrollContent;
            private bool isEnabled;
            private Point offset;
            private Point mouse;

            /// <summary>
            /// Initializes a new instance of the AutoScroller class.
            /// </summary>
            /// <param name="itemsControl">The ItemsControl that is scrolled.</param>
            /// <exception cref="ArgumentNullException">itemsControl is null.</exception>
            public AutoScroller(ItemsControl itemsControl)
            {
                if (itemsControl == null)
                {
                    throw new ArgumentNullException("itemsControl");
                }

                this.itemsControl = itemsControl;
                this.scrollViewer = FindChild<ScrollViewer>(itemsControl);
                this.scrollViewer.ScrollChanged += this.OnScrollChanged;
                this.scrollContent = FindChild<ScrollContentPresenter>(this.scrollViewer);

                this.autoScroll.Tick += delegate { this.PreformScroll(); };
                this.autoScroll.Interval = TimeSpan.FromMilliseconds(GetRepeatRate());
            }

            /// <summary>Occurs when the scroll offset has changed.</summary>
            public event EventHandler<OffsetChangedEventArgs> OffsetChanged;

            /// <summary>
            /// Gets or sets a value indicating whether the auto-scroller is enabled
            /// or not.
            /// </summary>
            public bool IsEnabled
            {
                get
                {
                    return this.isEnabled;
                }
                set
                {
                    if (this.isEnabled != value)
                    {
                        this.isEnabled = value;

                        // Reset the auto-scroller and offset.
                        this.autoScroll.IsEnabled = false;
                        this.offset = new Point();
                    }
                }
            }

            /// <summary>
            /// Translates the specified point by the current scroll offset.
            /// </summary>
            /// <param name="point">The point to translate.</param>
            /// <returns>A new point offset by the current scroll amount.</returns>
            public Point TranslatePoint(Point point)
            {
                return new Point(point.X - this.offset.X, point.Y - this.offset.Y);
            }

            /// <summary>
            /// Removes all the event handlers registered on the control.
            /// </summary>
            public void UnRegister()
            {
                this.scrollViewer.ScrollChanged -= this.OnScrollChanged;
            }

            /// <summary>
            /// Updates the location of the mouse and automatically scrolls if required.
            /// </summary>
            /// <param name="mouse">
            /// The location of the mouse, relative to the ScrollViewer's content.
            /// </param>
            public void Update(Point mouse)
            {
                this.mouse = mouse;

                // If scrolling isn't enabled then see if it needs to be.
                if (!this.autoScroll.IsEnabled)
                {
                    this.PreformScroll();
                }
            }

            // Returns the default repeat rate in milliseconds.
            private static int GetRepeatRate()
            {
                // The RepeatButton uses the SystemParameters.KeyboardSpeed as the
                // default value for the Interval property. KeyboardSpeed returns
                // a value between 0 (400ms) and 31 (33ms).
                const double Ratio = (400.0 - 33.0) / 31.0;
                return 400 - (int)(SystemParameters.KeyboardSpeed * Ratio);
            }

            private double CalculateOffset(int startIndex, int endIndex)
            {
                double sum = 0;
                for (int i = startIndex; i != endIndex; i++)
                {
                    FrameworkElement container = this.itemsControl.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
                    if (container != null)
                    {
                        // Height = Actual height + margin
                        sum += container.ActualHeight;
                        sum += container.Margin.Top + container.Margin.Bottom;
                    }
                }
                return sum;
            }

            private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
            {
                if (this.IsEnabled)
                {
                    double horizontal = e.HorizontalChange;
                    double vertical = e.VerticalChange;

                    // VerticalOffset means two seperate things based on the CanContentScroll
                    // property. If this property is true then the offset is the number of
                    // items to scroll; false then it's in Device Independant Pixels (DIPs).
                    if (this.scrollViewer.CanContentScroll)
                    {
                        // We need to either increase the offset or decrease it.
                        if (e.VerticalChange < 0)
                        {
                            int start = (int)e.VerticalOffset;
                            int end = (int)(e.VerticalOffset - e.VerticalChange);
                            vertical = -this.CalculateOffset(start, end);
                        }
                        else
                        {
                            int start = (int)(e.VerticalOffset - e.VerticalChange);
                            int end = (int)e.VerticalOffset;
                            vertical = this.CalculateOffset(start, end);
                        }
                    }

                    this.offset.X += horizontal;
                    this.offset.Y += vertical;

                    var callback = this.OffsetChanged;
                    if (callback != null)
                    {
                        callback(this, new OffsetChangedEventArgs(horizontal, vertical));
                    }
                }
            }

            private void PreformScroll()
            {
                bool scrolled = false;

                if (this.mouse.X > this.scrollContent.ActualWidth)
                {
                    this.scrollViewer.LineRight();
                    scrolled = true;
                }
                else if (this.mouse.X < 0)
                {
                    this.scrollViewer.LineLeft();
                    scrolled = true;
                }

                if (this.mouse.Y > this.scrollContent.ActualHeight)
                {
                    this.scrollViewer.LineDown();
                    scrolled = true;
                }
                else if (this.mouse.Y < 0)
                {
                    this.scrollViewer.LineUp();
                    scrolled = true;
                }

                // It's important to disable scrolling if we're inside the bounds of
                // the control so that when the user does leave the bounds we can
                // re-enable scrolling and it will have the correct initial delay.
                this.autoScroll.IsEnabled = scrolled;
            }
        }

        private sealed class ItemsControlSelector
        {
            private readonly ItemsControl itemsControl;
            private Rect previousArea;

            public ItemsControlSelector(ItemsControl itemsControl)
            {
                if (itemsControl == null)
                {
                    throw new ArgumentNullException("itemsControl");
                }
                this.itemsControl = itemsControl;
            }

            public void Reset()
            {
                this.previousArea = new Rect();
            }

            public void Scroll(double x, double y)
            {
                this.previousArea.Offset(-x, -y);
            }

            public void UpdateSelection(Rect area)
            {
                // Check eack item to see if it intersects with the area.
                for (int i = 0; i < this.itemsControl.Items.Count; i++)
                {
                    FrameworkElement item = this.itemsControl.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
                    if (item != null)
                    {
                        // Get the bounds in the parent's co-ordinates.
                        Point topLeft = item.TranslatePoint(new Point(0, 0), this.itemsControl);
                        Rect itemBounds = new Rect(topLeft.X, topLeft.Y, item.ActualWidth, item.ActualHeight);

                        // Only change the selection if it intersects with the area
                        // (or intersected i.e. we changed the value last time).
                        if (itemBounds.IntersectsWith(area))
                        {
                            Selector.SetIsSelected(item, true);
                        }
                        else if (itemBounds.IntersectsWith(this.previousArea))
                        {
                            // We previously changed the selection to true but it no
                            // longer intersects with the area so clear the selection.
                            Selector.SetIsSelected(item, false);
                        }
                    }
                }
                this.previousArea = area;
            }
        }

        /// <summary>The event data for the AutoScroller.OffsetChanged event.</summary>
        private sealed class OffsetChangedEventArgs : EventArgs
        {
            private readonly double horizontal;
            private readonly double vertical;

            /// <summary>
            /// Initializes a new instance of the OffsetChangedEventArgs class.
            /// </summary>
            /// <param name="horizontal">The change in horizontal scroll.</param>
            /// <param name="vertical">The change in vertical scroll.</param>
            internal OffsetChangedEventArgs(double horizontal, double vertical)
            {
                this.horizontal = horizontal;
                this.vertical = vertical;
            }

            /// <summary>Gets the change in horizontal scroll position.</summary>
            public double HorizontalChange
            {
                get { return this.horizontal; }
            }

            /// <summary>Gets the change in vertical scroll position.</summary>
            public double VerticalChange
            {
                get { return this.vertical; }
            }
        }

        /// <summary>Draws a selection rectangle on an AdornerLayer.</summary>
        private sealed class SelectionAdorner : Adorner
        {
            private Rect selectionRect;

            /// <summary>
            /// Initializes a new instance of the SelectionAdorner class.
            /// </summary>
            /// <param name="parent">
            /// The UIElement which this instance will overlay.
            /// </param>
            /// <exception cref="ArgumentNullException">parent is null.</exception>
            public SelectionAdorner(UIElement parent)
                : base(parent)
            {
                // Make sure the mouse doesn't see us.
                this.IsHitTestVisible = false;

                // We only draw a rectangle when we're enabled.
                this.IsEnabledChanged += delegate { this.InvalidateVisual(); };
            }

            /// <summary>Gets or sets the area of the selection rectangle.</summary>
            public Rect SelectionArea
            {
                get
                {
                    return this.selectionRect;
                }
                set
                {
                    this.selectionRect = value;
                    this.InvalidateVisual();
                }
            }

            /// <summary>
            /// Participates in rendering operations that are directed by the layout system.
            /// </summary>
            /// <param name="drawingContext">The drawing instructions.</param>
            protected override void OnRender(DrawingContext drawingContext)
            {
                base.OnRender(drawingContext);

                if (this.IsEnabled)
                {
                    // Make the lines snap to pixels (add half the pen width [0.5])
                    double[] x = { this.SelectionArea.Left + 0.5, this.SelectionArea.Right + 0.5 };
                    double[] y = { this.SelectionArea.Top + 0.5, this.SelectionArea.Bottom + 0.5 };
                    drawingContext.PushGuidelineSet(new GuidelineSet(x, y));

                    Brush fill = SystemColors.HighlightBrush.Clone();
                    fill.Opacity = 0.4;
                    drawingContext.DrawRectangle(fill, new Pen(SystemColors.HighlightBrush, 1.0), this.SelectionArea);
                }
            }
        }
    }
}