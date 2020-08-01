using Microsoft.Extensions.Logging;
using Sitecore.Commerce.Core;
using Sitecore.Commerce.Core.Caching;
using Sitecore.Framework.Pipelines;
using System;
using System.Threading.Tasks;
using Force.DeepCloner;

namespace Sitecore.Support.Commerce.Core
{
    public class FindEntityInMemoryCacheBlock : PipelineBlock<FindEntityArgument, FindEntityArgument, CommercePipelineExecutionContext>
    {
        private readonly IGetEnvironmentCachePipeline _cachePipeline;

        public FindEntityInMemoryCacheBlock(IGetEnvironmentCachePipeline cachePipeline)
        : base((string)null)
        {
            _cachePipeline = cachePipeline;
        }

        public override async Task<FindEntityArgument> Run(FindEntityArgument arg, CommercePipelineExecutionContext context)
        {
            var entityCachePolicy = EntityMemoryCachingPolicy.GetCachePolicy(context.CommerceContext, arg.EntityType);
            if (!entityCachePolicy.AllowCaching)
            {
                // Skip caching if not enabled for this entity
                return arg;
            }

            var itemKey = arg.EntityId;

            var requestedVersion = context.GetModel<RequestedEntityVersion>(x => x.EntityId.Equals(arg.EntityId, StringComparison.OrdinalIgnoreCase));
            if (arg.EntityVersion.HasValue)
            {
                itemKey += $"-{arg.EntityVersion}";
                context.AddModel(new CacheRequest(arg.EntityId, arg.EntityVersion));
            }
            else if (requestedVersion != null)
            {
                itemKey += $"-{requestedVersion.EntityVersion}";
                context.AddModel(new CacheRequest(arg.EntityId, requestedVersion.EntityVersion));
            }
            else
            {
                context.AddModel(new CacheRequest(arg.EntityId));
            }

            var typeName = arg.EntityType.Name;

            context.Logger.LogDebug($"{this.Name} looking for {itemKey}");

            var cache = await this._cachePipeline.Run(new EnvironmentCacheArgument { CacheName = entityCachePolicy.CacheName }, context);

            if (entityCachePolicy.CacheAsEntity)
            {
                var data = await cache.Get(itemKey) as CommerceEntity;
                if (data == null)
                {
                    context.Logger.LogInformation($"Core.MemCache.CE.Miss.{typeName}: ItemKey={itemKey}");
                }
                else
                {
                    if (!context.HasPolicy<IgnorePublishedPolicy>() && !data.Published)
                    {
                        context.Logger.LogDebug($"Core.MemCache.CE.NotPublished.{typeName}: ItemKey={itemKey}");
                        return arg;
                    }

                    context.Logger.LogDebug($"Core.MemCache.CE.Hit.{typeName}: ItemKey={itemKey}");
                    context.AddModel(new CacheHit(itemKey));

                    context.CommerceContext.AddObject(new FoundEntity
                    {
                        EntityId = arg.EntityId,
                        Entity = data,
                        EntityVersion = data.EntityVersion,
                        FoundInCache = true,
                        CachedAsEntity = true
                    });
                    arg.Entity = data;
                }
            }
            else
            {
                var data = await cache.Get(itemKey) as string;
                if (data == null)
                {
                    context.Logger.LogInformation($"Core.MemCache.S.Miss.{typeName}: ItemKey={itemKey}");
                }
                else
                {
                    context.Logger.LogDebug($"Core.MemCache.S.Hit.{typeName}: ItemKey={itemKey}");
                    context.AddModel(new CacheHit(itemKey));
                    context.CommerceContext.AddObject(new FoundEntity
                    {
                        EntityId = arg.EntityId,
                        SerializedEntity = data,
                        EntityVersion = arg.EntityVersion ?? 1,
                        FoundInCache = true,
                        CachedAsEntity = false
                    });
                    arg.SerializedEntity = data;
                }
            }

            return arg;
        }
    }
}
