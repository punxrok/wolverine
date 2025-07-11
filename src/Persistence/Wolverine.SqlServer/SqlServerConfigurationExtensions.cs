﻿using JasperFx.Core;
using JasperFx.Core.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Weasel.Core.Migrations;
using Wolverine.Configuration;
using Wolverine.RDBMS;
using Wolverine.SqlServer.Transport;

namespace Wolverine.SqlServer;

public static class SqlServerConfigurationExtensions
{
    /// <summary>
    ///     Register sql server backed message persistence to a known connection string
    /// </summary>
    /// <param name="settings"></param>
    /// <param name="connectionString"></param>
    /// <param name="schema">Potentially override the schema name for Wolverine envelope storage. Default is to use WolverineOptions.Durability.MessageStorageSchemaName ?? "dbo"</param>
    public static ISqlServerBackedPersistence PersistMessagesWithSqlServer(this WolverineOptions options, string connectionString,
        string? schema = null)
    {
        var extension = new SqlServerBackedPersistence
        {
            ConnectionString = connectionString
        };

        if (schema.IsNotEmpty())
        {
            extension.EnvelopeStorageSchemaName = schema;
        }
        else
        {
            extension.EnvelopeStorageSchemaName = options.Durability.MessageStorageSchemaName ?? "dbo";
        }
        
        options.Include(extension);

        return extension;
    }

    /// <summary>
    /// Register Sql Server backed message persistence *and* the Sql Server messaging transport
    /// </summary>
    /// <param name="options"></param>
    /// <param name="connectionString"></param>
    /// <param name="schema"></param>
    /// <returns></returns>
    [Obsolete("Prefer PersistMessagesWithSqlServer().EnableMessageTransport()")]
    public static SqlServerPersistenceExpression UseSqlServerPersistenceAndTransport(this WolverineOptions options,
        string connectionString,
        string? schema = null,
        string? transportSchema = null)
    {
        options.PersistMessagesWithSqlServer(connectionString, schema);

        options.Services.AddTransient<IDatabase, SqlServerTransportDatabase>();

        var transport = new SqlServerTransport(new DatabaseSettings
        {
            ConnectionString = connectionString,
            SchemaName = schema ?? "dbo"
        }, transportSchema);
        
        options.Transports.Add(transport);

        return new SqlServerPersistenceExpression(transport, options);
    }

    /// <summary>
    ///     Quick access to the Rabbit MQ Transport within this application.
    ///     This is for advanced usage
    /// </summary>
    /// <param name="endpoints"></param>
    /// <returns></returns>
    internal static SqlServerTransport SqlServerTransport(this WolverineOptions endpoints)
    {
        var transports = endpoints.As<WolverineOptions>().Transports;

        try
        {
            return transports.OfType<SqlServerTransport>().Single();
        }
        catch (Exception)
        {
            throw new InvalidOperationException("The Sql Server transport is not registered in this system");
        }
    }

    /// <summary>
    /// Listen for incoming messages at the designated Sql Server queue by name
    /// </summary>
    /// <param name="endpoints"></param>
    /// <param name="queueName"></param>
    /// <returns></returns>
    public static SqlServerListenerConfiguration ListenToSqlServerQueue(this WolverineOptions endpoints, string queueName)
    {
        var transport = endpoints.SqlServerTransport();
        var corrected = transport.MaybeCorrectName(queueName);
        var queue = transport.Queues[corrected];
        queue.EndpointName = queueName;
        queue.IsListener = true;

        return new SqlServerListenerConfiguration(queue);
    }

    /// <summary>
    ///     Publish matching messages straight to a Sql Server queue using the queue name
    /// </summary>
    /// <param name="publishing"></param>
    /// <param name="queueName"></param>
    /// <returns></returns>
    public static SqlServerSubscriberConfiguration ToSqlServerQueue(this IPublishToExpression publishing,
        string queueName)
    {
        var transports = publishing.As<PublishingExpression>().Parent.Transports;
        var transport = transports.OfType<SqlServerTransport>().Single();

        var corrected = transport.MaybeCorrectName(queueName);
        var queue = transport.Queues[corrected];
        queue.EndpointName = queueName;

        // This is necessary unfortunately to hook up the subscription rules
        publishing.To(queue.Uri);

        return new SqlServerSubscriberConfiguration(queue);
    }
}