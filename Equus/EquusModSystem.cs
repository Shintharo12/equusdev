using Genelib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace Equus
{
    public class EquusModSystem : ModSystem
    {
        // Called on server and client
        public override void Start(ICoreAPI api)
        {
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
