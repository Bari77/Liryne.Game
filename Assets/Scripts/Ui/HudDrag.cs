using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Liryne.Game.Ui
{
    /// <summary>Déplace un panneau HUD par glisser depuis une poignée.</summary>
    sealed class HudDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public RectTransform Target;
        public Action Moved;

        Vector2 _origin;
        Vector2 _pointer;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (Target is null)
            {
                return;
            }

            _origin = Target.anchoredPosition;
            Local(eventData, out _pointer);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (Target is null || !Local(eventData, out Vector2 now))
            {
                return;
            }

            Target.anchoredPosition = _origin + (now - _pointer);
            Moved?.Invoke();
        }

        public void OnEndDrag(PointerEventData eventData) => Moved?.Invoke();

        bool Local(PointerEventData eventData, out Vector2 point)
        {
            Transform parent = Target.parent;
            var parentRt = parent as RectTransform;
            if (parentRt is null)
            {
                point = default;
                return false;
            }

            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRt, eventData.position, eventData.pressEventCamera, out point);
        }
    }
}
