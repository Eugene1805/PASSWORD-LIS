using PASSWORD_LIS_Client.Services;
using System;
using System.Windows.Data;
using System.Windows.Markup;

namespace PASSWORD_LIS_Client.Utils
{
    public class TranslateExtension : MarkupExtension
    {
        public string Key { get; set; }

        public TranslateExtension(string key)
        {
            Key = key;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            // Creamos un Binding que apunta al indizador del TranslationProvider
            var binding = new Binding($"[{Key}]")
            {
                Source = TranslationProvider.Instance,
                Mode = BindingMode.OneWay
            };
            return binding.ProvideValue(serviceProvider);
        }
    }
}
