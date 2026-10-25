using System.Collections.Generic;
using UnityEngine;

namespace Echelon.Unity.Presentation.Fx
{
    internal sealed class FxFlashLightPool
    {
        private readonly Transform _parent;
        private readonly List<Light> _lights = new List<Light>();

        internal FxFlashLightPool(Transform parent)
        {
            _parent = parent;
        }

        internal void Sync(FxEffectSystem effects)
        {
            while (_lights.Count < effects.FlashLightSlotCount)
            {
                GameObject lightObject = new GameObject("Fx Flash " + _lights.Count);
                lightObject.transform.SetParent(_parent, false);
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.shadows = LightShadows.None;
                light.bounceIntensity = 0.0f;
                light.enabled = false;
                _lights.Add(light);
            }

            for (int i = 0; i < _lights.Count; i++)
            {
                Light light = _lights[i];
                if (i >= effects.FlashLightSlotCount)
                {
                    light.enabled = false;
                    continue;
                }
                FxFlashState flash = effects.GetFlashLight(i);
                light.enabled = flash.Active;
                if (!flash.Active)
                    continue;
                light.transform.position = flash.Position;
                light.color = flash.Color;
                light.range = flash.Range;
                light.intensity = flash.Intensity;
            }
        }

        internal void Clear()
        {
            for (int i = 0; i < _lights.Count; i++)
            {
                if (_lights[i] != null)
                    _lights[i].enabled = false;
            }
        }

        internal void Dispose()
        {
            for (int i = 0; i < _lights.Count; i++)
            {
                Light light = _lights[i];
                if (light == null)
                    continue;
                if (Application.isPlaying)
                    Object.Destroy(light.gameObject);
                else
                    Object.DestroyImmediate(light.gameObject);
            }
            _lights.Clear();
        }
    }
}
