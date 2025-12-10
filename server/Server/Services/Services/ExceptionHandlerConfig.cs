using log4net;
using Services.Contracts.Enums;
using System;

namespace Services.Services
{
    public class ExceptionHandlerConfig
    {
        public Action<ILog, string, Exception> LogAction { get; }
        public ServiceErrorCode ErrorCode { get; }
        public string ErrorCodeString { get; }
        public Func<Exception, string> MessageFactory { get; }

        public ExceptionHandlerConfig(
            Action<ILog, string, Exception> logAction,
            ServiceErrorCode errorCode,
            string errorCodeString,
            string message)
            : this(logAction, errorCode, errorCodeString, _ => message)
        {
        }

        public ExceptionHandlerConfig(
            Action<ILog, string, Exception> logAction,
            ServiceErrorCode errorCode,
            string errorCodeString,
            Func<Exception, string> messageFactory)
        {
            LogAction = logAction;
            ErrorCode = errorCode;
            ErrorCodeString = errorCodeString;
            MessageFactory = messageFactory;
        }
    }
}
