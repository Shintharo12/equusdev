using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace Equus.Hud
{
    public class HudElementStaminaBar : HudElement
    {
        public static EquusModSystem ModSystem => EquusModSystem.Instance;

        private GuiElementStatbar _statbar;
        private int _tickCounter;
        private float _lastCurrentStamina = -1f;
        private float _lastMaxStamina = -1f;
        private float _lastLineInterval = -1f;

        public bool ShowStaminaBar = true;
        public override double InputOrder => 1.0;
        private static bool HideStaminaOnFull => ModSystem.Config?.HideStaminaOnFull ?? true;

        public HudElementStaminaBar(ICoreClientAPI capi) : base(capi)
        {
            ComposeGuis();
            capi.Event.RegisterGameTickListener(OnGameTick, 100);
        }

        public void OnGameTick(float dt)
        {
            if (!ModSystem.Config.EnableStamina ||
                capi.World.Player.WorldData.CurrentGameMode == EnumGameMode.Spectator)
            {
                return;
            }

            UpdateStamina();

            _tickCounter++;
            if (_tickCounter >= 2)
            {
                CheckFlash();
                _tickCounter = 0;
            }
        }

        private void CheckFlash()
        {
            if (_statbar == null) return;

            if (_lastCurrentStamina < _lastMaxStamina * 0.3f)
            {
                _statbar.ShouldFlash = true;
            }
            else
            {
                _statbar.ShouldFlash = false;
            }
        }

        private void UpdateStamina()
        {
            if (capi.World.Player.Entity?.MountedOn?.Entity is not EntityAgent equus) return;

            var staminaTree = equus.WatchedAttributes.GetTreeAttribute($"{ModSystem.ModId}:stamina");
            if (staminaTree == null) return;

            if (_statbar == null) return;

            var stamina = staminaTree.GetFloat("currentstamina");
            var maxStamina = staminaTree.GetFloat("maxstamina");

            if (stamina != _lastCurrentStamina)
            {
                if (maxStamina != _lastMaxStamina)
                {
                    var newLineInterval = Math.Max(100f, maxStamina / 100f);
                    if (Math.Abs(newLineInterval - _lastLineInterval) > 0.001f)
                    {
                        _statbar.SetLineInterval(newLineInterval);
                        _lastLineInterval = newLineInterval;
                    }
                }

                _statbar.SetValues(stamina, 0.0f, maxStamina);
                _lastCurrentStamina = stamina;
                _lastMaxStamina = maxStamina;
            }
        }

        private void ComposeGuis()
        {
            const float statsBarParentWidth = 850f;
            const float statsBarWidth = statsBarParentWidth * 0.41f;

            double[] staminaBarColor = { 0.85, 0.65, 0, 0.5 };

            var statsBarBounds = new ElementBounds()
            {
                Alignment = EnumDialogArea.CenterBottom,
                BothSizing = ElementSizing.Fixed,
                fixedWidth = statsBarParentWidth,
                fixedHeight = 100
            }.WithFixedAlignmentOffset(0.0, 5.0);

            var isRight = false;
            var alignmentOffsetX = isRight ? -2.0 : 1.0;

            var staminaBarBounds = ElementStdBounds.Statbar(isRight ? EnumDialogArea.RightTop : EnumDialogArea.LeftTop, statsBarWidth)
                .WithFixedAlignmentOffset(alignmentOffsetX, -16)
                .WithFixedHeight(10);

            var staminaBarParentBounds = statsBarBounds.FlatCopy().FixedGrow(0.0, 20.0);

            var composer = capi.Gui.CreateCompo("staminastatbar", staminaBarParentBounds);

            _statbar = new GuiElementStatbar(composer.Api, staminaBarBounds, staminaBarColor, isRight, false);

            composer
                .BeginChildElements(statsBarBounds)
                .AddInteractiveElement(_statbar, "staminastatsbar")
                .EndChildElements()
                .Compose();

            Composers["staminabar"] = composer;

            TryOpen();
        }
        public override void OnOwnPlayerDataReceived()
        {
            ComposeGuis();
            UpdateStamina();
        }

        public override void OnRenderGUI(float deltaTime)
        {
            if (capi.World.Player.Entity?.MountedOn?.Entity is not EntityAgent equus) return;

            var staminaTree = equus.WatchedAttributes.GetTreeAttribute($"{ModSystem.ModId}:stamina");
            if (staminaTree == null) return;

            var stamina = staminaTree.GetFloat("currentstamina");
            var maxStamina = staminaTree.GetFloat("maxstamina");

            if (stamina == maxStamina && HideStaminaOnFull) return;

            if (!ModSystem.Config.EnableStamina) return;

            base.OnRenderGUI(deltaTime);
        }

        public override bool TryClose() => false;

        public override bool ShouldReceiveKeyboardEvents() => false;

        public override bool Focusable => false;
    }
}