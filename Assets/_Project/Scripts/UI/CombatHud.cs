using System.Collections.Generic;
using ShapeShifty.AI;
using ShapeShifty.Combat;
using ShapeShifty.Levels;
using ShapeShifty.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShapeShifty.UI
{
    // The fighting HUD: a crosshair and the poo supply whenever the player can throw (red
    // over a Grey or a Bullseye), a line for messages, and in a level with someone to fight,
    // the player's health as pips, a brown splat across the screen when they're hit, how many
    // Greys are left, and the Grey King's health once he's awake.
    public class CombatHud : MonoBehaviour
    {
        [SerializeField] Health playerHealth;
        [SerializeField] PlayerThrower thrower;

        [Header("Health")]
        [SerializeField] CanvasGroup healthPanel;
        [Tooltip("Copied once per point of health.")]
        [SerializeField] Image pipTemplate;
        [SerializeField] Color fullColor = new Color(0.95f, 0.3f, 0.3f);
        [SerializeField] Color emptyColor = new Color(0.2f, 0.2f, 0.2f, 0.6f);

        [Header("Poo")]
        [SerializeField] CanvasGroup ammoPanel;
        [SerializeField] TMP_Text ammoText;
        [SerializeField] Color lowAmmoColor = new Color(1f, 0.45f, 0.3f);

        [Header("Crosshair")]
        [SerializeField] GameObject crosshair;
        [SerializeField] Image[] crosshairParts;
        [SerializeField] Color crosshairColor = Color.white;
        [SerializeField] Color onTargetColor = new Color(1f, 0.3f, 0.25f);

        [Header("Hit")]
        [SerializeField] CanvasGroup hitFlash;
        [SerializeField] float flashTime = 0.7f;

        [Header("Foes")]
        [SerializeField] TMP_Text foesText;
        [SerializeField] CanvasGroup bossPanel;
        [SerializeField] TMP_Text bossName;
        [SerializeField] Image bossFill;

        [Header("Messages")]
        [SerializeField] TMP_Text message;
        [SerializeField] float messageTime = 2.5f;

        readonly List<Image> pips = new List<Image>();
        bool combatLevel;
        float flashUntil, messageUntil;
        Color ammoColor;
        Vector3 ammoScale = Vector3.one;
        float ammoPunch;

        void Start()
        {
            ammoColor = ammoText != null ? ammoText.color : Color.white;
            if (pipTemplate != null)
            {
                pipTemplate.gameObject.SetActive(false);
                int count = playerHealth != null ? Mathf.CeilToInt(playerHealth.Max) : 0;
                for (int i = 0; i < count; i++)
                {
                    var pip = Instantiate(pipTemplate, pipTemplate.transform.parent);
                    pip.gameObject.SetActive(true);
                    pips.Add(pip);
                }
            }
            if (playerHealth != null) playerHealth.Damaged += OnHurt;
            if (playerHealth != null) playerHealth.Healed += () => Say("Banana! Feeling better");
            if (thrower != null)
            {
                thrower.OutOfAmmo += () => Say("Out of poo! Find a poo pile");
                thrower.CantThrow += () => Say("Only a monkey can throw poo");
                thrower.Thrown += () => ammoPunch = 1f;
                thrower.Refilled += () => Say("Poo topped up");
            }
            KingMonkey.Beaten += OnKingBeaten;
            Checkpoint.Reached += OnCheckpoint;
            Ambush.Sprung += OnAmbush;
            if (message != null) message.gameObject.SetActive(false);
            if (hitFlash != null) hitFlash.alpha = 0f;
        }

        void OnDestroy()
        {
            KingMonkey.Beaten -= OnKingBeaten;
            Checkpoint.Reached -= OnCheckpoint;
            Ambush.Sprung -= OnAmbush;
        }

        void OnHurt(float amount, Vector3 from) => flashUntil = Time.time + flashTime;
        void OnKingBeaten() => Say("The Guerilla King is beaten! The gate's open", 5f);
        void OnCheckpoint(Checkpoint checkpoint) => Say("Checkpoint");
        void OnAmbush(Ambush ambush) => Say("Ambush! Guerillas in the trees!");

        public void Say(string text, float seconds = -1f)
        {
            if (message == null) return;
            message.text = text;
            message.gameObject.SetActive(true);
            messageUntil = Time.time + (seconds > 0f ? seconds : messageTime);
        }

        void Update()
        {
            // A level with someone to fight in it (checked until it's found one).
            if (!combatLevel) combatLevel = Health.AnyFoes;
            // Throwing works in any level (at targets, say); health and foes only matter in a fight.
            bool aiming = thrower != null && thrower.CanThrow;
            SetVisible(healthPanel, combatLevel);
            SetVisible(ammoPanel, aiming);
            if (foesText != null) foesText.gameObject.SetActive(combatLevel);

            if (thrower != null && ammoText != null)
            {
                ammoText.text = "Poo  " + thrower.Ammo + " / " + thrower.MaxAmmo;
                ammoText.color = thrower.Ammo <= 3 ? lowAmmoColor : ammoColor;
                ammoPunch = Mathf.MoveTowards(ammoPunch, 0f, Time.deltaTime * 6f);
                ammoText.transform.localScale = ammoScale * (1f + ammoPunch * 0.15f);
            }

            if (crosshair != null) crosshair.SetActive(aiming);
            if (aiming && crosshairParts != null)
            {
                Color c = thrower.AimedAt != null || thrower.AimedAtTarget != null ? onTargetColor : crosshairColor;
                foreach (var part in crosshairParts) if (part != null) part.color = c;
            }

            if (message != null && message.gameObject.activeSelf && Time.time > messageUntil) message.gameObject.SetActive(false);
            if (!combatLevel)
            {
                SetVisible(bossPanel, false);
                return;
            }

            for (int i = 0; i < pips.Count; i++)
                pips[i].color = playerHealth != null && i < Mathf.CeilToInt(playerHealth.Current) ? fullColor : emptyColor;

            if (hitFlash != null)
                hitFlash.alpha = Mathf.Clamp01((flashUntil - Time.time) / flashTime) * 0.85f;

            if (foesText != null)
            {
                int left = 0;
                foreach (var m in Monkey.All) if (m.Hostile && !m.IsDown) left++;
                var king = KingMonkey.Current;
                bool kingLeft = king != null && !king.Health.IsDead;
                foesText.text = "Guerillas left: " + left + (kingLeft ? "  + the King" : "");
            }

            var boss = KingMonkey.Current;
            bool showBoss = boss != null && boss.IsAwake;
            SetVisible(bossPanel, showBoss);
            if (showBoss)
            {
                if (bossName != null) bossName.text = boss.DisplayName;
                if (bossFill != null) bossFill.fillAmount = Mathf.MoveTowards(bossFill.fillAmount, boss.Health.Fraction, Time.deltaTime * 0.8f);
            }
        }

        static void SetVisible(CanvasGroup group, bool visible)
        {
            if (group == null) return;
            group.alpha = Mathf.MoveTowards(group.alpha, visible ? 1f : 0f, Time.unscaledDeltaTime * 6f);
        }
    }
}
