using System.Windows;
using System.Windows.Controls;

namespace PASSWORD_LIS_Client.Utils
{
    public static class PasswordBoxHelper
    {
        public static readonly DependencyProperty BoundPasswordProperty =
            DependencyProperty.RegisterAttached("BoundPassword", typeof(string),
                typeof(PasswordBoxHelper), new PropertyMetadata(string.Empty, OnBoundPasswordChanged));

        public static readonly DependencyProperty BindPasswordProperty =
            DependencyProperty.RegisterAttached("BindPassword", typeof(bool),
                typeof(PasswordBoxHelper), new PropertyMetadata(false, OnBindPasswordChanged));

        private static readonly DependencyProperty UpdatingPasswordProperty =
            DependencyProperty.RegisterAttached("UpdatingPassword", typeof(bool),
                typeof(PasswordBoxHelper), new PropertyMetadata(false));


        public static void SetBindPassword(DependencyObject dependencyObject, bool value)
        {
            dependencyObject.SetValue(BindPasswordProperty, value);
        }

        public static bool GetBindPassword(DependencyObject dependencyObject)
        {
            return (bool)dependencyObject.GetValue(BindPasswordProperty);
        }

        public static string GetBoundPassword(DependencyObject dependencyObject)
        {
            return (string)dependencyObject.GetValue(BoundPasswordProperty);
        }

        public static void SetBoundPassword(DependencyObject dependencyObject, string value)
        {
            dependencyObject.SetValue(BoundPasswordProperty, value);
        }

        private static void OnBindPasswordChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            if (!(dependencyObject is PasswordBox passwordBox))
            {
                return;
            }
            bool wasBound = (bool)(e.OldValue);
            bool needToBind = (bool)(e.NewValue);

            if (wasBound)
            {
                passwordBox.PasswordChanged -= HandlePasswordChanged;
            }

            if (needToBind)
            {
                passwordBox.PasswordChanged += HandlePasswordChanged;
            }
        }

        private static void OnBoundPasswordChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs dependencyPropertyChangedEventArgs)
        {
            if (!(dependencyObject is PasswordBox passwordBox))
            {
                return;

            }

            if ((bool)passwordBox.GetValue(UpdatingPasswordProperty))
            {
                return;
            }
            passwordBox.Password = (string)dependencyPropertyChangedEventArgs.NewValue;
        }

        private static void HandlePasswordChanged(object sender, RoutedEventArgs routedEventArgs)
        {
            var passwordBox = sender as PasswordBox;

            passwordBox.SetValue(UpdatingPasswordProperty, true);
            SetBoundPassword(passwordBox, passwordBox.Password);
            passwordBox.SetValue(UpdatingPasswordProperty, false);
        }
    }
}
