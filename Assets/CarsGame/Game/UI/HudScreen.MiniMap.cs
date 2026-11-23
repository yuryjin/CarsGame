using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RacingGame
{
    public sealed partial class HudScreen
    {
        private Button miniButton;
        private CircuitMapGraphic miniRoute;
        private TextMeshProUGUI miniLabel;
        private RectTransform miniLeader, miniPlayer, miniFork;
        private TrackPath miniPath;
        private Vector3 miniEntry;
        private float miniEntryDistance, nextMiniRefresh;
        private bool miniExpanded = true;

        private void BuildMiniMap(Transform root)
        {
            miniButton = UiFactory.Button(root, "ToggleMiniMap", "", ToggleMiniMap, Vector2.zero, Vector2.zero,
                Vector2.zero, Vector2.one, 24f, UiFactory.Surface);
            miniLabel = miniButton.GetComponentInChildren<TextMeshProUGUI>();
            var area = UiFactory.Panel(miniButton.transform, "MiniMapRoute", Vector2.zero, Vector2.one,
                new Vector2(6f, 4f), new Vector2(-6f, -4f));
            miniRoute = area.gameObject.AddComponent<CircuitMapGraphic>();
            miniFork = MapMark(area, "NextFork", "◆", UiFactory.Accent);
            miniLeader = MapMark(area, "Leader", "1", Color.white);
            miniPlayer = MapMark(area, "You", "●", UiFactory.Positive);
            miniLabel.transform.SetAsLastSibling();
            miniButton.gameObject.SetActive(false);
        }

        private static RectTransform MapMark(RectTransform area, string name, string symbol, Color tint)
        {
            var text = UiFactory.Text(area, name, symbol, 32f, TextAlignmentOptions.Center, Vector2.one * 0.5f,
                Vector2.one * 0.5f, new Vector2(-20f, -22f), new Vector2(20f, 22f), tint);
            text.raycastTarget = false;
            text.fontStyle = FontStyles.Bold;
            UiFactory.SetOutline(text, 0.25f);
            return text.rectTransform;
        }

        private void StartMiniMap()
        {
            miniPath = RaceSetup.Event?.mode == EventMode.Race && gameFlow != null && !gameFlow.IsTestDrive
                ? TrackPath.Instance : null;
            miniButton.gameObject.SetActive(miniPath != null);
            if (miniPath == null) return;
            miniRoute.SetRoute(miniPath.Spline, miniPath.startLineDistance, miniPath.Fork);
            if (miniPath.Fork != null)
            {
                miniEntry = miniPath.Fork.Position(miniPath.reversed ? 1f : 0f);
                int hint = -1;
                miniEntryDistance = miniPath.Spline.Project(miniEntry, ref hint, 0);
            }
            LayoutMiniMap();
            nextMiniRefresh = 0f;
            UpdateMiniMap();
        }

        private void ToggleMiniMap()
        {
            miniExpanded = !miniExpanded;
            LayoutMiniMap();
            nextMiniRefresh = 0f;
            UpdateMiniMap();
        }

        private void LayoutMiniMap()
        {
            if (miniButton == null) return;
            float scale = Mathf.Max(0.1f, hudRoot.lossyScale.x);
            float left = racePanel.offsetMax.x + 6f / scale;
            float right = 0.5f * hudRoot.rect.width + timerPanel.offsetMin.x - 6f / scale;
            float width = Mathf.Min(Mathf.Max(44f / scale, right - left), (miniExpanded ? 104f : 60f) / scale);
            float height = (miniExpanded ? 62f : 44f) / scale;
            float top = racePanel.offsetMax.y;
            Place((RectTransform)miniButton.transform, new Vector2(0f, 1f), new Vector2(left, top - height), new Vector2(left + width, top));
            miniRoute.gameObject.SetActive(miniExpanded);
            miniLabel.alignment = miniExpanded ? TextAlignmentOptions.TopRight : TextAlignmentOptions.Center;
            miniLabel.fontSizeMin = 12f / scale;
            miniLabel.fontSizeMax = 14f / scale;
            miniLabel.text = miniExpanded ? "–" : Loc.Get("hud.mini_map");
        }

        private void UpdateMiniMap()
        {
            if (!miniExpanded || miniPath == null || !miniButton.isActiveAndEnabled || Time.unscaledTime < nextMiniRefresh) return;
            nextMiniRefresh = Time.unscaledTime + 0.1f;
            var player = race.Player;
            if (player == null || player.Progress == null) return;
            RaceParticipant leader = null;
            for (int i = 0; i < race.Participants.Count; i++)
            {
                var car = race.Participants[i];
                if (car != null && race.PositionOf(car) == 1) { leader = car; break; }
            }
            miniLeader.gameObject.SetActive(leader != null);
            if (leader != null) miniLeader.anchoredPosition = miniRoute.MapPosition(leader.transform.position);
            miniPlayer.gameObject.SetActive(leader != player);
            if (leader != player) miniPlayer.anchoredPosition = miniRoute.MapPosition(player.transform.position);
            // The authored fork has one entrance per lap; after the final pass there is no next fork.
            bool upcoming = miniPath.Fork != null && (race.State == RaceState.Countdown || player.Progress.LapsCompleted < race.laps - 1 ||
                miniPath.Wrap(player.Progress.Distance - miniPath.startLineDistance) <= miniPath.Wrap(miniEntryDistance - miniPath.startLineDistance));
            miniFork.gameObject.SetActive(upcoming);
            if (upcoming) miniFork.anchoredPosition = miniRoute.MapPosition(miniEntry);
        }
    }
}
