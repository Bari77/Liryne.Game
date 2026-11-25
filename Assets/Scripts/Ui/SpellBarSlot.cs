using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Liryne.Game.Ui
{
    /// <summary>Case de barre : clic pour lancer, glisser pour déplacer ou inverser.</summary>
    sealed class SpellBarSlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerClickHandler
    {
        public int Index;
        public Action<int> Clicked;
        public Action<int, PointerEventData> DragBegan;
        public Action<PointerEventData> Dragged;
        public Action Ended;
        public Action<int, int> Dropped;

        bool _dragging;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_dragging && eventData.button == PointerEventData.InputButton.Left)
            {
                Clicked?.Invoke(Index);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _dragging = true;
            DragBegan?.Invoke(Index, eventData);
        }

        public void OnDrag(PointerEventData eventData) => Dragged?.Invoke(eventData);

        public void OnEndDrag(PointerEventData eventData)
        {
            Ended?.Invoke();
            _dragging = false;
        }

        public void OnDrop(PointerEventData eventData)
        {
            var source = eventData.pointerDrag is null ? null : eventData.pointerDrag.GetComponent<SpellBarSlot>();
            if (source is not null && source.Index != Index)
            {
                Dropped?.Invoke(source.Index, Index);
            }
        }
    }
}
