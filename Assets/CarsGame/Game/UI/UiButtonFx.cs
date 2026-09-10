using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RacingGame
{
    /// <summary>
    /// Adds responsive visual scale bounce and UI audio cues to buttons on hover and press.
    /// Runs on unscaled time so it works smoothly in menus and pause screens.
    /// </summary>
    public sealed class UiButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private float hoverScale = 1.045f;
        [SerializeField] private float pressScale = 0.96f;
        [SerializeField] private float smoothSpeed = 16f;
        [SerializeField] private bool playSound = true;

        private Vector3 baseScale = Vector3.one;
        private Vector3 targetScale = Vector3.one;
        private bool isHovered;
        private bool isPressed;
        private Button button;

        private void Awake()
        {
            baseScale = transform.localScale;
            targetScale = baseScale;
            button = GetComponent<Button>();
        }

        private void OnEnable()
        {
            transform.localScale = baseScale;
            targetScale = baseScale;
            isHovered = false;
            isPressed = false;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt > 0f)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, targetScale, 1f - Mathf.Exp(-smoothSpeed * dt));
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (button != null && !button.interactable) return;
            isHovered = true;
            UpdateTarget();
            if (playSound && SfxPlayer.Instance != null)
            {
                SfxPlayer.Instance.PlayUi(SfxSynth.Beep);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            isPressed = false;
            UpdateTarget();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (button != null && !button.interactable) return;
            isPressed = true;
            UpdateTarget();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isPressed = false;
            UpdateTarget();
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (button != null && !button.interactable) return;
            isHovered = true;
            UpdateTarget();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            isHovered = false;
            isPressed = false;
            UpdateTarget();
        }

        private void UpdateTarget()
        {
            if (button != null && !button.interactable)
            {
                targetScale = baseScale;
                return;
            }

            if (isPressed)
                targetScale = baseScale * pressScale;
            else if (isHovered)
                targetScale = baseScale * hoverScale;
            else
                targetScale = baseScale;
        }
    }
}
