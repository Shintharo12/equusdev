using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace Equus.Hud
{
    public class HudOverlaySystem : ModSystem, IDisposable
    {
        ICoreClientAPI capi;
        StaminaBarRenderer renderer;
        private static EquusModSystem ModSystem => EquusModSystem.Instance;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Client;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            renderer = new StaminaBarRenderer(api);
            api.Event.RegisterRenderer(renderer, EnumRenderStage.Ortho, $"{ModSystem.ModId}:staminabar");
        }

        public override void Dispose()
        {
            renderer.Dispose();
        }
    }
}
