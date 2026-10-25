using System;
using System.Collections.Generic;
using Echelon.Presentation.Models;
using UnityEngine;

namespace Echelon.Unity.Presentation.Elements
{
    public sealed class ElementVisualController
    {
        private const float AliveAltitudeRate = 12.0f;
        private const float WreckAltitudeRate = 30.0f;
        private const float PlaneAltitudeRate = 200.0f;
        private const float PlaneBankPerTurnDegrees = 1.2f;
        private const float PlaneMaxBankDegrees = 55.0f;
        private const float PlaneBankRateDegrees = 90.0f;
        private const float RotorRadiansPerSecond = 28.0f;
        private const float PausedRotorRadiansPerSecond = 4.0f;

        private readonly Material[] hullMaterials;
        private readonly Material[] turretMaterials;
        private readonly Vector3 turretPivot;
        private ElementAppearance appearance = ElementAppearance.Unknown;
        private float altitude = -1.0f;
        private float turretYaw;
        private float recoil;
        private int lastFiredTick = ElementVisualState.NoTick;
        private float rotorAngle;
        private float bank;
        private Vector2 tiltPosition;
        private bool hasTiltPosition;
        private bool needsRootName = true;

        public EquipmentModelHandle Model { get; private set; }
        public ElementVisualState State { get; private set; }
        public GameObject Root { get { return Model.Root; } }
        public bool IsVisible { get; private set; }
        public float SmoothedAltitude { get { return altitude; } }
        public float TurretYaw { get { return turretYaw; } }
        public float Recoil { get { return recoil; } }
        public float RotorAngle { get { return rotorAngle; } }

        public ElementVisualController(EquipmentModelHandle model, ElementVisualState initialState, Transform parent = null)
        {
            if (model == null)
                throw new ArgumentNullException("model");
            if (initialState == null)
                throw new ArgumentNullException("initialState");

            Model = model;
            State = initialState.Snapshot();
            hullMaterials = model.HullRenderer.sharedMaterials;
            turretMaterials = model.TurretRenderer != null ? model.TurretRenderer.sharedMaterials : null;
            turretPivot = model.Turret != null ? model.Turret.localPosition : Vector3.zero;
            model.RootTransform.SetParent(parent, false);
            model.Root.name = "E" + State.Id;
            needsRootName = false;
            model.Root.SetActive(false);
        }

        public void Synchronize(ElementVisualState state)
        {
            if (state == null)
                throw new ArgumentNullException("state");
            if (needsRootName || State.Id != state.Id)
            {
                Root.name = "E" + state.Id;
                needsRootName = false;
            }
            State.CopyFrom(state);
        }

        public void UpdateVisuals(
            float realDeltaSeconds,
            float gameDeltaSeconds,
            float interpolationAlpha,
            int currentTick,
            string fogSide,
            ElementVisualState target,
            Func<Vector2, float> groundHeight = null)
        {
            float realDelta = Mathf.Max(0.0f, realDeltaSeconds);
            float gameDelta = Mathf.Max(0.0f, gameDeltaSeconds);
            bool visible = ElementVisualMath.IsVisible(State, fogSide, currentTick);
            IsVisible = visible;
            if (Root.activeSelf != visible)
                Root.SetActive(visible);
            if (!visible)
                return;

            Vector2 position = ElementVisualMath.InterpolatePosition(
                State.PreviousPosition, State.Position, interpolationAlpha);
            float heading = ElementVisualMath.InterpolateHeading(
                State.PreviousHeading, State.Heading, interpolationAlpha);
            float ground = groundHeight != null ? groundHeight(position) : State.TerrainHeight;
            float height = ground;
            if (State.IsAircraft)
            {
                UpdateAir(realDelta, gameDelta);
                height += altitude;
            }

            Root.transform.position = new Vector3(position.x, height, position.y);
            Root.transform.rotation = Quaternion.AngleAxis(
                ElementVisualMath.HeadingToYaw(heading) * Mathf.Rad2Deg, Vector3.up);

            if (State.IsVehicle && !State.IsAircraft)
                UpdateTilt(position, heading, groundHeight);
            UpdateAppearance();
            if (Model.Turret != null)
                UpdateTurret(position, heading, gameDelta, target);
        }

