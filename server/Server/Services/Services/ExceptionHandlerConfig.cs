using log4net;
using Services.Contracts.Enums;
using System;

namespace Services.Services
{
    public class ExceptionHandlerConfig
    {
        public Action<ILog, string, Exception> LogAction 
        { 
            get;
        }
        public ServiceErrorCode ErrorCode 
        { 
            get;
        }
        public string ErrorCodeString 
        { 
            get;
        }
        public Func<Exception, string> MessageFactory 
        { 
            get;
        }

        public ExceptionHandlerConfig(
            Action<ILog, string, Exception> LogAction,
            ServiceErrorCode ErrorCode,
            string ErrorCodeString,
            string Message)
            : this(LogAction, ErrorCode, ErrorCodeString, _ => Message)
        {
        }

        public ExceptionHandlerConfig(
            Action<ILog, string, Exception> LogAction,
            ServiceErrorCode ErrorCode,
            string ErrorCodeString,
            Func<Exception, string> MessageFactory)
        {
            this.LogAction = LogAction;
            this.ErrorCode = ErrorCode;
            this.ErrorCodeString = ErrorCodeString;
            this.MessageFactory = MessageFactory;
        }
    }
}
