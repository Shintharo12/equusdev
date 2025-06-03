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
                capi?.TriggerIngameError(this, "toowild", Lang.Get("equus:ingame-error-too-wild"));
                return false;
            }

            return base.CanMount(entityAgent);
        }

        public override void DidMount(EntityAgent entityAgent)
        {
            base.DidMount(entityAgent);

            if (Entity != null)
            {
                Entity.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager.StopTasks();
                Entity.StartAnimation("idle");

                var capi = entityAgent.Api as ICoreClientAPI;
                if (capi != null && capi.World.Player.Entity.EntityId == entityAgent.EntityId) // Isself
                {
                    capi.Input.MouseYaw = Entity.Pos.Yaw;
                }
            }

            var ebr = mountedEntity as IMountableListener;
            (ebr as EntityBehaviorEquusRideable)?.DidMount(entityAgent);

            ebr = Entity as IMountableListener;
            (ebr as EntityBehaviorEquusRideable)?.DidMount(entityAgent);
        }

        public override void DidUnmount(EntityAgent entityAgent)
        {
            if (entityAgent.World.Side == EnumAppSide.Server && DoTeleportOnUnmount)
            {
                tryTeleportToFreeLocation();
            }
            if (entityAgent is EntityPlayer eplr)
            {
                eplr.BodyYawLimits = null;
                eplr.HeadYawLimits = null;
            }

            base.DidUnmount(entityAgent);

            var ebr = mountedEntity as IMountableListener;
            (ebr as EntityBehaviorEquusRideable)?.DidUnnmount(entityAgent);

            ebr = Entity as IMountableListener;
            (ebr as EntityBehaviorEquusRideable)?.DidUnnmount(entityAgent);
        }
    }
}