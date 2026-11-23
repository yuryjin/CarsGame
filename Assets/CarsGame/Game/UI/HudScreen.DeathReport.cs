using System.Text;
using TMPro;
using UnityEngine;
using RacingGame.Core.Combat;

namespace RacingGame
{
    public sealed partial class HudScreen
    {
        private readonly RecentHit[] recentHits = new RecentHit[3];
        private int hitCount;
        private RectTransform deathPanel;
        private TextMeshProUGUI deathTitle, deathLines;
        public string LastDeathReport { get; private set; } = "";

        private void BuildDeathReport(Transform root)
        {
            var anchor = new Vector2(.5f, .97f);
            deathPanel = UiFactory.Card(root, "DeathReport", anchor, anchor,
                new Vector2(-345f, -172f), new Vector2(345f, 0f), .96f);
            deathTitle = UiFactory.Text(deathPanel, "DeathReportTitle", "", 26f, TextAlignmentOptions.MidlineLeft,
                new Vector2(0f, .74f), Vector2.one, new Vector2(15f, 0f), new Vector2(-14f, -3f), UiFactory.Accent);
            deathLines = UiFactory.Text(deathPanel, "DeathReportLines", "", 22f, TextAlignmentOptions.MidlineLeft,
                Vector2.zero, new Vector2(1f, .76f), new Vector2(15f, 3f), new Vector2(-14f, 0f), Color.white);
            deathTitle.raycastTarget = deathLines.raycastTarget = false;
            deathPanel.gameObject.SetActive(false);
        }

        private void ResetDeathReport()
        {
            hitCount = 0;
            LastDeathReport = "";
            if (deathPanel != null) deathPanel.gameObject.SetActive(false);
        }

        private void RememberHit(HitInfo hit, HitResult result)
        {
            var driver = hit.Attacker != null ? hit.Attacker.GetComponentInParent<RaceParticipant>() : null;
            if (hitCount == recentHits.Length)
            {
                System.Array.Copy(recentHits, 1, recentHits, 0, hitCount - 1);
                hitCount--;
            }
            recentHits[hitCount++] = new RecentHit {
                driver = driver != null ? driver.displayName : null, isPlayer = driver != null && driver.isPlayer,
                environmental = hit.Attacker == null, mine = hit.FromMine, damage = Mathf.CeilToInt(result.DamageDealt),
                fatal = result.Fatal, shield = result.ShieldAbsorbed
            };
            if (result.Fatal) RefreshDeathReport();
        }

        private void RefreshDeathReport()
        {
            if (hitCount == 0 || !recentHits[hitCount - 1].fatal || deathPanel == null) return;
            LastDeathReport = FormatRecentHits(recentHits, hitCount);
            deathTitle.text = Loc.Get("combat.recent_hits");
            deathLines.text = LastDeathReport;
            float scale = Mathf.Max(.01f, hudRoot.lossyScale.x);
            float width = Mathf.Min(hudRoot.rect.width - 12f / scale, Mathf.Max(690f, 280f / scale));
            float height = Mathf.Max(150f, (24f + hitCount * 15f) / scale);
            deathPanel.offsetMin = new Vector2(-width * .5f, -height);
            deathPanel.offsetMax = new Vector2(width * .5f, 0f);
            deathTitle.enableAutoSizing = deathLines.enableAutoSizing = true;
            deathTitle.fontSizeMin = 12f / scale; deathTitle.fontSizeMax = Mathf.Max(25f, 15f / scale) * UiFactory.TextScale;
            deathLines.fontSizeMin = 10f / scale; deathLines.fontSizeMax = Mathf.Max(22f, 13f / scale) * UiFactory.TextScale;
            deathPanel.gameObject.SetActive(true);
        }

        internal RecentHit[] SnapshotLastHits()
        {
            if (hitCount == 0 || !recentHits[hitCount - 1].fatal) return null;
            var saved = new RecentHit[hitCount];
            System.Array.Copy(recentHits, saved, hitCount);
            return saved;
        }

        internal static string FormatRecentHits(RecentHit[] hits, int count)
        {
            var text = new StringBuilder();
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (i > 0) text.Append('\n');
                if (hit.driver != null) text.Append(UiFactory.Escape(GetLocalizedRacerName(hit.driver, hit.isPlayer)));
                else text.Append(Loc.Get(hit.mine && hit.environmental ? "touch.secondary" : hit.environmental ? "combat.obstacle" : "combat.enemy"));
                if (hit.mine && !hit.environmental) text.Append(" · ").Append(Loc.Get("touch.secondary"));
                text.Append(" · ").Append(hit.fatal ? Loc.Get("combat.final_hit") : hit.shield ? Loc.Get("combat.shield")
                    : hit.damage > 0 ? "−" + hit.damage : Loc.Get("combat.hit"));
            }
            return text.ToString();
        }
    }
}
