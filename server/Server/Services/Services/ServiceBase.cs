using Data.Exceptions;
using log4net;
using Services.Contracts.DTOs;
using Services.Contracts.Enums;
using Services.Util;
using System;
using System.Configuration;
using System.Data.Entity.Core;
using System.Data.Entity.Infrastructure;
using System.Net.Mail;
using System.Security;
using System.ServiceModel;
using System.Threading.Tasks;

namespace Services.Services
{
    public abstract class ServiceBase
    {
        protected readonly ILog logger;
        private const string DatabaseError = "DATABASE_ERROR";
        protected ServiceBase(ILog log) 
        {
            this.logger = log;
        }

        protected async Task<T> ExecuteAsync<T>(Func<Task<T>> func, string context = null)
        {
            try
            {
                return await func();
            }
            catch (FaultException<ServiceErrorDetailDTO> faultEx)
            {
                logger.WarnFormat("{0} - Service produced a fault: {1}", faultEx, faultEx.Detail?.ErrorCode);
                throw;
            }
            catch (FaultException faultEx)
            {
                logger.WarnFormat("{0} - Service produced a generic FaultException.", faultEx);
                throw;
            }
            catch (Exception ex)
            {
                HandleAndThrow(ex, context, useUpdateMessageForDbUpdate: true);
                throw; 
            }
        }

        protected async Task ExecuteAsync(Func<Task> func, string context = null)
        {
            await ExecuteAsync(async () => { await func(); return true; }, context);
        }

        protected T Execute<T>(Func<T> func, string context = null)
        {
            try
            {
                return func();
            }
            catch (FaultException<ServiceErrorDetailDTO> faultEx)
            {
                logger.WarnFormat("{0} - Service produced a fault: {1}", faultEx, faultEx.Detail?.ErrorCode);
                throw;
            }
            catch (FaultException faultEx)
            {
                logger.WarnFormat("{0} - Service produced a generic FaultException.", faultEx);
                throw;
            }
            catch (Exception ex)
            {
                HandleAndThrow(ex, context, useUpdateMessageForDbUpdate: false);
                throw; 
            }
        }

        private void HandleAndThrow(Exception ex, string context, bool useUpdateMessageForDbUpdate)
        {
            switch (ex)
            {
                case ArgumentNullException _:
                    logger.Error($"{context} - Null argument.", ex);
                    throw FaultExceptionFactory.Create(ServiceErrorCode.NullArgument,
                        "NULL_ARGUMENT", "A null argument was received occurred.");
                case InvalidOperationException _:
                    logger.Error($"{context} - Invalid operation", ex);
                    throw FaultExceptionFactory.Create(
                        ServiceErrorCode.InvalidOperation,
                        "INVALID_OPERATION", ex.Message);
                case DuplicateAccountException dupEx:
                    logger.Warn($"{context} - Duplicate.", dupEx);
                    throw FaultExceptionFactory.Create(ServiceErrorCode.UserAlreadyExists,
                        "USER_ALREADY_EXISTS", dupEx.Message);
                case SecurityException _:
                    logger.Error($"{context} - Security error.", ex);
                    throw FaultExceptionFactory.Create(ServiceErrorCode.SecurityError, 
                        "SECURITY_ERROR", "Could not load Enviroment variables");
                case DbUpdateException _:
                    logger.Error($"{context} - Database update error.", ex);
                    var dbUpdateMsg = useUpdateMessageForDbUpdate
                        ? "An error occurred while processing the update."
                        : "An error occurred while processing the request.";
                    throw FaultExceptionFactory.Create(ServiceErrorCode.DatabaseError,
                        DatabaseError, dbUpdateMsg);
                case ConfigurationErrorsException _:
                    logger.Error($"{context} - Email config error.", ex);
                    throw FaultExceptionFactory.Create(ServiceErrorCode.EmailConfigurationError,
                        "EMAIL_CONFIGURATION_ERROR", "Email service configuration error");
                case FormatException _:
                    logger.Error($"{context} - Email config format.", ex);
                    throw FaultExceptionFactory.Create(ServiceErrorCode.FormatError,
                        "FORMAT_ERROR", "Invalid email service configuration");
                case SmtpException _:
                    logger.Error($"{context} - SMTP error.", ex);
                    throw FaultExceptionFactory.Create(ServiceErrorCode.EmailSendingError,
                        "EMAIL_SENDING_ERROR", "Failed to send email");
                case EntityException _:
                    logger.Error($"{context} - Entity Framework error.", ex);
                    throw FaultExceptionFactory.Create(ServiceErrorCode.DatabaseError,
                        DatabaseError, "An error occurred while processing the request.");
                default:
                    logger.Fatal($"{context} - Unexpected error.", ex);
                    throw FaultExceptionFactory.Create(ServiceErrorCode.UnexpectedError,
                        "UNEXPECTED_ERROR", "An unexpected server error occurred.");
            }
        }
    }
}
