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
            // Return default(T) when there is no current OperationContext to avoid NRE
            var current = OperationContext.Current;
            return current != null ? current.GetCallbackChannel<T>() : default;
        }
    }
}
