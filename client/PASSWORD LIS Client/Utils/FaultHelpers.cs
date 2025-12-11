using System;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Xml.Linq;

namespace PASSWORD_LIS_Client.Utils
{
    public static class FaultHelpers
    {
        public static bool TryConvertToTypedFault<T>(FaultException faultException, out FaultException<T> typedFault) where T : class
        {
            typedFault = null;
            if (faultException == null)
            {
                return false;
            }

            MessageFault messageFault = faultException.CreateMessageFault();
            if (!messageFault.HasDetail)
            {
                return false;
            }
            try
            {
                T detail = messageFault.GetDetail<T>();
                typedFault = new FaultException<T>(detail, faultException.Reason);
                return true;
            }
            catch
            {
                var reader = messageFault.GetReaderAtDetailContents();
                var serializer = new DataContractSerializer(typeof(T));
                var readObject = serializer.ReadObject(reader, verifyObjectName: false);
                if (readObject is T casted)
                {
                    typedFault = new FaultException<T>(casted, faultException.Reason);
                    return true;
                }
            }
            return false;
        }


        public static string GetErrorCodeFromFault(FaultException faultException)
        {
            if (faultException == null)
            {
                return null;
            } 
            
            var messageFault = faultException.CreateMessageFault();
            if (!messageFault.HasDetail)
            {
                return null;
            } 
            var reader = messageFault.GetReaderAtDetailContents();
            var xml = reader.ReadOuterXml();
            if (string.IsNullOrWhiteSpace(xml))
            { 
                return null; 
            }

            var x = XElement.Parse(xml);
            var names = new[] { "ErrorCode", "errorCode", "Code", "code" };
            foreach (var n in names)
            {
                var node = x.Descendants().FirstOrDefault(d => 
                string.Equals(d.Name.LocalName, n, StringComparison.OrdinalIgnoreCase));
                if (node != null)
                {
                    return node.Value;
                }
            }
            return null;
        }
    
    }
}
