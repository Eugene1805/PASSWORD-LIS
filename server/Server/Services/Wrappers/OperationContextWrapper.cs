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
            return OperationContext.Current.GetCallbackChannel<T>();
        }
    }
}
