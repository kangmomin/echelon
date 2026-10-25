using UnityEngine;
using UnityEngine.InputSystem;

namespace Echelon.Unity.Presentation
{
    [DisallowMultipleComponent]
    public sealed class RtsCameraController : MonoBehaviour
    {
        public const float MinDistance = 80f;
        public const float MaxDistance = 6000f;
        public const float PanSpeed = 1.1f;
        public const float EdgeMarginPixels = 6f;
        public const float ZoomFactor = 1.15f;
        public const float MinPitchDegrees = 25f;
        public const float MaxPitchDegrees = 85f;
        public const float FieldOfViewDegrees = 50f;
        public const float NearClipMeters = 1f;
        public const float FarClipMeters = 30000f;

        private const float YawRadiansPerPixel = 0.005f;
        private const float PitchRadiansPerPixel = 0.004f;

        [SerializeField] private UnityEngine.Camera targetCamera;
        [SerializeField] private float distance = 1400f;
        [SerializeField] private float yawDegrees;
        [SerializeField] private float pitchDegrees = 55f;
        [SerializeField] private Vector2 simFocus = new Vector2(2000f, 2000f);
        [SerializeField] private MapBounds bounds = new MapBounds(Vector2.zero, new Vector2(4000f, 4000f));
        [SerializeField] private bool edgeScrollEnabled = true;
        [SerializeField] private bool inputEnabled = true;

        private HeightSampler heightSampler;
        private bool rotating;

        public UnityEngine.Camera TargetCamera
        {
            get => targetCamera;
            set
            {
                targetCamera = value;
                ConfigureCamera();
                Apply();
            }
        }

        public float Distance
        {
            get => distance;
            set
            {
                distance = Mathf.Clamp(value, MinDistance, MaxDistance);
                Apply();
            }
        }

        public float YawDegrees
        {
            get => yawDegrees;
            set
            {
                yawDegrees = WrapYaw(value);
                Apply();
            }
        }

        public float PitchDegrees
        {
            get => pitchDegrees;
            set
            {
                pitchDegrees = Mathf.Clamp(value, MinPitchDegrees, MaxPitchDegrees);
                Apply();
            }
        }

        public Vector2 SimFocus
        {
            get => simFocus;
            set => FocusOnSim(value);
        }

        public Vector3 WorldFocus
        {
            get => ViewCoords.ToWorld(simFocus, GroundHeightAtFocus());
            set => FocusOnWorld(value);
        }

        public MapBounds Bounds
        {
            get => bounds;
            set
            {
                bounds = value;
                Apply();
            }
        }

        public HeightSampler SampleHeight
        {
            get => heightSampler;
            set
            {
                heightSampler = value;
                Apply();
            }
        }

        public bool EdgeScrollEnabled
        {
            get => edgeScrollEnabled;
            set => edgeScrollEnabled = value;
        }

        public bool InputEnabled
        {
            get => inputEnabled;
            set
            {
                inputEnabled = value;
                if (!value)
                    rotating = false;
            }
        }

        public bool WasdInputBlocked { get; set; }
        public bool PointerInputBlocked { get; set; }

        public float HeightAboveGround => distance * Mathf.Sin(pitchDegrees * Mathf.Deg2Rad);

        private void Awake()
        {
            if (targetCamera == null)
                targetCamera = GetComponent<UnityEngine.Camera>();
            if (targetCamera == null)
            {
                var cameraObject = new GameObject("RtsCamera");
                cameraObject.transform.SetParent(transform, false);
                targetCamera = cameraObject.AddComponent<UnityEngine.Camera>();
            }
            ConfigureCamera();
            Apply();
        }

        private void OnValidate()
        {
            distance = Mathf.Clamp(distance, MinDistance, MaxDistance);
            yawDegrees = WrapYaw(yawDegrees);
            pitchDegrees = Mathf.Clamp(pitchDegrees, MinPitchDegrees, MaxPitchDegrees);
            bounds = new MapBounds(bounds.Min, bounds.Size);
            simFocus = bounds.Clamp(simFocus);
            ConfigureCamera();
            Apply();
        }

        private void OnDisable()
        {
            rotating = false;
        }

        private void Update()
        {
            if (!inputEnabled)
                return;

            Vector2 movement = ReadKeyboardMovement();
            if (edgeScrollEnabled && Application.isFocused)
                movement += ReadEdgeMovement();
            if (movement != Vector2.zero)
                Pan(movement, Time.deltaTime);

            if (Application.isFocused)
                ReadMouseInput();
            else
                rotating = false;
        }

        public void Setup(MapBounds mapBounds, HeightSampler sampler, Vector2 startFocus)
        {
            bounds = mapBounds;
            heightSampler = sampler;
            simFocus = bounds.Clamp(startFocus);
            Apply();
        }

        public void FocusOnSim(Vector2 focus)
        {
            simFocus = bounds.Clamp(focus);
            Apply();
        }

