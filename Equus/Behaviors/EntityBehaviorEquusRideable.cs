using Equus.Systems;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
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
    // Added enum for gait states
    public enum GaitState
    {
        Walk,
        Canter,
        Gallop
    }

    /// <summary>
    /// Essentially a duplicate of EntityBehaviorRideable, but with added canter state and progressive adjustment using stamina behavior
    /// </summary>
    /// <param name="seat"></param>
    /// <param name="errorMessage"></param>
    /// <returns></returns>
    public delegate bool EquusCanRideDelegate(IMountableSeat seat, out string errorMessage);

    public class EntityBehaviorEquusRideable : EntityBehaviorSeatable, IMountable, IRenderer, IMountableListener
    {
        public static EquusModSystem ModSystem => EquusModSystem.Instance;
        private static bool DebugMode => ModSystem.Config.DebugMode; // Debug mode for logging
        public float StaminaSpeedMultiplier { get; set; } = 1f;
        public Vec3f MountAngle { get; set; } = new Vec3f();
        public EntityPos SeatPosition => entity.SidedPos;
        public double RenderOrder => 1;
        public int RenderRange => 100;
        public virtual float SpeedMultiplier => 1f;
        public Entity Mount => entity;
        // current forward speed
        public double ForwardSpeed = 0.0;
        // current turning speed (rad/tick)
        public double AngularVelocity = 0.0;

        public bool IsInMidJump;
        public event EquusCanRideDelegate CanRide;
        public event EquusCanRideDelegate CanTurn;

        // In EntityBehaviorEquusRideable class
        public GaitState CurrentGait { get; private set; } = GaitState.Walk;

        public float GaitMotionMultiplier
        {
            get
            {
                return CurrentGait switch
                {
                    GaitState.Walk => 1.0f,
                    GaitState.Canter => 1.5f,
                    GaitState.Gallop => 2.0f,
                    _ => 1.0f
                };
            }
        }

        protected ICoreAPI api;
        // Time the player can walk off an edge before gravity applies.
        protected float coyoteTimer;
        // Time the player last jumped.
        protected long lastJumpMs;
        protected bool jumpNow;
        protected EntityAgent eagent;
        protected RideableConfig rideableconfig;
        protected ILoadedSound trotSound;
        protected ILoadedSound gallopSound;
        protected ICoreClientAPI capi;

        protected long lastGaitChangeMs = 0;
        protected bool lastSprintPressed = false;
        private float timeSinceLastLog = 0;
        private float timeSinceLastGaitCheck = 0;
        internal int minGeneration = 0; // Minimum generation for the equus to be rideable

        ControlMeta curControlMeta = null;
        bool shouldMove = false;
        public AnimationMetaData curAnim;

        string curTurnAnim = null;
        EnumControlScheme scheme;
        EntityBehaviorStamina ebs;

        public double lastDismountTotalHours
        {
            get
            {
                return entity.WatchedAttributes.GetDouble("lastDismountTotalHours");
            }
            set
            {
                entity.WatchedAttributes.SetDouble("lastDismountTotalHours", value);
            }
        }

        public EntityBehaviorEquusRideable(Entity entity) : base(entity)
        {
            eagent = entity as EntityAgent;
        }

        protected override IMountableSeat CreateSeat(string seatId, SeatConfig config)
        {
            return new EntityEquusRideableSeat(this, seatId, config);
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            rideableconfig = attributes.AsObject<RideableConfig>();
            minGeneration = rideableconfig.MinGeneration;
            foreach (var val in rideableconfig.Controls.Values) { val.RiderAnim?.Init(); }

            api = entity.Api;
            capi = api as ICoreClientAPI;
            curAnim = rideableconfig.Controls["idle"].RiderAnim;

            capi?.Event.RegisterRenderer(this, EnumRenderStage.Before, "rideablesim");
        }

        public override void AfterInitialized(bool onFirstSpawn)
        {
            ebs = eagent.GetBehavior<EntityBehaviorStamina>();
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            base.OnEntityDespawn(despawn);

            capi?.Event.UnregisterRenderer(this, EnumRenderStage.Before);
        }

        public void UnmnountPassengers()
        {
            foreach (var seat in Seats)
            {
                (seat.Passenger as EntityAgent)?.TryUnmount();
            }
        }

        public override void OnEntityLoaded()
        {
            SetupTaskBlocker();
        }

        public override void OnEntitySpawn()
        {
            SetupTaskBlocker();
        }

        void SetupTaskBlocker()
        {
            var ebc = entity.GetBehavior<EntityBehaviorAttachable>();

            if (api.Side == EnumAppSide.Server)
            {
                EntityBehaviorTaskAI taskAi = entity.GetBehavior<EntityBehaviorTaskAI>();
                taskAi.TaskManager.OnShouldExecuteTask += TaskManager_OnShouldExecuteTask;
                if (ebc != null)
                {
                    ebc.Inventory.SlotModified += Inventory_SlotModified;
                }
            }
            else
            {
                if (ebc != null)
                {
                    entity.WatchedAttributes.RegisterModifiedListener(ebc.InventoryClassName, UpdateControlScheme);
                }
            }

        }

        private void Inventory_SlotModified(int obj)
        {
            UpdateControlScheme();
        }

        private void UpdateControlScheme()
        {
            var ebc = entity.GetBehavior<EntityBehaviorAttachable>();
            if (ebc != null)
            {
                scheme = EnumControlScheme.Hold;
                foreach (var slot in ebc.Inventory)
                {
                    if (slot.Empty) continue;
                    var sch = slot.Itemstack.ItemAttributes?["controlScheme"].AsString(null);
                    if (sch != null)
                    {
                        if (!Enum.TryParse<EnumControlScheme>(sch, out scheme)) scheme = EnumControlScheme.Hold;
                        else break;
                    }
                }
            }
        }

        private bool TaskManager_OnShouldExecuteTask(IAiTask task)
        {
            if (task is AiTaskWander && api.World.Calendar.TotalHours - lastDismountTotalHours < 24) return false;

            return !Seats.Any(seat => seat.Passenger != null);
        }

        bool wasPaused;

        public void OnRenderFrame(float dt, EnumRenderStage stage)
        {
            if (!wasPaused && capi.IsGamePaused)
            {
                trotSound?.Pause();
                gallopSound?.Pause();
            }
            if (wasPaused && !capi.IsGamePaused)
            {
                if (trotSound?.IsPaused == true) trotSound?.Start();
                if (gallopSound?.IsPaused == true) gallopSound?.Start();
            }

            wasPaused = capi.IsGamePaused;

            if (capi.IsGamePaused) return;


            UpdateAngleAndMotion(dt);
        }

        protected virtual void UpdateAngleAndMotion(float dt)
        {
            // Ignore lag spikes
            dt = Math.Min(0.5f, dt);

            float step = GlobalConstants.PhysicsFrameTime;
            var motion = SeatsToMotion(step);

            if (jumpNow)
            {
                UpdateRidingState();
            }

            ForwardSpeed = Math.Sign(motion.X);

            float yawMultiplier = CurrentGait switch
            {
                GaitState.Walk => 3f,
                GaitState.Canter => 2f,
                GaitState.Gallop => 1.5f,
                _ => 3f
            };

            AngularVelocity = motion.Y * yawMultiplier;

            entity.SidedPos.Yaw += (float)motion.Y * dt * 30f;
            entity.SidedPos.Yaw = entity.SidedPos.Yaw % GameMath.TWOPI;

            if (entity.World.ElapsedMilliseconds - lastJumpMs < 2000 && entity.World.ElapsedMilliseconds - lastJumpMs > 200 && entity.OnGround)
            {
                eagent.StopAnimation("jump");
            }
        }

        bool prevForwardKey, prevBackwardKey, prevSprintKey;

        bool forward, backward;
        public virtual Vec2d SeatsToMotion(float dt)
        {
            int seatsRowing = 0;

            double linearMotion = 0;
            double angularMotion = 0;

            jumpNow = false;
            coyoteTimer -= dt;

            Controller = null;

            foreach (var seat in Seats)
            {
                if (entity.OnGround) coyoteTimer = 0.15f;

                if (seat.Passenger == null || !seat.Config.Controllable) continue;

                var eplr = seat.Passenger as EntityPlayer;

                if (eplr != null)
                {
                    eplr.Controls.LeftMouseDown = seat.Controls.LeftMouseDown;
                    eplr.HeadYawLimits = new AngleConstraint(entity.Pos.Yaw + seat.Config.MountRotation.Y * GameMath.DEG2RAD, GameMath.PIHALF);
                    eplr.BodyYawLimits = new AngleConstraint(entity.Pos.Yaw + seat.Config.MountRotation.Y * GameMath.DEG2RAD, GameMath.PIHALF);
                }

                if (Controller != null) continue;
                Controller = seat.Passenger;

                var controls = seat.Controls;
                bool canride = true;
                bool canturn = true;

                if (CanRide != null && (controls.Jump || controls.TriesToMove))
                {
                    foreach (EquusCanRideDelegate dele in CanRide.GetInvocationList())
                    {
                        if (!dele(seat, out string errMsg))
                        {
                            if (capi != null && seat.Passenger == capi.World.Player.Entity)
                            {
                                capi.TriggerIngameError(this, "cantride", Lang.Get("cantride-" + errMsg));
                            }
                            canride = false;
                            break;
                        }
                    }
                }

                if (CanTurn != null && (controls.Left || controls.Right))
                {
                    foreach (EquusCanRideDelegate dele in CanTurn.GetInvocationList())
                    {
                        if (!dele(seat, out string errMsg))
                        {
                            if (capi != null && seat.Passenger == capi.World.Player.Entity)
                            {
                                capi.TriggerIngameError(this, "cantride", Lang.Get("cantride-" + errMsg));
                            }
                            canturn = false;
                            break;
                        }
                    }
                }

                if (!canride) continue;


                // Only able to jump every 1000ms. Only works while on the ground.
                if (controls.Jump && entity.World.ElapsedMilliseconds - lastJumpMs > 1000 && entity.Alive && (entity.OnGround || coyoteTimer > 0))
                {
                    lastJumpMs = entity.World.ElapsedMilliseconds;
                    jumpNow = true;
                }

                if (scheme == EnumControlScheme.Hold && !controls.TriesToMove)
                {
                    continue;
                }

                float str = ++seatsRowing == 1 ? 1 : 0.5f;

                // Handle gait switching via sprint button
                bool nowForwards = controls.Forward;
                bool nowBackwards = controls.Backward;
                bool nowSprint = controls.CtrlKey;

                // Detect fresh forward press
                bool forwardPressed = nowForwards && !prevForwardKey;
                bool backwardPressed = nowBackwards && !prevBackwardKey;
                bool sprintPressed = nowSprint && !prevSprintKey;
                long nowMs = entity.World.ElapsedMilliseconds;

                if (sprintPressed && nowMs - lastGaitChangeMs > 300)
                {
                    CurrentGait = CurrentGait switch
                    {
                        GaitState.Walk => GaitState.Canter,
                        GaitState.Canter => GaitState.Gallop,
                        GaitState.Gallop => GaitState.Walk,
                        _ => GaitState.Walk
                    };

                    lastGaitChangeMs = nowMs;
                }

                prevSprintKey = nowSprint;

                if (scheme == EnumControlScheme.Hold)
                {
                    forward = controls.Forward;
                    backward = controls.Backward;

                    // Don't allow backwards canter/gallop
                    if (backward) CurrentGait = GaitState.Walk;
                }
                else
                {
                    // Transition from idle to forward
                    if (!forward && !backward && forwardPressed)
                    {
                        forward = true;
                        CurrentGait = GaitState.Walk;
                    }
                    // Switch from forward to backward
                    else if (forward && backwardPressed)
                    {
                        forward = false;
                        CurrentGait = GaitState.Walk;
                    }
                    // Transition from idle to backward
                    else if (!backward && backwardPressed)
                    {
                        backward = true;
                        CurrentGait = GaitState.Walk;
                    }
                    // Switch from backward to forward
                    else if (backward && forwardPressed)
                    {
                        backward = false;
                        CurrentGait = GaitState.Walk;
                    }

                    prevForwardKey = nowForwards;
                    prevBackwardKey = nowBackwards;
                }

                if (canturn && (controls.Left || controls.Right))
                {
                    float dir = controls.Left ? 1 : -1;
                    angularMotion += str * dir * dt;
                }
                if (forward || backward)
                {
                    float dir = forward ? 1 : -1;
                    linearMotion += str * dir * dt * 2f;
                }
            }

            return new Vec2d(linearMotion, angularMotion);
        }

        protected void UpdateRidingState()
        {
            if (!AnyMounted()) return;

            bool wasMidJump = IsInMidJump;
            IsInMidJump &= (entity.World.ElapsedMilliseconds - lastJumpMs < 500 || !entity.OnGround) && !entity.Swimming;

            if (wasMidJump && !IsInMidJump)
            {
                var meta = rideableconfig.Controls["jump"];
                foreach (var seat in Seats) seat.Passenger?.AnimManager?.StopAnimation(meta.RiderAnim.Animation);
                eagent.AnimManager.StopAnimation(meta.Animation);
            }

            eagent.Controls.Backward = ForwardSpeed < 0;
            eagent.Controls.Forward = ForwardSpeed >= 0;
            eagent.Controls.Sprint = CurrentGait == GaitState.Gallop && ForwardSpeed > 0;

            string nowTurnAnim = null;
            if (ForwardSpeed >= 0)
            {
                if (AngularVelocity > 0.001) nowTurnAnim = "turn-left";                
                else if (AngularVelocity < -0.001) nowTurnAnim = "turn-right";
            }
            if (nowTurnAnim != curTurnAnim)
            {
                if (curTurnAnim != null) eagent.StopAnimation(curTurnAnim);
                eagent.StartAnimation((ForwardSpeed == 0 ? "idle-" : "") + (curTurnAnim = nowTurnAnim));
            }

            ControlMeta nowControlMeta;
            shouldMove = ForwardSpeed != 0;

            if (!shouldMove && !jumpNow)
            {
                if (curControlMeta != null) Stop();
                curAnim = rideableconfig.Controls[eagent.Swimming ? "swim" : "idle"].RiderAnim;
                nowControlMeta = eagent.Swimming ? rideableconfig.Controls["swim"] : null;
            }
            else
            {
                string controlCode = eagent.Controls.Backward ? "walkback" : "walk";

                switch (CurrentGait)
                {
                    case GaitState.Walk:
                        controlCode = eagent.Controls.Backward ? "walkback" : "walk";
                        break;
                    case GaitState.Canter:
                        controlCode = "canter";
                        break;
                    case GaitState.Gallop:
                        controlCode = "sprint";
                        break;
                }

                if (eagent.Swimming) controlCode = "swim";

                nowControlMeta = rideableconfig.Controls[controlCode];
                eagent.Controls.Jump = jumpNow;

                if (jumpNow)
                {
                    IsInMidJump = true;
                    jumpNow = false;
                    var esr = eagent.Properties.Client.Renderer as EntityShapeRenderer;
                    if (esr != null) esr.LastJumpMs = capi.InWorldEllapsedMilliseconds;

                    nowControlMeta = rideableconfig.Controls["jump"];
                    nowControlMeta.EaseOutSpeed = (ForwardSpeed != 0) ? 30 : 40;

                    foreach (var seat in Seats) seat.Passenger?.AnimManager?.StartAnimation(nowControlMeta.RiderAnim);
                    EntityPlayer entityPlayer = entity as EntityPlayer;
                    IPlayer player = entityPlayer?.World.PlayerByUid(entityPlayer.PlayerUID);
                    entity.PlayEntitySound("jump", player, false);
                }
                else
                {
                    curAnim = nowControlMeta.RiderAnim;
                }
            }

            if (nowControlMeta != curControlMeta)
            {
                if (curControlMeta != null && curControlMeta.Animation != "jump")
                {
                    eagent.StopAnimation(curControlMeta.Animation);
                }

                curControlMeta = nowControlMeta;
                ModSystem.Logger.Notification($"Side: {api.Side}, Meta: {nowControlMeta.Code}");
                eagent.AnimManager.StartAnimation(nowControlMeta);
            }

            if (api.Side == EnumAppSide.Server)
            {
                eagent.Controls.Sprint = false; // Uh, why does the elk speed up 2x with this on?
            }
        }

        /// <summary>
        /// Check for stamina triggered changes to gait then sync to server
        /// </summary>
        /// <param name="dt">tick time</param>
        public void StaminaGaitCheckandSync(float dt)
        {
            if (capi is null) return;
            if (api.Side != EnumAppSide.Client) return;

            timeSinceLastGaitCheck += dt;

            // Check once a second
            if (timeSinceLastGaitCheck >= 1f)
            {
            if (CurrentGait == GaitState.Gallop && !eagent.Swimming)
            {
                    GaitState nextGait = CurrentGait;
                    if (ebs.Exhausted && capi?.World.Rand.NextDouble() > 0.1f) 
                    { 
                        /* maybe buck */  
                    }

                    int syncPacketId;
                if (ebs.Stamina < 10)
                    {
                        nextGait = GaitState.Walk;
                        syncPacketId = 9999;
                    }
                    else if (capi.World.Rand.NextDouble() < GetStaminaDeficitMultiplier(ebs.Stamina, ebs.MaxStamina))
                    {
                        nextGait = GaitState.Canter;
                        syncPacketId = 9998;
                    }
                    else
                    {
                        nextGait = GaitState.Gallop;
                        syncPacketId = 9997;
                    }

                    // If gait changes sync it to the server 
                    if (nextGait != CurrentGait)
                    {
                        CurrentGait = nextGait;
                        capi.Network.SendEntityPacket(entity.EntityId, syncPacketId);
                    }
                }

                timeSinceLastGaitCheck = 0;
            }
        }

        // For syncing gait from client to server
        public override void OnReceivedClientPacket(IServerPlayer player, int packetid, byte[] data, ref EnumHandling handled)
        {
            switch (packetid)
            {
                case 9999:
                    CurrentGait = GaitState.Walk;
                    UpdateRidingState();
                    break;
                case 9998:
                    CurrentGait = GaitState.Canter;
                    UpdateRidingState();
                    break;
                case 9997:
                    CurrentGait = GaitState.Gallop;
                    UpdateRidingState();
                    break;
            };
            handled = EnumHandling.Handled;
            }

        /// <summary>
        /// Returns a value on a quadratic curve as stamina drops below 50%
        /// </summary>
        /// <param name="currentStamina"></param>
        /// <param name="maxStamina"></param>
        /// <returns></returns>
        public static float GetStaminaDeficitMultiplier(float currentStamina, float maxStamina)
        {
            float midpoint = maxStamina * 0.5f;

            if (currentStamina >= midpoint)
                return 0f;

            float deficit = 1f - (currentStamina / midpoint);  // 0 at midpoint, 1 at 0 stamina
            return deficit * deficit;  // Quadratic curve for gradual increase
        }

        public void DismountViolently()
        {
            var meta = rideableconfig.Controls["sprint"];
            bool unmounted = false;
            foreach (var seat in Seats)
            {
                EntityPlayer rider = seat.Passenger as EntityPlayer;
                if (rider is null) return;

                seat.DoTeleportOnUnmount = false;
                unmounted = rider.TryUnmount();
                rider?.AnimManager?.StopAnimation(meta.RiderAnim.Animation);
                rider?.AnimManager?.StartAnimation("knockbackland");
            }

            if (unmounted)
            {
                Stop();
            }
        }

        public void Stop()
        {
            CurrentGait = GaitState.Walk;
            eagent.Controls.StopAllMovement();
            eagent.Controls.WalkVector.Set(0, 0, 0);
            eagent.Controls.FlyVector.Set(0, 0, 0);
            shouldMove = false;
            if (curControlMeta != null && curControlMeta.Animation != "jump")
            {
                eagent.StopAnimation(curControlMeta.Animation);
            }
            curControlMeta = null;
            eagent.StartAnimation("idle");
        }

        public override void OnGameTick(float dt)
        {
            timeSinceLastLog += dt;

            if (false && timeSinceLastLog > 5)
            {
                timeSinceLastLog = 0;
                foreach (var seat in Seats)
                {
                    if (DebugMode && seat.Passenger != null) ModSystem.Logger.Notification($"Current Gait: {CurrentGait}");
                }
            }

            if (api.Side == EnumAppSide.Server)
            {
                UpdateAngleAndMotion(dt);
            }

            StaminaGaitCheckandSync(dt);

            UpdateRidingState();

            if (!AnyMounted() && eagent.Controls.TriesToMove && eagent?.MountedOn != null)
            {
                eagent.TryUnmount();
            }

            if (shouldMove)
            {
                Move(dt, eagent.Controls, curControlMeta.MoveSpeed);
            }
            else
            {
                if (entity.Swimming) eagent.Controls.FlyVector.Y = 0.2;
            }

            UpdateSoundState(dt);
        }

        float notOnGroundAccum;
        private void UpdateSoundState(float dt)
        {
            if (capi == null) return;

            if (eagent.OnGround) notOnGroundAccum = 0;
            else notOnGroundAccum += dt;

            bool nowtrot = shouldMove && CurrentGait != GaitState.Gallop && notOnGroundAccum < 0.2;
            bool nowgallop = shouldMove && CurrentGait == GaitState.Gallop && notOnGroundAccum < 0.2;

            bool wastrot = trotSound != null && trotSound.IsPlaying;
            bool wasgallop = gallopSound != null && gallopSound.IsPlaying;

            trotSound?.SetPosition((float)entity.Pos.X, (float)entity.Pos.Y, (float)entity.Pos.Z);
            gallopSound?.SetPosition((float)entity.Pos.X, (float)entity.Pos.Y, (float)entity.Pos.Z);

            if (nowtrot != wastrot)
            {
                if (nowtrot)
                {
                    if (trotSound == null)
                    {
                        trotSound = capi.World.LoadSound(new SoundParams()
                        {
                            Location = new AssetLocation("sounds/creature/hooved/trot"),
                            DisposeOnFinish = false,
                            Position = entity.Pos.XYZ.ToVec3f(),
                            ShouldLoop = true,
                        });
                    }

                    trotSound.Start();

                }
                else
                {
                    trotSound.Stop();
                }
            }

            if (nowgallop != wasgallop)
            {
                if (nowgallop)
                {
                    if (gallopSound == null)
                    {
                        gallopSound = capi.World.LoadSound(new SoundParams()
                        {
                            Location = new AssetLocation("sounds/creature/hooved/gallop"),
                            DisposeOnFinish = false,
                            Position = entity.Pos.XYZ.ToVec3f(),
                            ShouldLoop = true,
                        });
                    }
                    gallopSound.Start();
                }
                else
                {
                    gallopSound.Stop();
                }
            }
        }

        private void Move(float dt, EntityControls controls, float nowMoveSpeed)
        {
            double cosYaw = Math.Cos(entity.Pos.Yaw);
            double sinYaw = Math.Sin(entity.Pos.Yaw);
            controls.WalkVector.Set(sinYaw, 0, cosYaw);
            controls.WalkVector.Mul(nowMoveSpeed * GlobalConstants.OverallSpeedMultiplier * StaminaSpeedMultiplier * ForwardSpeed);

            // Make it walk along the wall, but not walk into the wall, which causes it to climb
            if (entity.Properties.RotateModelOnClimb && controls.IsClimbing && entity.ClimbingOnFace != null && entity.Alive)
            {
                BlockFacing facing = entity.ClimbingOnFace;
                if (Math.Sign(facing.Normali.X) == Math.Sign(controls.WalkVector.X))
                {
                    controls.WalkVector.X = 0;
                }

                if (Math.Sign(facing.Normali.Z) == Math.Sign(controls.WalkVector.Z))
                {
                    controls.WalkVector.Z = 0;
                }
            }

            if (entity.Swimming)
            {
                controls.FlyVector.Set(controls.WalkVector);

                Vec3d pos = entity.Pos.XYZ;
                Block inblock = entity.World.BlockAccessor.GetBlock((int)pos.X, (int)(pos.Y), (int)pos.Z, BlockLayersAccess.Fluid);
                Block aboveblock = entity.World.BlockAccessor.GetBlock((int)pos.X, (int)(pos.Y + 1), (int)pos.Z, BlockLayersAccess.Fluid);
                float waterY = (int)pos.Y + inblock.LiquidLevel / 8f + (aboveblock.IsLiquid() ? 9 / 8f : 0);
                float bottomSubmergedness = waterY - (float)pos.Y;

                // 0 = at swim line
                // 1 = completely submerged
                float swimlineSubmergedness = GameMath.Clamp(bottomSubmergedness - ((float)entity.SwimmingOffsetY), 0, 1);
                swimlineSubmergedness = Math.Min(1, swimlineSubmergedness + 0.075f);
                controls.FlyVector.Y = GameMath.Clamp(controls.FlyVector.Y, 0.002f, 0.004f) * swimlineSubmergedness * 3;

                if (entity.CollidedHorizontally)
                {
                    controls.FlyVector.Y = 0.05f;
                }

                eagent.Pos.Motion.Y += (swimlineSubmergedness - 0.1) / 300.0;
            }
        }

        public override string PropertyName() => "rideable";
        public void Dispose() { }

        public void DidUnnmount(EntityAgent entityAgent)
        {
            Stop();

            lastDismountTotalHours = entity.World.Calendar.TotalHours;
            foreach (var meta in rideableconfig.Controls.Values)
            {
                if (meta.RiderAnim?.Animation != null)
                {
                    entityAgent.StopAnimation(meta.RiderAnim.Animation);
                }
            }

            if (eagent.Swimming)
            {
                eagent.StartAnimation("swim");
            }
        }

        public void DidMount(EntityAgent entityAgent)
        {
            UpdateControlScheme();
        }
    }

    public class ElkAnimationManager : AnimationManager
    {
        public string animAppendix = "-antlers";

        public override void ResetAnimation(string animCode)
        {
            base.ResetAnimation(animCode);
            base.ResetAnimation(animCode + animAppendix);
        }

        public override void StopAnimation(string code)
        {
            base.StopAnimation(code);
            base.StopAnimation(code + animAppendix);
        }

        public override bool StartAnimation(AnimationMetaData animdata)
        {
            return base.StartAnimation(animdata);
        }
    }


    public class ControlMeta : AnimationMetaData
    {
        public float MoveSpeed;
        public AnimationMetaData RiderAnim;
    }

    public class RideableConfig
    {
        public int MinGeneration;
        public Dictionary<string, ControlMeta> Controls;
    }

}