        internal void ResetForPool()
        {
            IsVisible = false;
            Root.SetActive(false);
            Root.name = "Pooled_" + Model.ElementId;
            needsRootName = true;
            Root.transform.localPosition = Vector3.zero;
            Root.transform.localRotation = Quaternion.identity;
            Root.transform.localScale = Vector3.one;
            Model.Tilt.localRotation = Quaternion.identity;
            if (Model.Turret != null)
            {
                Model.Turret.localPosition = turretPivot;
                Model.Turret.localRotation = Quaternion.identity;
            }
            if (Model.Rotor != null)
                Model.Rotor.localRotation = Quaternion.identity;
            if (Model.Rotor2 != null)
                Model.Rotor2.localRotation = Quaternion.identity;

            Model.HullRenderer.sharedMaterials = hullMaterials;
            if (Model.TurretRenderer != null)
                Model.TurretRenderer.sharedMaterials = turretMaterials;
            appearance = ElementAppearance.Normal;
            altitude = -1.0f;
            turretYaw = 0.0f;
            recoil = 0.0f;
            lastFiredTick = ElementVisualState.NoTick;
            rotorAngle = 0.0f;
            bank = 0.0f;
            tiltPosition = Vector2.zero;
            hasTiltPosition = false;
        }

        private void UpdateAir(float realDelta, float gameDelta)
        {
            float wantedAltitude = State.IsAlive ? Mathf.Max(0.0f, State.Altitude) : 0.0f;
            if (altitude < 0.0f)
                altitude = wantedAltitude;
            float delta = Mathf.Max(gameDelta, realDelta * 0.2f);
            float climbRate = !State.IsAlive ? WreckAltitudeRate : State.IsPlane ? PlaneAltitudeRate : AliveAltitudeRate;
            altitude = ElementVisualMath.MoveTowards(altitude, wantedAltitude, delta * climbRate);

            if (!State.IsAlive)
            {
                Model.Tilt.localRotation = Quaternion.Euler(0.25f * Mathf.Rad2Deg, 0.0f, 0.45f * Mathf.Rad2Deg);
                return;
            }

            if (State.IsPlane)
            {
                // 고정익은 선회 방향으로 기운다. 틱당 방위 변화로 선회율을 구한다(틱 0.1초).
                float turnPerSecond = Mathf.DeltaAngle(State.PreviousHeading * Mathf.Rad2Deg, State.Heading * Mathf.Rad2Deg) * 10.0f;
                float wantedBank = Mathf.Clamp(turnPerSecond * PlaneBankPerTurnDegrees, -PlaneMaxBankDegrees, PlaneMaxBankDegrees);
                bank = ElementVisualMath.MoveTowards(bank, wantedBank, Mathf.Max(gameDelta, realDelta * 0.2f) * PlaneBankRateDegrees);
                Model.Tilt.localRotation = Quaternion.Euler(0.0f, 0.0f, bank);
                return;
            }

            float speedPitch = -Mathf.Clamp(State.Velocity.magnitude / 70.0f, 0.0f, 0.22f);
            Model.Tilt.localRotation = Quaternion.Euler(speedPitch * Mathf.Rad2Deg, 0.0f, 0.0f);
            float rotorStep = gameDelta > 0.0f
                ? gameDelta * RotorRadiansPerSecond
                : realDelta * PausedRotorRadiansPerSecond;
            rotorAngle += rotorStep;
            if (Model.Rotor != null)
                Model.Rotor.localRotation = Quaternion.AngleAxis(rotorAngle * Mathf.Rad2Deg, Vector3.up);
            if (Model.Rotor2 != null)
                Model.Rotor2.localRotation = Quaternion.AngleAxis(-rotorAngle * Mathf.Rad2Deg, Vector3.up);
        }

