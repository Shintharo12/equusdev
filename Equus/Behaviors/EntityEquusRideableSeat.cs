using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace Equus.Behaviors
{
    public class EntityEquusRideableSeat : EntityRideableSeat
    {
        public EntityEquusRideableSeat(IMountable mountablesupplier, string seatId, SeatConfig config) : base(mountablesupplier, seatId, config)
        {
        }

        public override bool CanMount(EntityAgent entityAgent)
        {
            if (entityAgent is not EntityPlayer player) return false;

            var ebr = Entity.GetBehavior<EntityBehaviorEquusRideable>();
            if (Entity.WatchedAttributes.GetInt("generation") < ebr.minGeneration && player.Player.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                var capi = entityAgent.World.Api as ICoreClientAPI;
                capi?.TriggerIngameError(this, "toowild", Lang.Get("Animal is too wild to ride"));
                return false;
            }

            return base.CanMount(entityAgent);
        }
    }
}