using PASSWORD_LIS_Client.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PASSWORD_LIS_Client.ViewModels
{
    public class SettingsViewModel
    {

        private readonly IWindowService windowService;
        public SettingsViewModel(IWindowService windowService) { 
            this.windowService = windowService;

        }
    }
}
