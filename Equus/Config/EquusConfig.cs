using Newtonsoft.Json;
using System.Reflection;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Equus.Config
{
    public class EquusConfig
    {
        // Spawn rates
        public float WorldGenSpawnRateFerus { get; set; } = 0.002f;
        public float RuntimeSpawnRateFerus { get; set; } = 0.00005f;

        // Stamina
        public bool EnableStamina { get; set; } = true;
        public float GlobalMaxStaminaMultiplier { get; set; } = 1f;
        public float GlobalStaminaRegenMultiplier { get; set; } = 1f;
        
        // Stamina Costs
        public float GlobalSwimStaminaCostMultiplier { get; set; } = 1f;
        public float GlobalSprintStaminaCostMultiplier { get; set; } = 1f;

        // Behaviors
        public float BuckingChanceMultiplier { get; set; } = 0.5f;
        public float BuckingDismountChanceMultiplier { get; set; } = 0.05f;

        // Hud
        public bool HideStaminaOnFull { get; set; } = true;
        public string StaminaBarLocation { get; set; } = "AboveHealth";
        public float StaminaBarWidthMultiplier { get; set; } = 1f;
        public float StaminaBarXOffset { get; set; } = 0f;
        public float StaminaBarYOffset { get; set; } = 0f;
        public bool ShowHudIcon { get; set; } = true;
        public float IconOffsetX { get; set; } = -400f;
        public float IconOffsetY { get; set; } = -99f;
        public float IconSize { get; set; } = 42f;

        // Debugging
        public bool DebugMode { get; set; } = true;

    }
}
