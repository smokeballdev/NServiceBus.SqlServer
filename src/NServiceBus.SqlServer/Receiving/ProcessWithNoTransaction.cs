namespace NServiceBus.Transport.SQLServer
{
    using NServiceBus.Logging;
    using System;
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;

    class ProcessWithNoTransaction : ReceiveStrategy
    {
        static ILog Logger = LogManager.GetLogger<ProcessWithNoTransaction>();

        public ProcessWithNoTransaction(SqlConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;
        }

        public override async Task ReceiveMessage(CancellationTokenSource receiveCancellationTokenSource)
        {
            using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
            {
                Message message;
                using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    message = await TryReceive(connection, transaction, receiveCancellationTokenSource).ConfigureAwait(false);
                    transaction.Commit();
                }

                if (message == null)
                {
                    Logger.Debug($"message is null");
                    return;
                }

                message.Headers.TryGetValue("Smokeball.TraceId", out var traceId);
                message.Headers.TryGetValue("NServiceBus.MessageId", out var messageId);

                Logger.Debug($"about to process message {traceId ?? messageId}");

                var transportTransaction = new TransportTransaction();
                transportTransaction.Set(connection);

                try
                {
                    await TryProcessingMessage(message, transportTransaction).ConfigureAwait(false);
                    Logger.Debug($"successfully processed message {traceId ?? messageId}");
                }
                catch (Exception exception)
                {
                    Logger.Debug($"failed to process message {traceId ?? messageId}");
                    await HandleError(exception, message, transportTransaction, 1).ConfigureAwait(false);
                }
            }
        }

        SqlConnectionFactory connectionFactory;
    }
}