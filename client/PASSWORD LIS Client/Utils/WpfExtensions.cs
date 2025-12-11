using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace PASSWORD_LIS_Client.Utils
{
    public static class WpfExtensions
    {
        public static IEnumerable<T> FindVisualChildren<T>(this DependencyObject dependencyObject) where T : DependencyObject
        {
            if (dependencyObject != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(dependencyObject); i++)
                {
                    DependencyObject dependencyObjectChild = VisualTreeHelper.GetChild(dependencyObject, i);
                    if (dependencyObjectChild is T childType)
                    {
                        yield return childType;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(dependencyObjectChild))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }
    }
}