        private void UpdateTilt(Vector2 position, float heading, Func<Vector2, float> groundHeight)
        {
            if (hasTiltPosition && (position - tiltPosition).sqrMagnitude < 0.25f)
                return;
            tiltPosition = position;
            hasTiltPosition = true;

            float pitch;
            float roll;
            Vector2 forward = ElementVisualMath.Forward(heading);
            Vector2 right = ElementVisualMath.Right(forward);
            if (groundHeight != null)
            {
                float dz = groundHeight(position + forward * 3.0f) - groundHeight(position - forward * 3.0f);
                float dx = groundHeight(position + right * 1.8f) - groundHeight(position - right * 1.8f);
                pitch = Mathf.Atan2(dz, 6.0f);
                roll = Mathf.Atan2(-dx, 3.6f);
            }
            else
            {
                Vector2 angles = ElementVisualMath.TerrainAnglesFromNormal(State.TerrainNormal, heading);
                pitch = angles.x;
                roll = angles.y;
            }
            Model.Tilt.localRotation = Quaternion.Euler(pitch * Mathf.Rad2Deg, 0.0f, roll * Mathf.Rad2Deg);
        }

        private void UpdateTurret(Vector2 position, float heading, float gameDelta, ElementVisualState target)
        {
            if (State.IsAlive)
            {
                float wantedYaw = 0.0f;
                if (State.TargetId >= 0 && target != null && target.OnMap)
                    wantedYaw = ElementVisualMath.TargetTurretYaw(position, heading, target.Position);

                float step = ElementVisualMath.TurretRadiansPerSecond * gameDelta;
                turretYaw = ElementVisualMath.MoveTowardsAngle(turretYaw, wantedYaw, step);
                if (State.LastFiredTick != lastFiredTick)
                {
                    if (lastFiredTick > ElementVisualState.NoTick && State.LastFiredTick > lastFiredTick)
                        recoil = 1.0f;
                    lastFiredTick = State.LastFiredTick;
                }
                recoil = Mathf.Max(recoil - ElementVisualMath.RecoilDecayPerSecond * gameDelta, 0.0f);
                var back = new Vector3(Mathf.Sin(turretYaw), 0.0f, Mathf.Cos(turretYaw))
                    * (ElementVisualMath.RecoilMeters * recoil);
                Model.Turret.localRotation = Quaternion.AngleAxis(turretYaw * Mathf.Rad2Deg, Vector3.up);
                Model.Turret.localPosition = turretPivot + back;
            }
            else if (State.State == ElementVisualCondition.Destroyed)
            {
                float factor = ElementVisualMath.DestroyedTurretFactor(State.Id);
                Model.Turret.localRotation = Quaternion.Euler(
                    (0.18f + factor * 0.25f) * Mathf.Rad2Deg,
                    (turretYaw + (factor - 0.5f) * 1.2f) * Mathf.Rad2Deg,
                    ((factor - 0.5f) * 0.4f) * Mathf.Rad2Deg);
                Model.Turret.localPosition = turretPivot + new Vector3(0.0f, 0.35f + factor * 0.4f, 0.0f);
            }
        }

        private void UpdateAppearance()
        {
            ElementAppearance wanted = !State.IsAlive
                ? ElementAppearance.Dead
                : State.State == ElementVisualCondition.Damaged
                    ? ElementAppearance.Damaged
                    : ElementAppearance.Normal;
            if (wanted == appearance)
                return;
            appearance = wanted;
            ApplyAppearance(Model.HullRenderer, Model.HullRoles, hullMaterials);
            if (Model.TurretRenderer != null)
                ApplyAppearance(Model.TurretRenderer, Model.TurretRoles, turretMaterials);
        }

        private void ApplyAppearance(
            MeshRenderer renderer,
            IReadOnlyList<EquipmentMeshRole> roles,
            Material[] normalMaterials)
        {
            if (appearance == ElementAppearance.Normal)
            {
                renderer.sharedMaterials = normalMaterials;
                return;
            }

            var materials = new Material[roles.Count];
            for (int i = 0; i < roles.Count; i++)
            {
                if (appearance == ElementAppearance.Damaged && roles[i] != EquipmentMeshRole.Camo)
                    materials[i] = normalMaterials[i];
                else
                    materials[i] = ElementMaterialPalette.Get(roles[i], State.Side, State.IsInfantry, appearance);
            }
            renderer.sharedMaterials = materials;
        }
    }
}
