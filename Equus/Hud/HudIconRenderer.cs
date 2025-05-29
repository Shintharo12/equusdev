using Cairo;
using Equus.Behaviors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Equus.Hud
{
    public class HudIconRenderer : IRenderer, IDisposable
    {
        public static EquusModSystem ModSystem => EquusModSystem.Instance;
        private ICoreClientAPI capi;
        private Dictionary<string, LoadedTexture> texturesDict = new();
        private LoadedTexture activeTexture;
        private long listenerId;
        public double RenderOrder => 1;
        public int RenderRange => 0;
        public HudIconRenderer(ICoreClientAPI api) 
        {
            capi = api;
            api.Event.RegisterRenderer(this, EnumRenderStage.Ortho);
        }

        public void Initialize()
        {
            List<AssetLocation> assetLocations = capi.Assets.GetLocations("textures/hud/", ModSystem.ModId);

            foreach (var asset in assetLocations)
            {
                LoadedTexture texture = new(capi);

                string name = asset.GetName().Split('.')[0]; // Get the name without extension

                capi.Render.GetOrLoadTexture(asset, ref texture);
                texturesDict.Add(name, texture);
            }

            // Generate empty texture.
            LoadedTexture empty = new(this.capi);
            ImageSurface surface = new ImageSurface(Format.Argb32, (int)ModSystem.Config.IconSize, (int)ModSystem.Config.IconSize);

            this.capi.Gui.LoadOrUpdateCairoTexture(surface, true, ref empty);
            surface.Dispose();

            texturesDict.Add("empty", empty);

            listenerId = this.capi.Event.RegisterGameTickListener(OnGameTick, 100);
        }

        private bool CanRender()
        {
            return ModSystem.Config.ShowHudIcon == true && activeTexture != null;
        }

        public void OnRenderFrame(float dt, EnumRenderStage stage)
        {
            if (!CanRender()) return;

            float width = (float)GuiElement.scaled(ModSystem.Config.IconSize);
            float height = (float)GuiElement.scaled(ModSystem.Config.IconSize);

            float x = (capi.Render.FrameWidth / 2) - (width / 2) + (float)GuiElement.scaled(ModSystem.Config.IconOffsetX);
            float y = (capi.Render.FrameHeight - height) + (float)GuiElement.scaled(ModSystem.Config.IconOffsetY);

            capi.Render.RenderTexture(activeTexture.TextureId, x, y, width, height);
        }

        protected void OnGameTick(float dt)
        {
            EntityPlayer player = capi.World.Player.Entity;

            if (player.MountedOn?.MountSupplier.OnEntity.GetBehavior<EntityBehaviorRideable>() is EntityBehaviorEquusRideable ebr)
            {
                activeTexture = ebr.CurrentGait switch
                {
                    GaitState.Walk => texturesDict["walk"],
                    GaitState.Trot => texturesDict["trot"],
                    GaitState.Canter => texturesDict["canter"],
                    GaitState.Gallop => texturesDict["gallop"],
                    _ => texturesDict["empty"]
                };
            }
            else
            {
                activeTexture = texturesDict["empty"];
            }
        }

        public void Dispose()
        {
            capi.Event.UnregisterRenderer(this, EnumRenderStage.Ortho);
            capi.Event.UnregisterGameTickListener(listenerId);
            foreach (var texture in texturesDict.Values)
            {
                texture.Dispose();
            }
        }
    }
}
