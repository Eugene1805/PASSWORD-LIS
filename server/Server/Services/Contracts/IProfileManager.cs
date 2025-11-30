using Services.Contracts.DTOs;
using System.ServiceModel;
using System.Threading.Tasks;

namespace Services.Contracts
{
    /// <summary>
    /// Provides operations to update and manage a user's profile data.
    /// </summary>
    [ServiceContract]
    public interface IProfileManager
    {
        /// <summary>
        /// Updates a user's profile with the provided data.
        /// </summary>
        /// <param name="updatedProfileData">The updated profile information.</param>
        /// <returns>The persisted updated user profile.</returns>
        [OperationContract]
        [FaultContract(typeof(ServiceErrorDetailDTO))]
        Task<UserDTO> UpdateProfileAsync(UserDTO updatedProfileData);
    }
}
