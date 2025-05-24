using Equus.Behaviors;
using Equus.Config;
using Equus.Systems;
using Genelib;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace Equus
{
    public class EquusModSystem : ModSystem
    {
        private FileWatcher _fileWatcher;

        public string ModId => Mod.Info.ModID;
        public ILogger Logger => Mod.Logger;
        public ICoreAPI Api { get; private set; }
        public EquusConfig Config { get; private set; }
        public static EquusModSystem Instance { get; private set; }

        // Called on server and client
        public override void Start(ICoreAPI api)
        {
            Instance = this;
            Api = api;
            GenomeType.RegisterInterpreter(new EquusInterpreter());

            api.RegisterEntityBehaviorClass(ModId + ":rideable", typeof(EntityBehaviorEquusRideable));
            api.RegisterEntityBehaviorClass(ModId + ":rideableaccessories", typeof(EntityBehaviorEquusRideableAccessories));
            api.RegisterEntityBehaviorClass(ModId + ":stamina", typeof(EntityBehaviorStamina));

            ReloadConfig(api, false);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            api.Event.OnEntityLoaded += AddFatigueHandlers;

        }

        public override void StartClientSide(ICoreClientAPI api)
        {

        }

        private void AddFatigueHandlers(Entity entity)
        {
            var ebs = entity.GetBehavior<EntityBehaviorStamina>();
            ebs.OnFatigued += (ftg, ftgSource) => HandleFatigued(entity as EntityAgent, ftg, ftgSource);
        }

        public float HandleFatigued(EntityAgent eagent, float fatigue, FatigueSource ftgSource)
        {
            fatigue = ApplyFatigueProtection(eagent, fatigue, ftgSource);

            return fatigue;
        }

        public float ApplyFatigueProtection(EntityAgent eagent, float fatigue, FatigueSource ftgSource)
        {
            return fatigue;
        }

        public void ReloadConfig(ICoreAPI api, bool isReload)
        {
            (_fileWatcher ??= new FileWatcher()).Queued = true;

            try
            {
                // Load user config
                var _config = api.LoadModConfig<EquusConfig>($"{ModId}.json");

                // If no user config, create one
                if (_config == null)
                {
                    Mod.Logger.Warning("Missing config! Using default.");
                    Config = new EquusConfig();
                }
                else
                {
                    Config = _config;
                }

                // Only do this if we are not actively reloading
                if (isReload)
                {
                    // Update stats
                    // ToDo: Figure out how to update stats on reload
                }
                else
                {
                    // Store config again (to ensure any new props are saved)
                    api.StoreModConfig(Config, $"{ModId}.json");
                }
            }
            catch (Exception ex)
            {
                Mod.Logger.Error($"Could not load {ModId} config!");
                Mod.Logger.Error(ex);
            }

            //api.Event.RegisterCallback(_ => _fileWatcher.Queued = false, 100);
        }

    }
}
