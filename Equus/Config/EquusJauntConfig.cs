using Newtonsoft.Json;
using System.Reflection;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Jaunt.Config;

namespace Equus.Config
{
    public class EquusJauntConfig : IJauntConfig
    {
        // Stamina
        public bool EnableStamina { get; set; } = true;
        // Hud
        public bool HideStaminaOnFull { get; set; } = false;
        public string StaminaBarLocation { get; set; } = "AboveHealth";
        public float StaminaBarWidthMultiplier { get; set; } = 1f;
        public float StaminaBarXOffset { get; set; } = 0f;
        public float StaminaBarYOffset { get; set; } = 0f;
        public bool ShowGaitIcon { get; set; } = true;
        public float IconOffsetX { get; set; } = -400f;
        public float IconOffsetY { get; set; } = -99f;
        public float IconSize { get; set; } = 42f;

        // Debugging
        public bool DebugMode { get; set; } = false;

    }
}
