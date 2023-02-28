namespace NServiceBus.Transport.SQLServer
{
    using NServiceBus.Logging;
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;

    class ProcessWithTransactionScope : ReceiveStrategy
    {
        static ILog Logger = LogManager.GetLogger<ProcessWithTransactionScope>();

        public ProcessWithTransactionScope(TransactionOptions transactionOptions, SqlConnectionFactory connectionFactory, FailureInfoStorage failureInfoStorage)
        {
            this.transactionOptions = transactionOptions;
            this.connectionFactory = connectionFactory;
            this.failureInfoStorage = failureInfoStorage;
        }

        public override async Task ReceiveMessage(CancellationTokenSource receiveCancellationTokenSource)
        {
            Message message = null;
            try
            {
                using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew, transactionOptions, TransactionScopeAsyncFlowOption.Enabled))
                using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
                {
                    message = await TryReceive(connection, null, receiveCancellationTokenSource).ConfigureAwait(false);

                    if (message == null)
                    {
                        // The message was received but is not fit for processing (e.g. was DLQd).
                        // In such a case we still need to commit the transport tx to remove message
                        // from the queue table.
                        Logger.Debug($"message is null");
                        scope.Complete();
                        return;
                    }

                    connection.Close();

                    message.Headers.TryGetValue("Smokeball.TraceId", out var traceId);
                    message.Headers.TryGetValue("NServiceBus.MessageId", out var messageId);

                    Logger.Debug($"about to process message {traceId ?? messageId}");

                    if (!await TryProcess(message, PrepareTransportTransaction()).ConfigureAwait(false))
                    {
                        Logger.Debug($"failed to process message {traceId ?? messageId}");
                        return;
                    }

                    Logger.Debug($"successfully processed message {traceId ?? messageId}");
                    scope.Complete();
                }

                failureInfoStorage.ClearFailureInfoForMessage(message.TransportId);
            }
            catch (Exception exception)
            {
                if (message == null)
                {
                    throw;
                }
                failureInfoStorage.RecordFailureInfoForMessage(message.TransportId, exception);
            }
        }

        TransportTransaction PrepareTransportTransaction()
        {
            var transportTransaction = new TransportTransaction();

            //those resources are meant to be used by anyone except message dispatcher e.g. persister
            transportTransaction.Set(Transaction.Current);

            return transportTransaction;
        }

        async Task<bool> TryProcess(Message message, TransportTransaction transportTransaction)
        {
            FailureInfoStorage.ProcessingFailureInfo failure;
            if (failureInfoStorage.TryGetFailureInfoForMessage(message.TransportId, out failure))
            {
                var errorHandlingResult = await HandleError(failure.Exception, message, transportTransaction, failure.NumberOfProcessingAttempts).ConfigureAwait(false);

                if (errorHandlingResult == ErrorHandleResult.Handled)
                {
                    return true;
                }
            }

            try
            {
                return await TryProcessingMessage(message, transportTransaction).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failureInfoStorage.RecordFailureInfoForMessage(message.TransportId, exception);
                return false;
            }
        }

        TransactionOptions transactionOptions;
        SqlConnectionFactory connectionFactory;
        FailureInfoStorage failureInfoStorage;
    }
}