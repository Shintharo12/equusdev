using Genelib;
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
        public static EquusModSystem Instance { get; private set; }

        // Called on server and client
        public override void Start(ICoreAPI api)
        {
            Instance = this;
            Api = api;
            GenomeType.RegisterInterpreter(new EquusInterpreter());
        }

        public override void StartServerSide(ICoreServerAPI api)
        {

        }

        public override void StartClientSide(ICoreClientAPI api)
        {
        }
    }
}