        public void FocusOnWorld(Vector3 focus)
        {
            FocusOnSim(ViewCoords.ToSim(focus));
        }

        public void Pan(Vector2 screenMovement, float deltaTime)
        {
            if (screenMovement == Vector2.zero)
                return;

            float yaw = yawDegrees * Mathf.Deg2Rad;
            var forward = new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw));
            var right = new Vector2(Mathf.Cos(yaw), -Mathf.Sin(yaw));
            Vector2 movement = right * screenMovement.x + forward * screenMovement.y;
            simFocus += movement.normalized * distance * PanSpeed * Mathf.Max(0f, deltaTime);
            Apply();
        }

        public void ZoomIn()
        {
            Distance = distance / ZoomFactor;
        }

        public void ZoomOut()
        {
            Distance = distance * ZoomFactor;
        }

        public void Orbit(Vector2 mouseDelta)
        {
            yawDegrees = WrapYaw(yawDegrees + mouseDelta.x * YawRadiansPerPixel * Mathf.Rad2Deg);
            pitchDegrees = Mathf.Clamp(
                pitchDegrees - mouseDelta.y * PitchRadiansPerPixel * Mathf.Rad2Deg,
                MinPitchDegrees,
                MaxPitchDegrees);
            Apply();
        }

        private Vector2 ReadKeyboardMovement()
        {
            Vector2 movement = Vector2.zero;
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return movement;

            bool modified = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed
                || keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed;
            bool wasd = !WasdInputBlocked && !modified;

            if (keyboard.leftArrowKey.isPressed || (wasd && keyboard.aKey.isPressed))
                movement.x -= 1f;
            if (keyboard.rightArrowKey.isPressed || (wasd && keyboard.dKey.isPressed))
                movement.x += 1f;
            if (keyboard.upArrowKey.isPressed || (wasd && keyboard.wKey.isPressed))
                movement.y += 1f;
            if (keyboard.downArrowKey.isPressed || (wasd && keyboard.sKey.isPressed))
                movement.y -= 1f;
            return movement;
        }

        private static Vector2 ReadEdgeMovement()
        {
            Vector2 movement = Vector2.zero;
            Mouse mouse = Mouse.current;
            if (mouse == null)
                return movement;

            Vector2 position = mouse.position.ReadValue();
            float width = Screen.width;
            float height = Screen.height;
            if (position.x < 0f || position.y < 0f || position.x > width || position.y > height)
                return movement;

            if (position.x < EdgeMarginPixels)
                movement.x -= 1f;
            else if (position.x > width - EdgeMarginPixels)
                movement.x += 1f;
            if (position.y > height - EdgeMarginPixels)
                movement.y += 1f;
            else if (position.y < EdgeMarginPixels)
                movement.y -= 1f;
            return movement;
        }

        private void ReadMouseInput()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                rotating = false;
                return;
            }

            if (!PointerInputBlocked)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll > 0f)
                    ZoomIn();
                else if (scroll < 0f)
                    ZoomOut();

                if (mouse.middleButton.wasPressedThisFrame)
                    rotating = true;
            }

            if (PointerInputBlocked || !mouse.middleButton.isPressed)
                rotating = false;
            if (rotating)
                Orbit(mouse.delta.ReadValue());
        }

        private float GroundHeightAtFocus()
        {
            return heightSampler != null ? heightSampler(simFocus) : 0f;
        }

        private void ConfigureCamera()
        {
            if (targetCamera == null)
                return;
            targetCamera.fieldOfView = FieldOfViewDegrees;
            targetCamera.nearClipPlane = NearClipMeters;
            targetCamera.farClipPlane = FarClipMeters;
        }

        private void Apply()
        {
            simFocus = bounds.Clamp(simFocus);
            distance = Mathf.Clamp(distance, MinDistance, MaxDistance);
            yawDegrees = WrapYaw(yawDegrees);
            pitchDegrees = Mathf.Clamp(pitchDegrees, MinPitchDegrees, MaxPitchDegrees);
            if (targetCamera == null)
                return;

            float yaw = yawDegrees * Mathf.Deg2Rad;
            float pitch = pitchDegrees * Mathf.Deg2Rad;
            float groundHeight = GroundHeightAtFocus();
            Vector3 target = ViewCoords.ToWorld(simFocus, groundHeight);
            float horizontalDistance = distance * Mathf.Cos(pitch);
            Vector2 back = new Vector2(-Mathf.Sin(yaw), -Mathf.Cos(yaw)) * horizontalDistance;
            Vector3 cameraPosition = ViewCoords.ToWorld(
                simFocus + back,
                groundHeight + distance * Mathf.Sin(pitch));
            targetCamera.transform.SetPositionAndRotation(
                cameraPosition,
                Quaternion.LookRotation(target - cameraPosition, Vector3.up));
        }

        private static float WrapYaw(float yaw)
        {
            return Mathf.Repeat(yaw + 180f, 360f) - 180f;
        }
    }
}
