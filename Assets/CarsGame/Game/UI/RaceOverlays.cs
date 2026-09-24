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
            canvas = UiFactory.CreateOverlayCanvas("RaceOverlays", 25);
            canvas.transform.SetParent(transform, false);
            var root = canvas.transform;

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
                float left = promptUntil - Time.unscaledTime;
                int secs = Mathf.CeilToInt(Mathf.Max(0f, left));
                if (secs != lastSeconds) { lastSeconds = secs; promptTimer.SetText("{0}", secs); }
                if (left <= 0f) Choose(false);
            }
            UpdateAimed();
        }

        private void UpdateAimed()
        {
            var player = race != null ? race.Player : null;
            bool racing = player != null && player.Car != null && race.State == RaceState.Racing && !PauseController.IsPaused && !PromptOpen
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
            if (!visible) HideContinuePrompt();
            if (canvas != null) canvas.gameObject.SetActive(visible);
        }
    }
}
