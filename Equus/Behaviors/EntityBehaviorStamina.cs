using Equus.Systems;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Equus.Behaviors
{
    /// <summary>
    /// Entity behavior for stamina management. This is a server side behavior that syncs to the client.
    /// </summary>
    /// <param name="fatigue"></param>
    /// <param name="ftgSource"></param>
    /// <returns></returns>
    public delegate float OnFatiguedDelegate(float fatigue, FatigueSource ftgSource);
    public class EntityBehaviorStamina : EntityBehavior
    {
        public static EquusModSystem ModSystem => EquusModSystem.Instance;

        public event OnFatiguedDelegate OnFatigued = (ftg, ftgSource) => ftg;

        private float timeSinceLastUpdate;
        private EntityAgent eagent;

        #region Config props

        private static bool DebugMode => ModSystem.Config.DebugMode; // Debug mode for logging
        #endregion

        private ITreeAttribute StaminaTree
        {
            get
            {
                var tree = entity.WatchedAttributes.GetTreeAttribute(AttributeKey);
                if (tree == null)
                {
                    tree = new TreeAttribute();
                    entity.WatchedAttributes.SetAttribute(AttributeKey, tree);
                    entity.WatchedAttributes.MarkPathDirty(AttributeKey);
                }
                return tree;
            }
        }

        private static string AttributeKey => $"{ModSystem.ModId}:stamina";

        public bool Exhausted
        {
            get => StaminaTree?.GetBool("exhausted") ?? false;
            set
            {
                StaminaTree.SetBool("exhausted", value);
                entity.WatchedAttributes.MarkPathDirty(AttributeKey);
            }
        }

        public float Stamina
        {
            get => StaminaTree?.GetFloat("currentstamina") ?? 100f;
            set
            {
                StaminaTree.SetFloat("currentstamina", value);
                entity.WatchedAttributes.MarkPathDirty(AttributeKey);
            }
        }

        public float MaxStamina
        {
            get => StaminaTree?.GetFloat("maxstamina") ?? 100f;
            set
            {
                StaminaTree.SetFloat("maxstamina", value);
                entity.WatchedAttributes.MarkPathDirty(AttributeKey);
            }
        }

        public float SprintFatigue
        {
            get => StaminaTree?.GetFloat("sprintfatigue") ?? 1f;
            set
            {
                StaminaTree.SetFloat("sprintfatigue", value);
                entity.WatchedAttributes.MarkPathDirty(AttributeKey);
            }
        }
        public float SwimFatigue
        {
            get => StaminaTree?.GetFloat("swimfatigue") ?? 1f;
            set
            {
                StaminaTree.SetFloat("swimfatigue", value);
                entity.WatchedAttributes.MarkPathDirty(AttributeKey);
            }
        }

        public float BaseFatigueRate
        {
            get => StaminaTree?.GetFloat("basefatiguerate") ?? 1f;
            set
            {
                StaminaTree.SetFloat("basefatiguerate", value);
                entity.WatchedAttributes.MarkPathDirty(AttributeKey);
            }
        }

        public float StaminaRegenRate
        {
            get => StaminaTree?.GetFloat("staminaregenrate") ?? 1f;
            set
            {
                StaminaTree.SetFloat("staminaregenrate", value);
                entity.WatchedAttributes.MarkPathDirty(AttributeKey);
            }
        }

        public float RegenPenaltySwimming
        {
            get => StaminaTree?.GetFloat("regenpenaltyswimming") ?? 0f;
            set
            {
                StaminaTree.SetFloat("regenpenaltyswimming", value);
                entity.WatchedAttributes.MarkPathDirty(AttributeKey);
            }
        }
        public float RegenPenaltyMounted
        {
            get => StaminaTree?.GetFloat("regenpenaltymounted") ?? 0f;
            set
            {
                StaminaTree.SetFloat("regenpenaltymounted", value);
                entity.WatchedAttributes.MarkPathDirty(AttributeKey);
            }
        }

        public bool Sprinting
        {
            get => StaminaTree?.GetBool("sprinting") ?? false;
            set
            {
                StaminaTree.SetBool("sprinting", value);
                entity.WatchedAttributes.MarkPathDirty(AttributeKey);
            }
        }

        public FatigueSource SprintFatigueSource;
        public FatigueSource SwimFatigueSource;

        public EntityBehaviorStamina(Entity entity) : base(entity) 
        { 
            eagent = entity as EntityAgent;
        }

        public void MapAttributes(JsonObject typeAttributes, JsonObject staminaAttributes)
        {
            Exhausted = typeAttributes["exhausted"].AsBool(false);
            MaxStamina = typeAttributes["maxstamina"].AsFloat(staminaAttributes["maxStamina"].AsFloat(100f));
            Stamina = typeAttributes["currentstamina"].AsFloat(staminaAttributes["maxStamina"].AsFloat(100f));
            SprintFatigue = typeAttributes["sprintfatigue"].AsFloat(staminaAttributes["sprintfatigue"].AsFloat(0.2f));
            SwimFatigue = typeAttributes["swimfatigue"].AsFloat(staminaAttributes["swimfatigue"].AsFloat(0.2f));
            StaminaRegenRate = typeAttributes["staminaregenrate"].AsFloat(staminaAttributes["staminaregenrate"].AsFloat(1f));
            BaseFatigueRate = typeAttributes["basefatiguerate"].AsFloat(staminaAttributes["basefatiguerate"].AsFloat(1f));
            RegenPenaltySwimming = typeAttributes["regenpenaltyswimming"].AsFloat(staminaAttributes["regenpenaltyswimming"].AsFloat(0f));
            RegenPenaltyMounted = typeAttributes["regenpenaltymounted"].AsFloat(staminaAttributes["regenpenaltymounted"].AsFloat(0f));
            Sprinting = typeAttributes["sprinting"].AsBool(false);
            MarkDirty();
        }


        public override void Initialize(EntityProperties properties, JsonObject typeAttributes)
        {
            if (DebugMode) ModSystem.Logger.Notification($"{ModSystem.ModId} - Initializing stamina behavior for {0}", entity.EntityId);

            // Initialize common fatigue sources
            SprintFatigueSource = new()
            {
                Source = EnumFatigueSource.Run,
                SourceEntity = entity
            };

            SwimFatigueSource = new()
            {
                Source = EnumFatigueSource.Swim,
                SourceEntity = entity
            };

            // Fetch the stamina tree attribute
            var staminaTree = entity.WatchedAttributes.GetTreeAttribute(AttributeKey);

            // Fetch the stamina attributes from the entity properties
            var staminaAttributes = entity.Properties.Attributes["stamina"];

            // Initialize stamina tree
            if (staminaTree == null) entity.WatchedAttributes.SetAttribute(AttributeKey, new TreeAttribute());

            // Map attributes from entity properties to attribute tree
            MapAttributes(typeAttributes, staminaAttributes);

            timeSinceLastUpdate = (float)entity.World.Rand.NextDouble();   // Randomise which game tick these update, a starting server would otherwise start all loaded entities with the same zero timer
        }

        // For syncing sprinting state from client to server
        public override void OnReceivedClientPacket(IServerPlayer player, int packetid, byte[] data, ref EnumHandling handled)
        {
            Sprinting = packetid == 4242;
            handled = EnumHandling.Handled;
        }

        public override void OnGameTick(float deltaTime)
        {
            if (eagent.World.Side == EnumAppSide.Client)
            {
                var capi = entity.Api as ICoreClientAPI;

                // Only update if changed to reduce traffic
                bool currentlySprinting = eagent.Controls.Sprint;
                
                if (Sprinting != currentlySprinting)
                {
                    // Update client side sprinting state
                    Sprinting = currentlySprinting;

                    // Sync sprinting state to server, not sure why the tree attribute doesn't do this automatically
                    capi.Network.SendEntityPacket(entity.EntityId, currentlySprinting ? 4242 : 2424);
                    //if (DebugMode) ModSystem.Logger.Notification($"{entity.EntityId} - Sending sprinting state: {currentlySprinting}");
                }
                return;
            }            

            var stamina = Stamina;  // higher performance to read this TreeAttribute only once
            var maxStamina = MaxStamina;
            var sprinting = Sprinting;

            EntityBehaviorEquusRideableOld ebr = eagent.GetBehavior<EntityBehaviorEquusRideableOld>();
            EntityPlayer rider = ebr.Controller as EntityPlayer;

            timeSinceLastUpdate += deltaTime;

            // Check stamina 4 times a second
            // Since this is server side only don't attempt to trigger animations here
            if (timeSinceLastUpdate >= 0.25f)
            {
                if (entity.Alive)
                {
                    bool activelyFatiguing = false;

                    // --- Fatiguing actions ---
                    // Entity swimming
                    if (eagent.Swimming)
                    {
                        activelyFatiguing = ApplyFatigue(SwimFatigue * CalculateElapsedMultiplier(timeSinceLastUpdate), EnumFatigueSource.Swim);
                    }

                    // Entity sprinting (rider?.MountedOn != null && rider.MountedOn.Controls.Sprint) || 
                    if (sprinting)
                    {
                        activelyFatiguing = ApplyFatigue(SprintFatigue * CalculateElapsedMultiplier(timeSinceLastUpdate), EnumFatigueSource.Run);
                    }

                    // --- Stamina regeneration ---
                    if (!activelyFatiguing)
                    {
                        RegenerateStamina(timeSinceLastUpdate);
                    }
                }

                Exhausted = stamina <= 0; // Entity is exhausted when stamina reaches 0

                timeSinceLastUpdate = 0; 
            }
        }

        private float CalculateElapsedMultiplier(float elapsedTime)
        {
            return elapsedTime * entity.Api.World.Calendar.SpeedOfTime * entity.Api.World.Calendar.CalendarSpeedMul;
        }

        public void RegenerateStamina(float elapsedTime)
        {
            var stamina = Stamina;  // better performance to read this TreeAttribute only once
            var maxStamina = MaxStamina;

            // Add up penalties for various actions
            var currentSwimmingPenalty = eagent.Swimming ? RegenPenaltySwimming : 0f;
            var currentMountedPenalty = eagent.GetBehavior<EntityBehaviorEquusRideableOld>().AnyMounted() ? RegenPenaltyMounted : 0f;

            var totalPenalty = currentMountedPenalty + currentSwimmingPenalty;

            var staminaRegenRate = (StaminaRegenRate - totalPenalty) * ModSystem.Config.GlobalStaminaRegenMultiplier;

            if (stamina < maxStamina)
            {
                // 25% multiplier since we do this four times a second
                var staminaRegenPerGameSecond = 0.25f * staminaRegenRate;
                var multiplierPerGameSec = elapsedTime * ModSystem.Api.World.Calendar.SpeedOfTime * ModSystem.Api.World.Calendar.CalendarSpeedMul;

                Stamina = Math.Min(stamina + (multiplierPerGameSec * staminaRegenPerGameSecond), maxStamina);
            }
        }

        private bool ApplyFatigue(float fatigueAmount, EnumFatigueSource source)
        {
            if (fatigueAmount <= 0) return false;

            FatigueSource fs = new()
            {
                Source = source,
                SourceEntity = entity,
                CauseEntity = entity,
                SourceBlock = null,
                SourcePos = entity.Pos.XYZ
            };

            FatigueEntity(fatigueAmount, fs);

            return true;
        }

        public void OnEntityFatigued(FatigueSource fatigueSource, ref float fatigue)
        {
            // Only fatigue server side and sync to client
            if (entity.World.Side == EnumAppSide.Client) return;         

            if (OnFatigued != null)
            {
                foreach (OnFatiguedDelegate dele in OnFatigued.GetInvocationList().Cast<OnFatiguedDelegate>())
                {
                    fatigue = dele.Invoke(fatigue, fatigueSource);
                }
            }

            FatigueEntity(fatigue, fatigueSource);
        }

        public void FatigueEntity(float fatigue, FatigueSource ftgSource)
        {
            var stamina = Stamina;  // higher performance to read this TreeAttribute only once
            var maxStamina = MaxStamina;

            if (entity.World.Side == EnumAppSide.Client) return;

            if (!entity.Alive) return;
            if (fatigue <= 0) return;

            var fatigueRate = BaseFatigueRate * fatigue;

            Stamina = GameMath.Clamp(stamina - fatigueRate, 0, maxStamina);

            if (DebugMode)
            {
                //ModSystem.Logger.Notification($"{ftgSource.Source} reduced stamina by: {fatigue}");
                //ModSystem.Logger.Notification($"Stamina: {stamina}/{maxStamina}");
            }
        }

        public override void GetInfoText(StringBuilder infotext)
        {
            var capi = entity.Api as ICoreClientAPI;
            if (capi?.World.Player?.WorldData?.CurrentGameMode == EnumGameMode.Creative)
            {
                infotext.AppendLine(Lang.Get($"[{ModSystem.ModId}] Stamina: {Stamina}/{MaxStamina}"));
                infotext.AppendLine(Lang.Get($"[{ModSystem.ModId}] Sprint Fatigue: {SprintFatigue}"));
                infotext.AppendLine(Lang.Get($"[{ModSystem.ModId}] Swim Fatigue: {SwimFatigue}"));
            }
        }

        public override string PropertyName()
        {
            return AttributeKey;
        }

        public void MarkDirty()
        {
            entity.WatchedAttributes.MarkPathDirty(AttributeKey);
        }
    }
}
