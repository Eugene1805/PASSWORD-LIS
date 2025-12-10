using Data.Exceptions;
using log4net;
using Services.Contracts.DTOs;
using Services.Contracts.Enums;
using Services.Util;
using System;
using System.Collections.Generic;
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
        private readonly Dictionary<Type, ExceptionHandlerConfig> exceptionHandlers;

        protected ServiceBase(ILog log) 
        {
            this.logger = log;
            this.exceptionHandlers = BuildExceptionHandlers();
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
            if (ex is DbUpdateException)
            {
                HandleDbUpdateException(ex, context, useUpdateMessageForDbUpdate);
            }

            var exceptionType = ex.GetType();
            if (exceptionHandlers.TryGetValue(exceptionType, out var config))
            {
                config.LogAction(logger, context, ex);
                throw FaultExceptionFactory.Create(config.ErrorCode, config.ErrorCodeString, config.MessageFactory(ex));
            }

            logger.Fatal($"{context} - Unexpected error.", ex);
            throw FaultExceptionFactory.Create(ServiceErrorCode.UnexpectedError,
                "UNEXPECTED_ERROR", "An unexpected server error occurred.");
        }

        private void HandleDbUpdateException(Exception ex, string context, bool useUpdateMessageForDbUpdate)
        {
            logger.Error($"{context} - Database update error.", ex);
            var dbUpdateMsg = useUpdateMessageForDbUpdate
                ? "An error occurred while processing the update."
                : "An error occurred while processing the request.";
            throw FaultExceptionFactory.Create(ServiceErrorCode.DatabaseError, DatabaseError, dbUpdateMsg);
        }

        private static Dictionary<Type, ExceptionHandlerConfig> BuildExceptionHandlers()
        {
            return new Dictionary<Type, ExceptionHandlerConfig>
            {
                {
                    typeof(ArgumentNullException),
                    new ExceptionHandlerConfig(
                        (log, ctx, ex) => log.Error($"{ctx} - Null argument.", ex),
                        ServiceErrorCode.NullArgument,
                        "NULL_ARGUMENT",
                        "A null argument was received occurred.")
                },
                {
                    typeof(InvalidOperationException),
                    new ExceptionHandlerConfig(
                        (log, ctx, ex) => log.Error($"{ctx} - Invalid operation", ex),
                        ServiceErrorCode.InvalidOperation,
                        "INVALID_OPERATION",
                        ex => ex.Message)
                },
                {
                    typeof(DuplicateAccountException),
                    new ExceptionHandlerConfig(
                        (log, ctx, ex) => log.Warn($"{ctx} - Duplicate.", ex),
                        ServiceErrorCode.UserAlreadyExists,
                        "USER_ALREADY_EXISTS",
                        ex => ex.Message)
                },
                {
                    typeof(SecurityException),
                    new ExceptionHandlerConfig(
                        (log, ctx, ex) => log.Error($"{ctx} - Security error.", ex),
                        ServiceErrorCode.SecurityError,
                        "SECURITY_ERROR",
                        "Could not load Enviroment variables")
                },
                {
                    typeof(ConfigurationErrorsException),
                    new ExceptionHandlerConfig(
                        (log, ctx, ex) => log.Error($"{ctx} - Email config error.", ex),
                        ServiceErrorCode.EmailConfigurationError,
                        "EMAIL_CONFIGURATION_ERROR",
                        "Email service configuration error")
                },
                {
                    typeof(FormatException),
                    new ExceptionHandlerConfig(
                        (log, ctx, ex) => log.Error($"{ctx} - Email config format.", ex),
                        ServiceErrorCode.FormatError,
                        "FORMAT_ERROR",
                        "Invalid email service configuration")
                },
                {
                    typeof(SmtpException),
                    new ExceptionHandlerConfig(
                        (log, ctx, ex) => log.Error($"{ctx} - SMTP error.", ex),
                        ServiceErrorCode.EmailSendingError,
                        "EMAIL_SENDING_ERROR",
                        "Failed to send email")
                },
                {
                    typeof(EntityException),
                    new ExceptionHandlerConfig(
                        (log, ctx, ex) => log.Error($"{ctx} - Entity Framework error.", ex),
                        ServiceErrorCode.DatabaseError,
                        DatabaseError,
                        "An error occurred while processing the request.")
                }
            };
        }
    }
}
