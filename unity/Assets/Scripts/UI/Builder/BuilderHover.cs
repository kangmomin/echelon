using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Echelon.Unity.UI.Builder
{
    internal sealed class BuilderHoverTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler
    {
        private Action _entered;
        private Action _exited;

        internal void Configure(Action entered, Action exited)
        {
            _entered = entered;
            _exited = exited;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _entered?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _exited?.Invoke();
        }

        public void OnSelect(BaseEventData eventData)
        {
            _entered?.Invoke();
        }

        private void OnDisable()
        {
            _exited?.Invoke();
        }
    }

    internal sealed class BuilderDropdownHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler
    {
        private TMP_Dropdown _dropdown;
        private Action<int> _hovered;
        private Action _exited;
        private bool _wasExpanded;

        internal static BuilderDropdownHover Attach(
            TMP_Dropdown dropdown,
            Action<int> hovered,
            Action exited)
        {
            BuilderDropdownHover hover = dropdown.gameObject.AddComponent<BuilderDropdownHover>();
            hover._dropdown = dropdown;
            hover._hovered = hovered;
            hover._exited = exited;
            Transform item = dropdown.template.Find("Viewport/Content/Item");
            BuilderDropdownItem itemHover = item.gameObject.AddComponent<BuilderDropdownItem>();
            itemHover.Owner = hover;
            return hover;
        }

        internal void Hover(int index)
        {
            if (_dropdown != null && index >= 0 && index < _dropdown.options.Count)
                _hovered?.Invoke(index);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Hover(_dropdown.value);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_dropdown == null || !_dropdown.IsExpanded)
                _exited?.Invoke();
        }

        public void OnSelect(BaseEventData eventData)
        {
            Hover(_dropdown.value);
        }

        private void Update()
        {
            if (_dropdown == null)
                return;
            bool expanded = _dropdown.IsExpanded;
            if (_wasExpanded && !expanded)
                _exited?.Invoke();
            _wasExpanded = expanded;
        }

        private void OnDisable()
        {
            _wasExpanded = false;
            _exited?.Invoke();
        }
    }

    internal sealed class BuilderDropdownItem : MonoBehaviour, IPointerEnterHandler, ISelectHandler
    {
        [SerializeField] private BuilderDropdownHover _owner;

        internal BuilderDropdownHover Owner
        {
            get => _owner;
            set => _owner = value;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Owner?.Hover(ActiveItemIndex());
        }

        public void OnSelect(BaseEventData eventData)
        {
            Owner?.Hover(ActiveItemIndex());
        }

        private int ActiveItemIndex()
        {
            int index = 0;
            for (int i = 0; i < transform.GetSiblingIndex(); ++i)
            {
                Transform sibling = transform.parent.GetChild(i);
                if (sibling.gameObject.activeSelf && sibling.GetComponent<BuilderDropdownItem>() != null)
                    ++index;
            }
            return index;
        }
    }
}
