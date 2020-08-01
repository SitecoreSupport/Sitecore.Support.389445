using Force.DeepCloner;
using Microsoft.Extensions.Logging;
using Sitecore.Commerce.Core;
using Sitecore.Commerce.Core.Caching;
using Sitecore.Commerce.Core.Commands;
using Sitecore.Framework.Caching;
using Sitecore.Framework.Pipelines;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Sitecore.Support.Commerce.Core
{
    public class StoreCommerceEntityInMemoryCacheBlock : PipelineBlock<CommerceEntity, CommerceEntity, CommercePipelineExecutionContext>
    {
        private readonly IGetEnvironmentCachePipeline _cachePipeline;

        private readonly EntitySerializerCommand _entitySerializerCommand;

        public StoreCommerceEntityInMemoryCacheBlock(IGetEnvironmentCachePipeline cachePipeline, EntitySerializerCommand entitySerializerCommand)
            : base((string)null)
        {
            _cachePipeline = cachePipeline;
            _entitySerializerCommand = entitySerializerCommand;
        }

        public override async Task<CommerceEntity> Run(CommerceEntity arg, CommercePipelineExecutionContext context)
        {
            if (arg == null)
            {
                // Do not cache null entries
                return null;
            }

            var foundEntity = context.CommerceContext.GetObjects<FoundEntity>().FirstOrDefault(p => p.EntityId.Equals(arg.Id, StringComparison.OrdinalIgnoreCase));

            // Setup the item 
            var itemKey = arg.Id;

            var cacheRequest = context.GetModel<CacheRequest>(x => x.EntityId.Equals(arg.Id, StringComparison.OrdinalIgnoreCase));
            if (cacheRequest != null && cacheRequest.Version.HasValue)
            {
                itemKey += $"-{cacheRequest.Version}";
            }
            else if (cacheRequest == null)
            {
                itemKey += $"-{arg.EntityVersion}";
            }

            var cacheHits = context.GetModels<CacheHit>();
            for (var index = 0; index < cacheHits.Count; index += 1)
            {
                if (cacheHits[index].ItemKey.Equals(itemKey, StringComparison.OrdinalIgnoreCase))
                {
                    // CacheHit model existence means that this item was already retrieved from the cache so no need to Add it back
                    return arg;
                }
            }

            var typeName = arg.GetType().Name;
            var entityCachePolicy = EntityMemoryCachingPolicy.GetCachePolicy(context.CommerceContext, arg.GetType());
            if (!entityCachePolicy.AllowCaching)
            {
                if (context.GetPolicy<CacheLoggingPolicy>().LogSkipCache)
                {
                    context.Logger.LogInformation($"Core.MemCache.CE.SkipCache.NA.{typeName}: ItemKey={itemKey}");
                }

                return arg;
            }

            var entryOptions = entityCachePolicy.GetCacheEntryOptions();
            var cache = await this._cachePipeline.Run(new EnvironmentCacheArgument { CacheName = entityCachePolicy.CacheName }, context.CommerceContext.GetPipelineContextOptions());
            if (entityCachePolicy.CacheAsEntity)
            {
                await cache.Set(itemKey, new Cachable<CommerceEntity>(arg, 1), entryOptions);

                context.Logger.LogInformation($"Core.MemCache.CE.Add.{typeName}: ItemKey={itemKey} cache:{cache.Name}");

                return arg;
            }
            else
            {
                if (foundEntity == null)
                {
                    context.Logger.LogInformation($"Core.MemCache.CS.Add.NoFoundEntity-{itemKey} cache:{cache.Name}");
                }
                else
                {
                    if (foundEntity.FoundInCache)
                    {
                        context.Logger.LogDebug($"Core.MemCache.CS.SkipCache.AlreadyInCache-{itemKey} cache:{cache.Name}");
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(foundEntity.SerializedEntity))
                        {
                            foundEntity.SerializedEntity = await this._entitySerializerCommand.SerializeEntity(context.CommerceContext, foundEntity.Entity as CommerceEntity);
                        }

                        context.Logger.LogInformation($"Core.MemCache.CS.Add-{itemKey} cache:{cache.Name}");
                        await cache.Set(itemKey, new Cachable<string>(foundEntity.SerializedEntity, 1), entryOptions);
                    }
                }

                return arg;
            }
        }
    }
}
