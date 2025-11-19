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


        public static void SetBindPassword(DependencyObject dp, bool value)
        {
            dp.SetValue(BindPasswordProperty, value);
        }

        public static bool GetBindPassword(DependencyObject dp)
        {
            return (bool)dp.GetValue(BindPasswordProperty);
        }

        public static string GetBoundPassword(DependencyObject dp)
        {
            return (string)dp.GetValue(BoundPasswordProperty);
        }

        public static void SetBoundPassword(DependencyObject dp, string value)
        {
            dp.SetValue(BoundPasswordProperty, value);
        }

        private static void OnBindPasswordChanged(DependencyObject dp, DependencyPropertyChangedEventArgs e)
        {
            if (!(dp is PasswordBox box))
                return;

            bool wasBound = (bool)(e.OldValue);
            bool needToBind = (bool)(e.NewValue);

            if (wasBound)
            {
                box.PasswordChanged -= HandlePasswordChanged;
            }

            if (needToBind)
            {
                box.PasswordChanged += HandlePasswordChanged;
            }
        }

        private static void OnBoundPasswordChanged(DependencyObject dp, DependencyPropertyChangedEventArgs e)
        {
            if (!(dp is PasswordBox box))
                return;

            // Avoid recursive updates
            if ((bool)box.GetValue(UpdatingPasswordProperty)) return;

            box.Password = (string)e.NewValue;
        }

        private static void HandlePasswordChanged(object sender, RoutedEventArgs e)
        {
            var box = sender as PasswordBox;

            // Set a flag to indicate that we are updating the password
            box.SetValue(UpdatingPasswordProperty, true);
            SetBoundPassword(box, box.Password);
            box.SetValue(UpdatingPasswordProperty, false);
        }
    }
}
