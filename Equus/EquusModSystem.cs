using Equus.Config;
using Genelib;
using Jaunt.Config;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Equus
{
    public class EquusModSystem : ModSystem
    {
        public string ModId => Mod.Info.ModID;
        public ILogger Logger => Mod.Logger;
        public ICoreAPI Api { get; private set; }
        public EquusJauntConfig Config { get; private set; }
        public static EquusModSystem Instance { get; private set; }

        // Called on server and client
        public override void Start(ICoreAPI api)
        {
            Instance = this;
            Api = api;
            GenomeType.RegisterInterpreter(new EquusInterpreter());
            JauntConfig.Register(new EquusJauntConfig());

            ReloadConfig(api);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {

        }

        public override void StartClientSide(ICoreClientAPI api)
        {
        }

        public void ReloadConfig(ICoreAPI api)
        {
            try
            {
                // Load user config
                var _config = api.LoadModConfig<EquusJauntConfig>($"{ModId}.json");

                // If no user config, create one
                if (_config == null)
                {
                    Mod.Logger.Warning("Missing config! Using default.");
                    Config = new EquusJauntConfig();
                    api.StoreModConfig(Config, $"{ModId}.json");
                }
                else
                {
                    Config = _config;
                }
            }
            catch (Exception ex)
            {
                Mod.Logger.Error($"Could not load {ModId} config!");
                Mod.Logger.Error(ex);
            }
        }
    }
}
