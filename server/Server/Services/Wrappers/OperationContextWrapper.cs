using System.ServiceModel;

namespace Services.Wrappers
{
    public interface IOperationContextWrapper
    {
        T GetCallbackChannel<T>();
    }
    public class OperationContextWrapper : IOperationContextWrapper
    {
        public T GetCallbackChannel<T>()
        {
            // Esta clase es la única que habla con el código estático de WCF
            return OperationContext.Current.GetCallbackChannel<T>();
        }
    }
}
