using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Equus.Hud
{
    internal class StaminaBarRenderer : IRenderer, IDisposable
    {
        public static EquusModSystem ModSystem => EquusModSystem.Instance;

        ICoreClientAPI capi;
        private Vec4f exhaustedColorBackground = new Vec4f(255f, 0f, 0f, 0.5f);
        private Vec4f circleColorBackground = new Vec4f(255f, 255f, 255f, 0.2f);
        private Vec4f circleColorStamina = new Vec4f(255f, 255f, 255f, 0.7f);
        private Matrixf mvMatrix = new Matrixf();
        private MeshRef meshRef;
        private bool invGUIopen = false;
        private bool disposed;
        private float exhaustedFlashTimer = 0f;
        private MeshRef[] staminaMeshes;
        private const int StaminaMeshSteps = 100; // 5% increments

        private static bool HideStaminaOnFull => ModSystem.Config?.HideStaminaOnFull ?? true;
        private static float InnerRadius => ModSystem.Config?.StaminaCircleInnerRadius ?? 0.6f;
        private static float OuterRadius => ModSystem.Config?.StaminaCircleOuterRadius ?? 0.8f;
        private static float Scale => 35f * ModSystem.Config?.StaminaCircleScale ?? 35f;

        public double RenderOrder { get; set; } = 0.0;
        public int RenderRange { get; set; } = 10;
        public StaminaBarRenderer(ICoreClientAPI capi)
        {
            this.capi = capi;

            staminaMeshes = new MeshRef[StaminaMeshSteps + 1];

            for (int i = 0; i <= StaminaMeshSteps; i++)
            {
                float percent = i / (float)StaminaMeshSteps;
                MeshData mesh = MeshUtil.GetRing(InnerRadius, OuterRadius, StaminaMeshSteps, 2 * Math.PI * percent, ColorUtil.BlackArgb);
                staminaMeshes[i] = capi.Render.UploadMesh(mesh);
            }

            MeshData backgroundRing = MeshUtil.GetRing(InnerRadius, OuterRadius, StaminaMeshSteps, 2*Math.PI, ColorUtil.BlackArgb);
            meshRef = capi.Render.UploadMesh(backgroundRing);
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (capi.World.Player.Entity?.MountedOn?.Entity is not EntityAgent equus) return;

            var staminaTree = equus.WatchedAttributes.GetTreeAttribute($"{ModSystem.ModId}:stamina");
            if (staminaTree == null) return;

            var stamina = staminaTree.GetFloat("currentstamina");
            var maxStamina = staminaTree.GetFloat("maxstamina");
            var exhausted = staminaTree.GetBool("exhausted");

            if (stamina == maxStamina && HideStaminaOnFull) return;

            #region Check if the inventory GUI is open
            invGUIopen = false;

            foreach (var gui in capi.Gui.OpenedGuis)
            {
                string name = gui.ToString();
                if (name == "Vintagestory.Client.NoObf.GuiDialogInventory" || name == "Vintagestory.Client.NoObf.GuiDialogCharacter")
                {
                    invGUIopen = true;
                    return;
                }
            }
            #endregion

            if (exhausted)
            {
                exhaustedFlashTimer += deltaTime;
            }
            else
            {
                exhaustedFlashTimer = 0f; // Reset when no longer exhausted
            }

            IShaderProgram curShader = capi.Render.CurrentActiveShader;

            mvMatrix
                .Set(capi.Render.CurrentModelviewMatrix)
                .Translate(capi.Render.FrameWidth / 2, capi.Render.FrameHeight / 2, 0f)
                .Scale(Scale, Scale, 1f);

            curShader.UniformMatrix("projectionMatrix", capi.Render.CurrentProjectionMatrix);
            curShader.UniformMatrix("modelViewMatrix", mvMatrix.Values);

            curShader.Uniform("tex2d", 0);
            curShader.Uniform("noTexture", 1f);

            // --- Draw Background Ring ---
            if (exhausted)
            {
                // Pulse alpha from 0.2 to 0.8 over a second (sin wave flash)
                float flashAlpha = 0.3f + 0.2f * GameMath.Sin(exhaustedFlashTimer * 5f);

                curShader.Uniform("rgbaIn", new Vec4f(1f, 0f, 0f, flashAlpha)); // Red with flashing alpha);
            }
            else
            {
                curShader.Uniform("rgbaIn", circleColorBackground);
            }

            capi.Render.RenderMesh(meshRef);

            // --- Draw Stamina Ring ---

            float staminaPercent = GameMath.Clamp(stamina / maxStamina, 0f, 1f);
            int staminaMeshIndex = (int)(staminaPercent * StaminaMeshSteps);
            staminaMeshIndex = GameMath.Clamp(staminaMeshIndex, 0, StaminaMeshSteps);

            curShader.Uniform("rgbaIn", circleColorStamina);
            capi.Render.RenderMesh(staminaMeshes[staminaMeshIndex]);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    meshRef.Dispose();
                    capi.Render.DeleteMesh(meshRef);
                    foreach (var mesh in staminaMeshes)
                    {
                        mesh.Dispose();
                        capi.Render.DeleteMesh(mesh);
                    }
                }
                disposed = true;
            }
        }
    }
}
