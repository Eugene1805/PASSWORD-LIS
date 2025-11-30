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
        public static bool TryConvertToTypedFault<T>(FaultException ex, out FaultException<T> typedFault) where T : class
        {
            typedFault = null;
            if (ex == null) return false;

            try
            {
                MessageFault mf = ex.CreateMessageFault();
                if (!mf.HasDetail) return false;

                try
                {
                    T detail = mf.GetDetail<T>();
                    typedFault = new FaultException<T>(detail, ex.Reason);
                    return true;
                }
                catch
                {
                    var reader = mf.GetReaderAtDetailContents();
                    var serializer = new DataContractSerializer(typeof(T));
                    var obj = serializer.ReadObject(reader, verifyObjectName: false);
                    if (obj is T casted)
                    {
                        typedFault = new FaultException<T>(casted, ex.Reason);
                        return true;
                    }
                }
            }
            catch
            {
                // swallow and return false
            }

            return false;
        }


        public static string GetErrorCodeFromFault(FaultException ex)
        {
            if (ex == null) return null;
            
                var mf = ex.CreateMessageFault();
                if (!mf.HasDetail) return null;
                var reader = mf.GetReaderAtDetailContents();
                var xml = reader.ReadOuterXml();
                if (string.IsNullOrWhiteSpace(xml)) return null;

                var x = XElement.Parse(xml);
                var names = new[] { "ErrorCode", "errorCode", "Code", "code" };
                foreach (var n in names)
                {
                    var node = x.Descendants().FirstOrDefault(d => 
                    string.Equals(d.Name.LocalName, n, StringComparison.OrdinalIgnoreCase));
                    if (node != null) return node.Value;
                }
            
            return null;
        }
    
    }
}
