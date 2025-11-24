using Data.Exceptions;
using log4net;
using Services.Contracts.DTOs;
using Services.Contracts.Enums;
using Services.Util;
using System;
using System.Configuration;
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
            catch (ArgumentNullException ex)
            {
                logger.Error($"{context} - Null argument.", ex);
                throw FaultExceptionFactory.Create(ServiceErrorCode.NullArgument,
                    "NULL_ARGUMENT", "A null argument was received occurred.");
            }
            catch (InvalidOperationException ex)
            {
                logger.Error($"{context } - Invalid operation", ex);
                throw FaultExceptionFactory.Create(
                    ServiceErrorCode.InvalidOperation,
                    "INVALID_OPERATION",ex.Message);
            }
            catch (DuplicateAccountException ex) 
            {
                logger.Warn($"{context} - Duplicate.", ex);
                throw FaultExceptionFactory.Create(ServiceErrorCode.UserAlreadyExists,
                    "USER_ALREADY_EXISTS", ex.Message);
            }
            catch (SecurityException ex)
            {
                logger.Error($"{context} - Security error.", ex);
                throw FaultExceptionFactory.Create(ServiceErrorCode.SecurityError, 
                    "SECURITY_ERROR", "Could not load Enviroment variables");
            }
            catch (DbUpdateException ex)
            {
                logger.Error($"{context} - Database update error.", ex);
                throw FaultExceptionFactory.Create(ServiceErrorCode.DatabaseError,
                    "DATABASE_ERROR", "An error occurred while processing the request.");
            }
            catch (ConfigurationErrorsException ex)
            {
                logger.Error($"{context} - Email config error.", ex);
                throw FaultExceptionFactory.Create(ServiceErrorCode.EmailConfigurationError,
                    "EMAIL_CONFIGURATION_ERROR", "Email service configuration error");
            }
            catch (FormatException ex)
            {
                logger.Error($"{context} - Email config format.", ex);
                throw FaultExceptionFactory.Create(ServiceErrorCode.FormatError,
                    "FORMAT_ERROR", "Invalid email service configuration");
            }
            catch (SmtpException ex)
            {
                logger.Error($"{context} - SMTP error.", ex);
                throw FaultExceptionFactory.Create(ServiceErrorCode.EmailSendingError,
                    "EMAIL_SENDING_ERROR", "Failed to send email");
            }
            catch (Exception ex)
            {
                logger.Fatal($"{context} - Unexpected error.", ex);
                throw FaultExceptionFactory.Create(ServiceErrorCode.UnexpectedError,
                    "UNEXPECTED_ERROR", "An unexpected server error occurred.");
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
            catch (ArgumentNullException ex)
            {
                logger.Error($"{context} - Null argument.", ex);
                throw FaultExceptionFactory.Create(ServiceErrorCode.NullArgument,
                    "NULL_ARGUMENT", "A null argument was received occurred.");
            }
            catch (DuplicateAccountException ex)
            {
                logger.Warn($"{context} - Duplicate.", ex);
                throw FaultExceptionFactory.Create(ServiceErrorCode.UserAlreadyExists, 
                    "USER_ALREADY_EXISTS", ex.Message);
            }
            catch (SecurityException ex)
            {
                logger.Error($"{context} - Security error.", ex);
                throw FaultExceptionFactory.Create(ServiceErrorCode.SecurityError,
                    "SECURITY_ERROR", "Could not load Enviroment variables");
            }
            catch (DbUpdateException ex)
            {
                logger.Error($"{context} - Database update error.", ex);
                throw FaultExceptionFactory.Create(ServiceErrorCode.DatabaseError, 
                    "DATABASE_ERROR", "An error occurred while processing the request.");
            }
            catch (ConfigurationErrorsException ex)
            {
                logger.Error($"{context} - Email config error.", ex);
                throw FaultExceptionFactory.Create(ServiceErrorCode.EmailConfigurationError,
                    "EMAIL_CONFIGURATION_ERROR", "Email service configuration error");
            }
            catch (FormatException ex)
            {
                logger.Error($"{context} - Email config format.", ex);
                throw FaultExceptionFactory.Create(ServiceErrorCode.EmailConfigurationError,
                    "EMAIL_CONFIGURATION_ERROR", "Invalid email service configuration");
            }
            catch (SmtpException ex)
            {
                logger.Error($"{context} - SMTP error.", ex);
                throw FaultExceptionFactory.Create(ServiceErrorCode.EmailSendingError,
                    "EMAIL_SENDING_ERROR", "Failed to send email");
            }
            catch (Exception ex)
            {
                logger.Fatal($"{context} - Unexpected error.", ex);
                throw FaultExceptionFactory.Create(ServiceErrorCode.UnexpectedError, 
                    "UNEXPECTED_ERROR", "An unexpected server error occurred.");
            }
        }
    }
}
