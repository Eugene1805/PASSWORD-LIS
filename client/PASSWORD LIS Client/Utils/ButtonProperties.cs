using System.Windows;

namespace PASSWORD_LIS_Client.Utils
{
    public static class ButtonProperties
    {
        public static readonly DependencyProperty IsBusyProperty =
            DependencyProperty.RegisterAttached(
                "IsBusy",
                typeof(bool),
                typeof(ButtonProperties),
                new PropertyMetadata(false));

        public static void SetIsBusy(DependencyObject element, bool value)
        {
            element.SetValue(IsBusyProperty, value);
        }

        public static bool GetIsBusy(DependencyObject element)
        {
            return (bool)element.GetValue(IsBusyProperty);
        }
    }
}
