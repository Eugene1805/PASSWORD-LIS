using PASSWORD_LIS_Client.Properties.Langs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace PASSWORD_LIS_Client.Services
{
    public class TranslationProvider : INotifyPropertyChanged
    {
        // Implementación del Singleton
        private static readonly TranslationProvider _instance = new TranslationProvider();
        public static TranslationProvider Instance => _instance;

        // Propiedad para el indizador que notifica los cambios
        public string this[string key] => Lang.ResourceManager.GetString(key, Lang.Culture);

        public event PropertyChangedEventHandler PropertyChanged;

        public void SetLanguage(CultureInfo culture)
        {
            Lang.Culture = culture;
            // Notificamos que "todo" cambió para que la UI se refresque.
            // El nombre "Item[]" es una convención para el indizador.
            OnPropertyChanged(null);
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
