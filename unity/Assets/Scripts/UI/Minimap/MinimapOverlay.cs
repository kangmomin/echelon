using Echelon.Unity.Presentation.Symbols;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Echelon.Unity.UI.Minimap
{
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MinimapOverlay : MaskableGraphic, IPointerDownHandler, IDragHandler
    {
        private MinimapView _owner;

        public override Texture mainTexture => Texture2D.whiteTexture;

        public void Configure(MinimapView owner)
        {
            _owner = owner;
            raycastTarget = true;
            color = Color.white;
            SetVerticesDirty();
        }

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = true;
            color = Color.white;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || _owner == null)
                return;
            if (_owner.Click(eventData.position, eventData.pressEventCamera))
                eventData.Use();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || _owner == null)
                return;
            if (_owner.Drag(eventData.position, eventData.pressEventCamera))
                eventData.Use();
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            var batch = new SymbolMeshBatch(vertices);
            batch.Clear();
            if (_owner != null)
                _owner.PopulateOverlay(batch, rectTransform.rect);
        }
    }
}
