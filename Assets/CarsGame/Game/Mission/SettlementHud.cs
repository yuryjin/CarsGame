using TMPro;
using UnityEngine;

namespace RacingGame
{
    public sealed class SettlementHud : MonoBehaviour
    {
        private SettlementMission mission;
        private HudScreen hud;
        private RectTransform card;
        private TextMeshProUGUI status, bearing;
        private float nextUpdate;

        public static SettlementHud Create(SettlementMission mission, HudScreen hud, RectTransform root)
        {
            var top = new Vector2(.5f, 1f);
            var card = UiFactory.Card(root, "SettlementObjective", top, top, Vector2.zero, Vector2.zero);
            card.SetAsFirstSibling();
            var view = card.gameObject.AddComponent<SettlementHud>();
            view.mission = mission; view.hud = hud; view.card = card;
            view.status = UiFactory.Text(card, "ObjectiveStatus", "", 25f, TextAlignmentOptions.Center, Vector2.zero, Vector2.one,
                new Vector2(40f, 2f), new Vector2(-5f, -2f));
            view.bearing = UiFactory.Text(card, "ObjectiveDirection", "▲", 28f, TextAlignmentOptions.Center,
                new Vector2(0f, .5f), new Vector2(0f, .5f), Vector2.zero, Vector2.zero, UiFactory.Accent);
            view.Refresh();
            return view;
        }

        private void Update()
        {
            if (mission == null || mission.Objective == null || Time.unscaledTime < nextUpdate) return;
            nextUpdate = Time.unscaledTime + .12f;
            Refresh();
        }

        private void Refresh()
        {
            float scale = Mathf.Max(.01f, card.lossyScale.x);
            float width = hud.ConstrainTopCenterWidth(MissionHud.StatusWidthPixels(Screen.width, scale));
            float font = Mathf.Max(12f, 23f * scale);
            status.fontSize = font / scale;
            bool threatened = mission.Threatened;
            status.color = threatened ? new Color(1f, .58f, .32f) : Color.white;
            float health = mission.Objective.Health / mission.Objective.MaxHealth;
            string statusKey = mission.DefendingWell ? (mission.Wave == 1 ? "settlement.wave_west" : "settlement.wave_east")
                : threatened ? "settlement.tanker_threat" : "settlement.tanker_status";
            status.text = Loc.Format(statusKey,
                "h", Mathf.CeilToInt(100f * health), "m", Mathf.CeilToInt(mission.Remaining));
            float height = status.GetPreferredValues(status.text, (width - font * 2.4f) / scale, float.PositiveInfinity).y * scale + 10f;
            float top = 24f * scale;
            card.offsetMin = new Vector2(-width * .5f, -top - height) / scale;
            card.offsetMax = new Vector2(width * .5f, -top) / scale;
            status.rectTransform.offsetMin = new Vector2(font * 2.2f, 2f) / scale;
            status.rectTransform.offsetMax = new Vector2(-5f, -2f) / scale;
            bearing.fontSize = font / scale;
            bearing.rectTransform.sizeDelta = Vector2.one * (font * 1.7f / scale);
            bearing.rectTransform.anchoredPosition = new Vector2(font / scale, 0f);
            var player = mission.Race.Player;
            var camera = Camera.main;
            if (player == null || camera == null) return;
            var nearest = mission.ClosestThreat(threatened ? 57f : 0f);
            Vector3 target = nearest != null ? nearest.transform.position : mission.DefendingWell ? mission.WaveOrigin : mission.Destination;
            Vector3 direction = target - player.transform.position;
            Vector3 right = camera.transform.right;
            right.y = 0f;
            float angle = Mathf.Atan2(Vector3.Dot(direction, right), Vector3.Dot(direction, Vector3.Cross(right, Vector3.up)));
            bearing.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -angle * Mathf.Rad2Deg);
        }
    }
}
