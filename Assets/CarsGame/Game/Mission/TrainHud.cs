using TMPro;
using UnityEngine;

namespace RacingGame
{
    public sealed class TrainHud : MonoBehaviour
    {
        private TrainMission mission;
        private HudScreen hud;
        private RectTransform card;
        private TextMeshProUGUI objective, bearing;
        private AudioSource whistle;
        private float whistleUntil;
        private int shownPercent = -1;
        private string shownKey, shownLanguage;
        private float lastWidth, lastScale;
        private bool lastBearing;

        public static TrainHud Create(TrainMission mission, HudScreen hud, RectTransform root)
        {
            var top = new Vector2(0.5f, 1f);
            var panel = UiFactory.Card(root, "TrainObjective", top, top, Vector2.zero, Vector2.zero);
            panel.SetAsFirstSibling();
            var view = panel.gameObject.AddComponent<TrainHud>();
            view.mission = mission;
            view.hud = hud;
            view.card = panel;
            view.objective = UiFactory.Text(panel, "TrainGoal", "", 26f, TextAlignmentOptions.MidlineLeft,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            view.bearing = UiFactory.Text(panel, "TrainBearing", "▲", 26f, TextAlignmentOptions.Center,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero, UiFactory.Accent);
            view.objective.raycastTarget = view.bearing.raycastTarget = false;
            view.Refresh();
            return view;
        }

        private void Update()
        {
            if (mission == null) return;
            if (whistle != null) whistle.volume = SfxPlayer.Instance.volume;
            if (!PauseController.IsPaused) Refresh();
        }

        private void Refresh()
        {
            var route = mission.Route;
            var escort = mission.Escort;
            var player = mission.Race.Player;
            string key = "train.fork";
            int percent = -1;
            Vector3? target = null;
            if (route.PassedWhistlePoint && route.Locomotive != null && whistle == null)
            {
                whistle = route.Locomotive.gameObject.AddComponent<AudioSource>();
                whistle.playOnAwake = false;
                whistle.spatialBlend = 0.85f;
                whistle.rolloffMode = AudioRolloffMode.Linear;
                whistle.minDistance = 35f;
                whistle.maxDistance = 650f;
                whistle.volume = SfxPlayer.Instance.volume;
                whistle.clip = SfxSynth.TrainWhistle;
                whistle.Play();
                whistleUntil = Time.time + 3f;
                hud.ShowEvent(Loc.Get("train.whistle"), UiFactory.Accent);
            }

            if (mission.Diverted)
            {
                key = route.AtStation ? "train.station" : "train.siding";
                target = route.StationPoint;
            }
            else if (escort.PlayerAtTail && escort.ApproachClear)
            {
                key = "train.hold";
                percent = Mathf.FloorToInt(mission.CaptureProgress * 100f);
            }
            else if (route.Tail != null)
            {
                bool nearTail = player != null && (player.transform.position - route.Tail.position).sqrMagnitude < 18f * 18f;
                if (escort.PlayerAtTail || (escort.Released && nearTail) || route.PassedSwitch)
                {
                    key = (escort.PlayerAtTail || nearTail) && !escort.ApproachClear ? "train.blocked" : "train.pursue";
                    target = route.Tail.position;
                }
            }
            if (whistle != null && Time.time < whistleUntil && !mission.Diverted) target = route.Locomotive.position;

            bool showBearing = target.HasValue && player != null;
            if (showBearing)
            {
                var cam = Camera.main;
                var right = cam != null ? cam.transform.right : player.transform.right;
                right.y = 0f;
                Vector3 offset = target.Value - player.transform.position;
                float angle = Mathf.Atan2(Vector3.Dot(offset, right), Vector3.Dot(offset, Vector3.Cross(right, Vector3.up)));
                bearing.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -angle * Mathf.Rad2Deg);
            }

            float scale = Mathf.Max(0.01f, card.lossyScale.x);
            float width = hud.ConstrainTopCenterWidth(MissionHud.StatusWidthPixels(Screen.width, scale));
            if (shownKey == key && shownPercent == percent && shownLanguage == Loc.Language && showBearing == lastBearing
                && Mathf.Approximately(width, lastWidth) && Mathf.Approximately(scale, lastScale)) return;
            shownKey = key; shownPercent = percent; shownLanguage = Loc.Language;
            lastWidth = width; lastScale = scale; lastBearing = showBearing;
            objective.text = percent < 0 ? Loc.Get(key) : Loc.Format(key, "n", percent);
            float font = Mathf.Max(13f, 26f * scale);
            objective.fontSize = font / scale;
            float inset = showBearing ? font * 2.6f : 12f;
            float height = objective.GetPreferredValues(objective.text, (width - inset - 12f) / scale, float.PositiveInfinity).y * scale + 14f;
            float top = 24f * scale;
            card.offsetMin = new Vector2(-width * 0.5f, -top - height) / scale;
            card.offsetMax = new Vector2(width * 0.5f, -top) / scale;
            objective.rectTransform.offsetMin = new Vector2(inset, 4f) / scale;
            objective.rectTransform.offsetMax = new Vector2(-12f, -4f) / scale;
            bearing.gameObject.SetActive(showBearing);
            bearing.fontSize = font / scale;
            bearing.rectTransform.sizeDelta = Vector2.one * (font * 2f / scale);
            bearing.rectTransform.anchoredPosition = new Vector2(inset * 0.52f / scale, 0f);
        }

        internal static string ResultKey(TrainOutcome outcome)
        {
            switch (outcome)
            {
                case TrainOutcome.CapturedOnTrain: return "train.win.moving";
                case TrainOutcome.CapturedAtStation: return "train.win.station";
                case TrainOutcome.Escaped: return "train.loss";
                default: return "events.m2.name";
            }
        }

        private void OnDisable() { if (whistle != null) whistle.Stop(); }
    }
}
