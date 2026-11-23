using UnityEngine;
using UnityEngine.EventSystems;

namespace RacingGame
{
    public sealed class TouchLayoutDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        internal TouchControlsPresenter Owner;
        private Vector2 offset;

        public void OnBeginDrag(PointerEventData data)
        {
            var rect = (RectTransform)transform;
            offset = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) - data.pressPosition;
        }
        public void OnDrag(PointerEventData data) { if (Owner.EditingLayout) Owner.MoveButton((RectTransform)transform, data.position + offset); }
        public void OnEndDrag(PointerEventData data) { if (Owner.EditingLayout) Owner.StoreButton((RectTransform)transform); }
    }
}
