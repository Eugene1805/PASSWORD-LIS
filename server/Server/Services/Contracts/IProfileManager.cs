using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace Services.Contracts
{
    [ServiceContract]
    public interface IProfileManager
    {
        [OperationContract]

        bool UpdateAvatar(int playerId, int newPhotoId);
    }
}
