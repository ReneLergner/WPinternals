// Copyright (c) 2018, Rene Lergner - @Heathcliff74xda
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
//
// Some of the classes and functions in this file were found online.
// Where possible the original authors are referenced.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace WPinternals.HelperClasses
{
    // This class was written by: Rolf Wessels
    // https://github.com/rolfwessels/lazycowprojects/tree/master/Wpf

    /// <summary>
    /// Static class used to attach to wpf control
    /// </summary>
    public static class GridViewColumnResize
    {
        #region DependencyProperties

        public static readonly DependencyProperty WidthProperty =
            DependencyProperty.RegisterAttached("Width", typeof(string), typeof(GridViewColumnResize),
                                                new PropertyMetadata(OnSetWidthCallback));

        public static readonly DependencyProperty GridViewColumnResizeBehaviorProperty =
            DependencyProperty.RegisterAttached("GridViewColumnResizeBehavior",
                                                typeof(GridViewColumnResizeBehavior), typeof(GridViewColumnResize),
                                                null);

        public static readonly DependencyProperty EnabledProperty =
            DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(GridViewColumnResize),
                                                new PropertyMetadata(OnSetEnabledCallback));

        public static readonly DependencyProperty ListViewResizeBehaviorProperty =
            DependencyProperty.RegisterAttached("ListViewResizeBehaviorProperty",
                                                typeof(ListViewResizeBehavior), typeof(GridViewColumnResize), null);

        #endregion

        public static string GetWidth(DependencyObject obj)
        {
            return (string)obj.GetValue(WidthProperty);
        }

        public static void SetWidth(DependencyObject obj, string value)
        {
            obj.SetValue(WidthProperty, value);
        }

        public static bool GetEnabled(DependencyObject obj)
        {
            return (bool)obj.GetValue(EnabledProperty);
        }

        public static void SetEnabled(DependencyObject obj, bool value)
        {
            obj.SetValue(EnabledProperty, value);
        }

        #region CallBack

        private static void OnSetWidthCallback(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            if (dependencyObject is GridViewColumn element)
            {
                GridViewColumnResizeBehavior behavior = GetOrCreateBehavior(element);
                behavior.Width = e.NewValue as string;
            }
            else
            {
                Console.Error.WriteLine("Error: Expected type GridViewColumn but found " +
                                        dependencyObject.GetType().Name);
            }
        }

        private static void OnSetEnabledCallback(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            if (dependencyObject is ListView element)
            {
                ListViewResizeBehavior behavior = GetOrCreateBehavior(element);
                behavior.Enabled = (bool)e.NewValue;
            }
            else
            {
                Console.Error.WriteLine("Error: Expected type ListView but found " + dependencyObject.GetType().Name);
            }
        }

        private static ListViewResizeBehavior GetOrCreateBehavior(ListView element)
        {
            if (element.GetValue(GridViewColumnResizeBehaviorProperty) is not ListViewResizeBehavior behavior)
            {
                behavior = new ListViewResizeBehavior(element);
                element.SetValue(ListViewResizeBehaviorProperty, behavior);
            }

            return behavior;
        }

        private static GridViewColumnResizeBehavior GetOrCreateBehavior(GridViewColumn element)
        {
            if (element.GetValue(GridViewColumnResizeBehaviorProperty) is not GridViewColumnResizeBehavior behavior)
            {
                behavior = new GridViewColumnResizeBehavior(element);
                element.SetValue(GridViewColumnResizeBehaviorProperty, behavior);
            }

            return behavior;
        }

        #endregion

        #region Nested type: GridViewColumnResizeBehavior

        // This class was written by: Rolf Wessels
        // https://github.com/rolfwessels/lazycowprojects/tree/master/Wpf

        /// <summary>
        /// GridViewColumn class that gets attached to the GridViewColumn control
        /// </summary>
        public class GridViewColumnResizeBehavior
        {
            private readonly GridViewColumn _element;

            public GridViewColumnResizeBehavior(GridViewColumn element)
            {
                _element = element;
            }

            public string Width
            {
                get; set;
            }

            public bool IsStatic
            {
                get
                {
                    return StaticWidth >= 0;
                }
            }

            public double StaticWidth
            {
                get
                {
                    return double.TryParse(Width, out double result) ? result : -1;
                }
            }

            public double Percentage
            {
                get
                {
                    if (!IsStatic)
                    {
                        return Mulitplier * 100;
                    }
                    return 0;
                }
            }

            public double Mulitplier
            {
                get
                {
                    if (Width == "*" || Width == "1*")
                    {
                        return 1;
                    }

                    if (Width.EndsWith("*") && double.TryParse(Width[0..^1], out double perc))
                    {
                        return perc;
                    }
                    return 1;
                }
            }

            public void SetWidth(double allowedSpace, double totalPercentage)
            {
                if (IsStatic)
                {
                    _element.Width = StaticWidth;
                }
                else
                {
                    _element.Width = (double)Math.Max(allowedSpace * (Percentage / totalPercentage), 0);
                }
            }
        }

        #endregion

        #region Nested type: ListViewResizeBehavior

        // This class was written by: Rolf Wessels
        // https://github.com/rolfwessels/lazycowprojects/tree/master/Wpf

        /// <summary>
        /// ListViewResizeBehavior class that gets attached to the ListView control
        /// </summary>
        public class ListViewResizeBehavior
        {
            private const int Margin = 25;
            private const long RefreshTime = Timeout.Infinite;
            private const long Delay = 500;

            private readonly ListView _element;
            private readonly Timer _timer;

            public ListViewResizeBehavior(ListView element)
            {
                _element = element ?? throw new ArgumentNullException(nameof(element));
                element.Loaded += OnLoaded;

                // Action for resizing and re-enable the size lookup
                // This stops the columns from constantly resizing to improve performance
                Action resizeAndEnableSize = () =>
                {
                    Resize();
                    _element.SizeChanged += OnSizeChanged;
                };
                _timer = new Timer(x => Application.Current.Dispatcher.BeginInvoke(resizeAndEnableSize), null, Delay,
                                   RefreshTime);
            }

            public bool Enabled
            {
                get; set;
            }

            private void OnLoaded(object sender, RoutedEventArgs e)
            {
                _element.SizeChanged += OnSizeChanged;
            }

            private void OnSizeChanged(object sender, SizeChangedEventArgs e)
            {
                if (e.WidthChanged)
                {
                    _element.SizeChanged -= OnSizeChanged;
                    _timer.Change(Delay, RefreshTime);
                }
            }

            private void Resize()
            {
                if (Enabled)
                {
                    double totalWidth = _element.ActualWidth;
                    if (_element.View is GridView gv)
                    {
                        double allowedSpace = totalWidth - GetAllocatedSpace(gv);
                        allowedSpace -= Margin;
                        double totalPercentage = GridViewColumnResizeBehaviors(gv).Sum(x => x.Percentage);
                        foreach (GridViewColumnResizeBehavior behavior in GridViewColumnResizeBehaviors(gv))
                        {
                            behavior.SetWidth(allowedSpace, totalPercentage);
                        }
                    }
                }
            }

            private static IEnumerable<GridViewColumnResizeBehavior> GridViewColumnResizeBehaviors(GridView gv)
            {
                foreach (GridViewColumn t in gv.Columns)
                {
                    if (t.GetValue(GridViewColumnResizeBehaviorProperty) is GridViewColumnResizeBehavior gridViewColumnResizeBehavior)
                    {
                        yield return gridViewColumnResizeBehavior;
                    }
                }
            }

            private static double GetAllocatedSpace(GridView gv)
            {
                double totalWidth = 0;
                foreach (GridViewColumn t in gv.Columns)
                {
                    if (t.GetValue(GridViewColumnResizeBehaviorProperty) is GridViewColumnResizeBehavior gridViewColumnResizeBehavior)
                    {
                        if (gridViewColumnResizeBehavior.IsStatic)
                        {
                            totalWidth += gridViewColumnResizeBehavior.StaticWidth;
                        }
                    }
                    else
                    {
                        totalWidth += t.ActualWidth;
                    }
                }
                return totalWidth;
            }
        }

        #endregion
    }
}
