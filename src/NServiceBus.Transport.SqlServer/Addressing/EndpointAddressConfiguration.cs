﻿using NServiceBus.Configuration.AdvancedExtensibility;
using NServiceBus.Routing;

namespace NServiceBus.Transport.SqlServer
{
    /// <summary>
    /// Configuration extensions for endpoint catalog and schema settings
    /// </summary>
    public static class EndpointAddressConfiguration
    {
        /// <summary>
        /// Specifies custom schema for given endpoint.
        /// </summary>
        /// <param name="settings"><see cref="RoutingSettings"/></param>
        /// <param name="endpointName">Endpoint name.</param>
        /// <param name="schema">Custom schema value.</param>
        public static void UseSchemaForEndpoint(this RoutingSettings settings, string endpointName, string schema)
        {
            var schemaAndCatalogSettings = settings.GetSettings().GetOrCreate<EndpointSchemaAndCatalogSettings>();

            schemaAndCatalogSettings.SpecifySchema(endpointName, schema);

            settings.GetSettings().GetOrCreate<EndpointInstances>()
                .AddOrReplaceInstances("SqlServer", schemaAndCatalogSettings.ToEndpointInstances());
        }
        

        /// <summary>
        /// Specifies custom catalog for given endpoint.
        /// </summary>
        /// <param name="settings">The <see cref="RoutingSettings" /> to extend.</param>
        /// <param name="endpointName">Endpoint name.</param>
        /// <param name="catalog">Custom catalog value.</param>
        public static void UseCatalogForEndpoint(this RoutingSettings settings, string endpointName, string catalog)
        {
            var schemaAndCatalogSettings = settings.GetSettings().GetOrCreate<EndpointSchemaAndCatalogSettings>();

            schemaAndCatalogSettings.SpecifyCatalog(endpointName, catalog);

            settings.GetSettings().GetOrCreate<EndpointInstances>()
                .AddOrReplaceInstances("SqlServer", schemaAndCatalogSettings.ToEndpointInstances());
        }
    }
}