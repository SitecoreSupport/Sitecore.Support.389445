// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ConfigureSitecore.cs" company="Sitecore Corporation">
//   Copyright (c) Sitecore Corporation 1999-2017
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Sitecore.Support.Commerce.Core
{
    using System.Reflection;
    using Microsoft.Extensions.DependencyInjection;
    using Sitecore.Commerce.Core;
    using Sitecore.Framework.Configuration;
    using Sitecore.Framework.Pipelines.Definitions.Extensions;

    /// <summary>
    /// The configure sitecore class.
    /// </summary>
    public class ConfigureSitecore : IConfigureSitecore
    {
        /// <summary>
        /// The configure services.
        /// </summary>
        /// <param name="services">
        /// The services.
        /// </param>
        public void ConfigureServices(IServiceCollection services)
        {
            var assembly = Assembly.GetExecutingAssembly();
            services.RegisterAllPipelineBlocks(assembly);

            services.Sitecore().Pipelines(config => config
                .ConfigurePipeline<IFindEntityPipeline>
                (
                    configure => configure.Replace<Sitecore.Commerce.Core.FindEntityInMemoryCacheBlock, Support.Commerce.Core.FindEntityInMemoryCacheBlock>()
                )
                .ConfigurePipeline<IFindEntityPipeline>
                (
                    configure => configure.Replace<Sitecore.Commerce.Core.StoreCommerceEntityInMemoryCacheBlock, Support.Commerce.Core.StoreCommerceEntityInMemoryCacheBlock>()
                )
                .ConfigurePipeline<IDoesEntityExistPipeline>
                (
                    configure => configure.Replace<Sitecore.Commerce.Core.FindEntityInMemoryCacheBlock, Support.Commerce.Core.FindEntityInMemoryCacheBlock>()
                ));

            services.RegisterAllCommands(assembly);
        }
    }
}