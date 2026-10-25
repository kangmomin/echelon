using System;
using System.Collections.Generic;
using UnityEngine;

namespace Echelon.Unity.Presentation.Fx
{
    public sealed class FxLayer : MonoBehaviour
    {
        [SerializeField] private FxQuality quality = FxQuality.Auto;

        private FxCaps? _customCaps;
        private Func<Vector3, float> _groundHeight;
        private FxBatchRenderer _renderer;
        private FxFlashLightPool _lightPool;

        public FxEffectSystem Effects { get; private set; }

        public Func<Vector3, float> GroundHeight
        {
            get { return _groundHeight; }
            set
            {
                _groundHeight = value;
                if (Effects != null)
                    Effects.GroundHeight = value;
            }
        }

        public FxCaps CurrentCaps
        {
            get { return Effects != null ? Effects.Caps : _customCaps ?? FxCaps.ForQuality(quality); }
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void LateUpdate()
        {
            EnsureInitialized();
            _renderer.Render(Effects);
            _lightPool.Sync(Effects);
        }

        private void OnDisable()
        {
            if (_lightPool != null)
                _lightPool.Clear();
        }

        private void OnDestroy()
        {
            if (_renderer != null)
                _renderer.Dispose();
            if (_lightPool != null)
                _lightPool.Dispose();
        }

        public void Enqueue(FxEvent fxEvent)
        {
            EnsureInitialized();
            Effects.Enqueue(fxEvent);
        }

        public void SetSmokeCircles(IEnumerable<FxSmoke> smokeCircles)
        {
            EnsureInitialized();
            Effects.SetSmokeCircles(smokeCircles);
        }

        public void Advance(float dt, int tick)
        {
            EnsureInitialized();
            Effects.Update(dt, tick);
        }

        public void ResetEffects(uint seed = FxEffectSystem.DefaultSeed)
        {
            EnsureInitialized();
            Effects.Reset(seed);
            _lightPool.Clear();
        }

        public void SetQuality(FxQuality value)
        {
            quality = value;
            _customCaps = null;
            RebuildEffects(FxCaps.ForQuality(value));
        }

        public void SetCaps(FxCaps caps)
        {
            caps.Validate();
            _customCaps = caps;
            RebuildEffects(caps);
        }

        private void EnsureInitialized()
        {
            if (Effects == null)
            {
                Effects = new FxEffectSystem(CurrentCaps) { GroundHeight = _groundHeight };
            }
            if (_renderer == null)
            {
                _renderer = new FxBatchRenderer();
                _renderer.Initialize();
            }
            if (_lightPool == null)
                _lightPool = new FxFlashLightPool(transform);
        }

        private void RebuildEffects(FxCaps caps)
        {
            if (_lightPool != null)
                _lightPool.Clear();
            if (Effects != null)
                Effects = new FxEffectSystem(caps) { GroundHeight = _groundHeight };
        }
    }
}
