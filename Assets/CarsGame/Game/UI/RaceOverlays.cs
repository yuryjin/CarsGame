using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RacingGame
{
    /// <summary>
    /// Race overlays that sit above the HUD and the touch controls: the "IN SIGHTS!" warning while an AI gunner
    /// lines up a shot at the player (the aim telegraph of <see cref="AiGunner"/>), and the one-life
    /// "continue?" prompt with its countdown. Own canvas; no per-frame allocations.
    /// </summary>
    [DefaultExecutionOrder(120)]
    public sealed class RaceOverlays : MonoBehaviour
    {
        private RaceManager race;
        private HudScreen hud;
        private RectTransform bombPanel;
        private TextMeshProUGUI bombTitle, bombHint;
        private Transform bombMarker;
        private Material bombMaterial;
        private RaceParticipant shownCarrier;
        private int shownBombSeconds = -1, shownLockSeconds = -1;
        private int bombWidth, bombHeight;
        private float bombScale;
        public string BombMessage => bombTitle != null ? bombTitle.text : "";
        private Canvas canvas;
        private RectTransform aimedPanel;
        private TextMeshProUGUI aimedText;
        private readonly Image[] aimedEdges = new Image[2];
        private RectTransform promptPanel;
        private TextMeshProUGUI promptTitle, promptTimer;
        private Button watchButton, giveUpButton;
        private Action onWatch, onGiveUp;
        private float promptUntil = -1f;
        private int lastSeconds = -1;
        private AiGunner[] gunners = new AiGunner[0];
        private RaceParticipant gunnersFor;
        private int gunnerCount = -1;

        /// <summary>True while a rival's aim telegraph targets the player (test hook).</summary>
        public bool Aimed { get; private set; }
        public bool PromptOpen => promptPanel != null && promptPanel.gameObject.activeSelf;

        private static readonly Color AimRed = new Color(1f, 0.22f, 0.18f);

        private void Awake()
        {
            race = GetComponent<RaceManager>() ?? FindAnyObjectByType<RaceManager>();
            hud = GetComponent<HudScreen>() ?? FindAnyObjectByType<HudScreen>();
            canvas = UiFactory.CreateOverlayCanvas("RaceOverlays", 25);
            canvas.transform.SetParent(transform, false);
            var root = canvas.transform;

            bombPanel = UiFactory.Card(root, "HotBomb", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-320f, -198f), new Vector2(320f, -108f));
            bombTitle = UiFactory.Text(bombPanel, "CarrierTimer", "", 32f, TextAlignmentOptions.Center, new Vector2(0f, 0.45f), Vector2.one, new Vector2(14f, 0f), new Vector2(-14f, -6f));
            bombTitle.fontStyle = FontStyles.Bold;
            bombTitle.richText = false;
            bombTitle.enableAutoSizing = false;
            bombHint = UiFactory.Text(bombPanel, "PassHint", "", 23f, TextAlignmentOptions.Center, Vector2.zero, new Vector2(1f, 0.45f), new Vector2(14f, 5f), new Vector2(-14f, 0f), UiFactory.Muted);
            bombHint.enableAutoSizing = false;
            foreach (var graphic in bombPanel.GetComponentsInChildren<Graphic>()) graphic.raycastTarget = false;
            bombPanel.gameObject.SetActive(false);

            aimedPanel = UiFactory.Card(root, "Aimed", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-170f, -160f), new Vector2(170f, -108f));
            aimedText = UiFactory.Text(aimedPanel, "Text", "", 30f, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(12f, 4f), new Vector2(-12f, -4f), AimRed);
            aimedText.fontStyle = FontStyles.Bold;
            aimedText.enableAutoSizing = true; aimedText.fontSizeMin = 18f; aimedText.fontSizeMax = 30f;
            aimedEdges[0] = UiFactory.Panel(root, "AimedL", new Vector2(0f, 0.3f), new Vector2(0f, 0.7f), Vector2.zero, new Vector2(10f, 0f), AimRed, false).GetComponent<Image>();
            aimedEdges[1] = UiFactory.Panel(root, "AimedR", new Vector2(1f, 0.3f), new Vector2(1f, 0.7f), new Vector2(-10f, 0f), Vector2.zero, AimRed, false).GetComponent<Image>();
            foreach (var img in aimedPanel.GetComponentsInChildren<Graphic>()) img.raycastTarget = false;
            foreach (var e in aimedEdges) { e.raycastTarget = false; e.gameObject.SetActive(false); }
            aimedPanel.gameObject.SetActive(false);

            promptPanel = UiFactory.Card(root, "ContinuePrompt", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-330f, -170f), new Vector2(330f, 170f));
            promptPanel.GetComponent<Image>().raycastTarget = true;
            promptTitle = UiFactory.Text(promptPanel, "Title", "", 40f, TextAlignmentOptions.Center, new Vector2(0f, 1f), Vector2.one, new Vector2(30f, -96f), new Vector2(-30f, -24f));
            promptTitle.fontStyle = FontStyles.Bold;
            promptTitle.enableAutoSizing = true; promptTitle.fontSizeMin = 24f; promptTitle.fontSizeMax = 40f;
            promptTimer = UiFactory.Text(promptPanel, "Timer", "", 30f, TextAlignmentOptions.Center, new Vector2(0f, 1f), Vector2.one, new Vector2(30f, -150f), new Vector2(-30f, -100f), UiFactory.Muted);
            watchButton = UiFactory.Button(promptPanel, "Watch", "", () => Choose(true), Vector2.zero, new Vector2(1f, 0f), new Vector2(30f, 100f), new Vector2(-30f, 176f), 30f, UiFactory.Positive);
            giveUpButton = UiFactory.Button(promptPanel, "GiveUp", "", () => Choose(false), Vector2.zero, new Vector2(1f, 0f), new Vector2(30f, 24f), new Vector2(-30f, 88f), 26f, UiFactory.Raised);
            promptPanel.gameObject.SetActive(false);
        }

        /// <summary>Open the one-life prompt; it gives up by itself after <paramref name="seconds"/>.</summary>
        public void ShowContinuePrompt(float seconds, Action watch, Action giveUp)
        {
            onWatch = watch; onGiveUp = giveUp;
            promptTitle.text = Loc.Get("continue.title");
            watchButton.GetComponentInChildren<TextMeshProUGUI>().text = Loc.Get("continue.watch");
            giveUpButton.GetComponentInChildren<TextMeshProUGUI>().text = Loc.Get("continue.give_up");
            watchButton.interactable = giveUpButton.interactable = true;
            promptUntil = Time.unscaledTime + seconds;
            lastSeconds = -1;
            promptPanel.gameObject.SetActive(true);
            EventSystem.current?.SetSelectedGameObject(watchButton.gameObject);
        }

        public void HideContinuePrompt()
        {
            onWatch = onGiveUp = null;
            promptUntil = -1f;
            if (promptPanel != null) promptPanel.gameObject.SetActive(false);
        }

        private void Choose(bool watch)
        {
            var action = watch ? onWatch : onGiveUp;
            promptUntil = -1f;   // the ad may take longer than the countdown
            watchButton.interactable = giveUpButton.interactable = false;
            if (watch) promptTimer.text = Loc.Get("common.loading");
            action?.Invoke();
        }

        private void Update()
        {
            if (PromptOpen && promptUntil > 0f)
            {
                // Paused (menu, hidden tab, host overlay): the decision time does not run.
                if (PauseController.IsPaused) { promptUntil += Time.unscaledDeltaTime; return; }
                float left = promptUntil - Time.unscaledTime;
                int secs = Mathf.CeilToInt(Mathf.Max(0f, left));
                if (secs != lastSeconds) { lastSeconds = secs; promptTimer.SetText("{0}", secs); }
                if (left <= 0f) Choose(false);
            }
            UpdateAimed();
            UpdateBomb();
        }

        private void UpdateAimed()
        {
            var player = race != null ? race.Player : null;
            bool racing = player != null && player.HotBomb == null && player.Car != null && race.State == RaceState.Racing && !PauseController.IsPaused && !PromptOpen
                          && (player.Health == null || !player.Health.IsDead);
            bool aimed = false;
            if (racing)
            {
                if (gunnersFor != player || gunnerCount != race.Participants.Count) CollectGunners(player);
                for (int i = 0; i < gunners.Length; i++)
                    if (gunners[i] != null && gunners[i].AimTarget == player.Car) { aimed = true; break; }
            }
            if (aimed != Aimed)
            {
                Aimed = aimed;
                if (aimed) aimedText.text = Loc.Get("hud.aimed");
                aimedPanel.gameObject.SetActive(aimed);
                foreach (var e in aimedEdges) e.gameObject.SetActive(aimed);
                if (aimed) SfxPlayer.Instance.PlayUi(SfxSynth.Beep, 0.45f, 1.6f);
            }
            if (!aimed) return;
            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * 4f);
            var c = new Color(AimRed.r, AimRed.g, AimRed.b, 0.35f + 0.5f * pulse);
            foreach (var e in aimedEdges) e.color = c;
            float s = 1f + 0.05f * pulse;
            aimedPanel.localScale = new Vector3(s, s, 1f);
        }

        private void UpdateBomb()
        {
            var player = race != null ? race.Player : null;
            var bomb = player != null ? player.HotBomb : null;
            bool visible = canvas.gameObject.activeSelf && bomb != null && !PauseController.IsPaused
                && race.State >= RaceState.Countdown && race.State < RaceState.Finished && !race.HasFinished(player);
            bombPanel.gameObject.SetActive(visible);
            var carrier = visible ? bomb.Carrier : null;
            if (bombMarker != null) bombMarker.gameObject.SetActive(carrier != null);
            if (!visible) { shownCarrier = null; shownBombSeconds = -1; hud?.PlaceHintBelowBomb(0f); return; }
            int seconds = carrier != null ? Mathf.CeilToInt(bomb.SecondsLeft) : 0;
            int lockSeconds = carrier != null ? Mathf.CeilToInt(bomb.PassLockSeconds) : 0;
            if (shownCarrier != carrier || shownBombSeconds != seconds || shownLockSeconds != lockSeconds)
            {
                bool mine = carrier == player;
                bombTitle.text = carrier == null ? Loc.Get("bomb.wait") : Loc.Format(mine ? "bomb.yours" : "bomb.owner",
                    "name", UiFactory.Escape(HudScreen.GetLocalizedRacerName(carrier.displayName, carrier.isPlayer)), "n", seconds);
                bombTitle.color = mine ? AimRed : UiFactory.Accent;
                bombHint.text = mine && lockSeconds > 0 ? Loc.Format("bomb.locked", "n", lockSeconds)
                    : Loc.Get(carrier == null ? "bomb.rules" : mine ? "bomb.pass" : "bomb.avoid");
                if (carrier != null && (mine || seconds <= 5) && (shownCarrier != carrier || shownBombSeconds != seconds))
                    SfxPlayer.Instance.PlayUi(SfxSynth.Beep, 0.35f, seconds <= 5 ? 1.8f : 1.2f);
                shownCarrier = carrier;
                shownBombSeconds = seconds;
                shownLockSeconds = lockSeconds;
                bombWidth = 0; // Text changes can change the preferred line count.
            }
            LayoutBomb();
            hud?.PlaceHintBelowBomb(-bombPanel.offsetMin.y * canvas.scaleFactor);
            if (carrier == null) return;
            if (bombMarker == null) BuildBombMarker();
            bombMarker.gameObject.SetActive(true);
            bombMarker.position = carrier.transform.position + Vector3.up * 3f;
            float pulse = 1f + 0.1f * Mathf.Sin(bomb.SecondsLeft * Mathf.PI * (seconds <= 5 ? 8f : 2f));
            bombMarker.localScale = Vector3.one * pulse;
            Color color = seconds <= 5 ? AimRed : UiFactory.Accent;
            bombMaterial.SetColor("_BaseColor", color);
            bombMaterial.SetColor("_EmissionColor", color * 1.5f);
        }

        private void LayoutBomb()
        {
            float scale = Mathf.Max(0.01f, canvas.scaleFactor);
            if (bombWidth == Screen.width && bombHeight == Screen.height && Mathf.Abs(bombScale - scale) < 0.001f) return;
            bombWidth = Screen.width; bombHeight = Screen.height; bombScale = scale;
            float width = Mathf.Min(Screen.width - 24f, Mathf.Max(280f, 640f * scale));
            float title = Mathf.Max(14f, 32f * scale), detail = Mathf.Max(12f, 23f * scale);
            bombTitle.fontSize = title / scale; bombHint.fontSize = detail / scale;
            float textWidth = (width - 20f) / scale;
            float titleHeight = Mathf.Max(title + 4f, bombTitle.GetPreferredValues(bombTitle.text, textWidth, float.PositiveInfinity).y * scale + 2f);
            float hintHeight = Mathf.Max(detail + 4f, bombHint.GetPreferredValues(bombHint.text, textWidth, float.PositiveInfinity).y * scale + 2f);
            float height = titleHeight + hintHeight + 14f;
            bool narrow = ((RectTransform)canvas.transform).rect.width < 1400f;
            float top = (narrow ? 430f : 100f) * scale + 8f;
            bombPanel.offsetMin = new Vector2(-width * 0.5f, -top - height) / scale;
            bombPanel.offsetMax = new Vector2(width * 0.5f, -top) / scale;
            PlaceBombText(bombTitle.rectTransform, width, 6f, titleHeight, scale);
            PlaceBombText(bombHint.rectTransform, width, 8f + titleHeight, hintHeight, scale);
        }

        private static void PlaceBombText(RectTransform rect, float width, float top, float height, float scale)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.offsetMin = new Vector2(10f, -top - height) / scale;
            rect.offsetMax = new Vector2(width - 10f, -top) / scale;
        }

        private void BuildBombMarker()
        {
            bombMarker = new GameObject("HotBombMarker").transform;
            bombMarker.SetParent(transform, false);
            bombMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "HotBombGlow" };
            bombMaterial.EnableKeyword("_EMISSION");
            foreach (var shape in new[] { PrimitiveType.Sphere, PrimitiveType.Cube })
            {
                var piece = GameObject.CreatePrimitive(shape);
                Destroy(piece.GetComponent<Collider>());
                piece.transform.SetParent(bombMarker, false);
                piece.transform.localPosition = shape == PrimitiveType.Sphere ? Vector3.zero : Vector3.up * 0.8f;
                piece.transform.localScale = shape == PrimitiveType.Sphere ? Vector3.one * 1.2f : new Vector3(0.2f, 0.5f, 0.2f);
                var renderer = piece.GetComponent<Renderer>();
                renderer.sharedMaterial = bombMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        private void OnDestroy() { if (bombMaterial != null) Destroy(bombMaterial); }

        private void CollectGunners(RaceParticipant player)
        {
            gunnersFor = player;
            gunnerCount = race.Participants.Count;
            int n = 0;
            var tmp = new AiGunner[gunnerCount];
            foreach (var p in race.Participants)
            {
                if (p == null || p.isPlayer) continue;
                var g = p.GetComponent<AiGunner>();
                if (g != null) tmp[n++] = g;
            }
            Array.Resize(ref tmp, n);
            gunners = tmp;
        }

        public void SetVisible(bool visible)
        {
            if (!visible)
            {
                HideContinuePrompt();
                if (bombMarker != null) bombMarker.gameObject.SetActive(false);
            }
            if (canvas != null) canvas.gameObject.SetActive(visible);
        }
    }
}
