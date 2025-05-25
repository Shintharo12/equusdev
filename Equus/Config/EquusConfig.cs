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

        // Hud
        public bool HideStaminaOnFull { get; set; } = true;

        // Debugging
        public bool DebugMode { get; set; } = false;

    }
}
