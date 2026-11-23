using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RacingGame
{
    /// <summary>Builds both the briefing and live combat map from the crawler’s station positions.</summary>
    internal sealed class BossModuleMap : MonoBehaviour
    {
        private readonly Button[] buttons = new Button[BossCrawler.Stations.Length];
        private readonly TextMeshProUGUI[] labels = new TextMeshProUGUI[BossCrawler.Stations.Length];
        private BossCrawler boss;
        private int selected;
        private static string PartName(BossRole role) => Loc.Get("mission.part." + role.ToString().ToLowerInvariant());

        internal static BossModuleMap Create(RectTransform parent, BossCrawler boss, int selection, Action<int> choose)
        {
            var map = parent.gameObject.AddComponent<BossModuleMap>();
            map.boss = boss;
            map.selected = selection;
            var deck = UiFactory.Panel(parent, "FortressDeck", new Vector2(.03f, .3f), new Vector2(.97f, .98f),
                Vector2.zero, Vector2.zero, UiFactory.Raised, false);
            for (int i = 0; i < BossCrawler.Stations.Length; i++)
            {
                int index = i;
                var station = BossCrawler.Stations[i];
                int col = station.Local.x < -2f ? 0 : station.Local.x > 2f ? 2 : 1;
                int row = station.Role == BossRole.Core ? 3 : station.Local.z > 0f ? 0 : station.Local.z < -6f ? 2 : 1;
                var button = UiFactory.Button(deck, "Mark" + i, PartName(station.Role), () => { map.selected = index; map.Refresh(); choose(index); },
                    new Vector2(col / 3f, 1f - (row + 1) / 4f), new Vector2((col + 1) / 3f, 1f - row / 4f),
                    new Vector2(3f, 3f), new Vector2(-3f, -3f), 29f, UiFactory.Surface);
                map.buttons[i] = button;
                map.labels[i] = button.GetComponentInChildren<TextMeshProUGUI>();
            }
            var tip = UiFactory.Text(parent, "ModuleFunctions", Loc.Get("mission.module_functions"), 24f, TextAlignmentOptions.Center,
                new Vector2(.03f, .01f), new Vector2(.97f, .29f), Vector2.zero, Vector2.zero);
            tip.enableAutoSizing = true;
            tip.fontSizeMin = 14f;
            tip.fontSizeMax = 26f;
            map.Refresh();
            if (boss != null) boss.PartDestroyed += map.PartDestroyed;
            return map;
        }

        private void PartDestroyed(BossPart part) => Refresh();
        private void OnDestroy() { if (boss != null) boss.PartDestroyed -= PartDestroyed; }

        private void Refresh()
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                bool alive = boss == null || boss.Parts[i].Targetable;
                buttons[i].interactable = alive;
                labels[i].text = (alive ? "" : "× ") + PartName(BossCrawler.Stations[i].Role);
                labels[i].color = !alive ? UiFactory.Muted : selected == i ? UiFactory.Accent : Color.white;
            }
        }
    }
}
